using System;
using System.Collections.Generic;
using System.Linq;
using ProjectH.Account;
using ProjectH.Core;
using ProjectH.Data.Tables;
using UnityEngine;
using UnityEngine.UI;
using ProjectH.UI;

namespace ProjectH.UI.Scenes
{
    public sealed class OfficeSceneController : MonoBehaviour
    {
        private enum Tab { Mercs, Craft, Inventory }

        // ── 공통 UI ──────────────────────────────────────────────────
        private Text headerCreditsText;
        private Text headerLevelText;
        private Transform contentRoot;           // 콘텐츠 스크롤 부모
        private Tab currentTab = Tab.Mercs;

        // ── 탭 버튼 (활성 표시용) ─────────────────────────────────────
        private Button tabMercsBtn;
        private Button tabCraftBtn;
        private Button tabInvBtn;

        // ── 상세 오버레이 (용병 선택 시) ──────────────────────────────
        private GameObject detailOverlay;
        private Transform detailScrollRoot;
        private string selectedMercenaryId;
        private bool inventorySortByGrade = true;

        // ── 타이머 (제작/승급 갱신) ───────────────────────────────────
        private float timerElapsed;
        private const float TimerInterval = 1f;

        private void Start()
        {
            BuildUI();
            SwitchTab(Tab.Mercs);
        }

        private void Update()
        {
            timerElapsed += Time.deltaTime;
            if (timerElapsed < TimerInterval) return;
            timerElapsed = 0f;

            // 크레딧 헤더 갱신
            headerCreditsText.text = $"{PlayerAccountService.Credits}C";

            // 오버레이 열려있고 승급 중이면 갱신
            if (detailOverlay != null && detailOverlay.activeSelf &&
                !string.IsNullOrEmpty(selectedMercenaryId) &&
                PlayerAccountService.IsPromoting(selectedMercenaryId))
            {
                PopulateDetailOverlay(selectedMercenaryId);
                return;
            }

            // 제작 탭이면 타이머 갱신
            if (currentTab == Tab.Craft)
            {
                RefreshCurrentTab();
            }
        }

        // ── UI 빌드 ──────────────────────────────────────────────────

        private void BuildUI()
        {
            var t = UITheme.Instance;
            var canvas = UIFactory.CreateCanvas("OfficeCanvas");
            var root = UIFactory.CreateFullPanel(canvas.transform);

            // ── 상단 바 ──────────────────────────────────────────────
            var topBar = UIFactory.CreateTopBar(root);

            UIFactory.CreateBarText(topBar, "사무소", TextRole.Heading,
                TextAnchor.MiddleLeft, left: UIFactory.HorizPad, right: 700f);

            headerLevelText = UIFactory.CreateBarText(topBar, string.Empty, TextRole.Caption,
                TextAnchor.MiddleCenter, left: 200f, right: 300f);

            headerCreditsText = UIFactory.CreateBarText(topBar, "0C", TextRole.Caption,
                TextAnchor.MiddleRight, left: 700f, right: 160f);

            UIFactory.CreateButton(topBar, "↑",
                new Vector2(-(UIFactory.HorizPad / 2f + 50f), 0f),
                new Vector2(110f, 64f),
                OnUpgradeOffice, ButtonVariant.Primary);

            // ── 탭 바 ────────────────────────────────────────────────
            var tabBar = UIFactory.CreateTabBar(root);
            var tabRow = UIFactory.CreateHorizontalRow(tabBar, spacing: 2f);
            tabMercsBtn = UIFactory.CreateNavButton(tabRow, "용병", () => SwitchTab(Tab.Mercs), ButtonVariant.Muted);
            tabCraftBtn = UIFactory.CreateNavButton(tabRow, "제작", () => SwitchTab(Tab.Craft), ButtonVariant.Muted);
            tabInvBtn   = UIFactory.CreateNavButton(tabRow, "인벤토리", () => SwitchTab(Tab.Inventory), ButtonVariant.Muted);

            // ── 콘텐츠 영역 ──────────────────────────────────────────
            var contentArea = UIFactory.CreateContentArea(root,
                topInset: UIFactory.TopBarH + UIFactory.TabBarH,
                bottomInset: UIFactory.BottomNavH);
            contentRoot = UIFactory.CreateScrollList(contentArea, spacing: 8f);

            // ── 하단 내비 ────────────────────────────────────────────
            var bottomBar = UIFactory.CreateBottomBar(root);
            var navRow = UIFactory.CreateHorizontalRow(bottomBar, spacing: 4f);
            UIFactory.CreateNavButton(navRow, "모집", () => SceneNavigator.TryLoad("Recruit"), ButtonVariant.Nav);
            UIFactory.CreateNavButton(navRow, "현장", () => SceneNavigator.TryLoad("Dungeon"), ButtonVariant.Nav);
            UIFactory.CreateNavButton(navRow, "새로고침", RefreshCurrentTab, ButtonVariant.Muted);

            // ── 상세 오버레이 ─────────────────────────────────────────
            detailOverlay = BuildDetailOverlay(root);
            detailOverlay.SetActive(false);

            RefreshHeader();
        }

        private void RefreshHeader()
        {
            headerCreditsText.text = $"{PlayerAccountService.Credits}C";

            if (!GameCsvTables.TryLoad(out var tables, out _)) return;
            var lv = PlayerAccountService.GetOfficeLevel();
            tables.TryGetOfficeLevelRow(lv, out var lvRow);
            var maxLv = tables.GetMaxOfficeLevel();
            headerLevelText.text = lvRow.IsMaxLevel
                ? $"Lv.{lv} MAX"
                : $"Lv.{lv}/{maxLv}  ↑{lvRow.UpgradeCostCredits}C";
        }

        // ── 탭 전환 ──────────────────────────────────────────────────

        private void SwitchTab(Tab tab)
        {
            currentTab = tab;
            UpdateTabButtons();
            RefreshCurrentTab();
        }

        private void UpdateTabButtons()
        {
            var t = UITheme.Instance;
            SetTabColor(tabMercsBtn, currentTab == Tab.Mercs);
            SetTabColor(tabCraftBtn, currentTab == Tab.Craft);
            SetTabColor(tabInvBtn,   currentTab == Tab.Inventory);
        }

        private static void SetTabColor(Button btn, bool active)
        {
            if (btn == null) return;
            btn.GetComponent<Image>().color = active
                ? UITheme.Instance.primary
                : UITheme.Instance.muted;
        }

        private void RefreshCurrentTab()
        {
            UIFactory.ClearChildren(contentRoot);
            switch (currentTab)
            {
                case Tab.Mercs:     PopulateMercsTab();     break;
                case Tab.Craft:     PopulateCraftTab();     break;
                case Tab.Inventory: PopulateInventoryTab(); break;
            }

            if (contentRoot is RectTransform rt)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            RefreshHeader();
        }

        // ── 용병 탭 ──────────────────────────────────────────────────

        private void PopulateMercsTab()
        {
            var owned = PlayerAccountService.GetOwnedMercenaries();
            if (owned.Count == 0)
            {
                UIFactory.CreateListItem(contentRoot, "보유 용병 없음\n모집 탭에서 용병을 고용하세요.",
                    92f, UITheme.Instance.muted);
                return;
            }

            if (!GameCsvTables.TryLoad(out var tables, out _)) return;
            TalentCatalog.TryLoad(out var talents, out _);

            for (var i = 0; i < owned.Count; i++)
            {
                var record = owned[i];
                tables.TryGetCharacterRow(record.templateId, out var charRow);
                var name    = string.IsNullOrWhiteSpace(charRow.Name) ? record.templateId : charRow.Name;
                var talent  = talents != null ? talents.GetTalentName(record.talentTag) : record.talentTag;
                var promo   = PlayerAccountService.IsPromoting(record.mercenaryId) ? "  [승급중]" : string.Empty;
                var label   = $"#{i + 1}  {name}  Lv.{record.level}  G{charRow.Grade}{promo}\n{talent}";

                var bgColor = PlayerAccountService.IsPromoting(record.mercenaryId)
                    ? UITheme.Instance.warning
                    : UITheme.Instance.surfaceRaised;

                var mercId = record.mercenaryId;
                UIFactory.CreateListItem(contentRoot, label, 100f, bgColor,
                    () => OpenDetailOverlay(mercId));
            }
        }

        // ── 제작 탭 ──────────────────────────────────────────────────

        private void PopulateCraftTab()
        {
            if (!GameCsvTables.TryLoad(out var tables, out _)) return;

            // 재료 요약
            var matSummary = BuildMaterialSummary(tables);
            UIFactory.CreateActionItem(contentRoot, matSummary, 80f, UITheme.Instance.surface);

            // 크레딧
            UIFactory.CreateActionItem(contentRoot,
                $"보유 크레딧: {PlayerAccountService.Credits}C", 60f, UITheme.Instance.surface);

            // 레시피 목록
            UIFactory.CreateActionItem(contentRoot, "── 제작 가능 ──", 50f, UITheme.Instance.surfaceBorder);
            var recipes = tables.GetRecipes()
                .Where(x => PlayerAccountService.IsSupportedEquipType(x.ResultEquipType))
                .ToList();

            if (recipes.Count == 0)
            {
                UIFactory.CreateActionItem(contentRoot, "레시피 없음", 72f, UITheme.Instance.muted);
            }
            else
            {
                foreach (var recipe in recipes)
                {
                    var canStart = CraftingService.CanStartCraft(recipe, out _);
                    var label    = BuildRecipeLabel(recipe, tables);
                    var color    = canStart ? UITheme.Instance.success : UITheme.Instance.muted;
                    UIFactory.CreateActionItem(contentRoot, label, 120f, color,
                        canStart ? (Action)(() => OnStartCraft(recipe.RecipeId)) : null);
                }
            }

            // 진행 중 작업
            var jobs = CraftingService.GetJobs();
            if (jobs.Count > 0)
            {
                UIFactory.CreateActionItem(contentRoot, "── 진행 중 ──", 50f, UITheme.Instance.surfaceBorder);
                foreach (var job in jobs)
                {
                    var remaining = CraftingService.GetRemainingSeconds(job.jobId);
                    var recipe    = tables.GetRecipes().FirstOrDefault(x => x.RecipeId == job.recipeId);
                    var resultStr = string.IsNullOrWhiteSpace(recipe.RecipeId)
                        ? job.recipeId
                        : $"{recipe.ResultEquipType} G{recipe.ResultGrade} +{recipe.StatValue}";
                    var label = remaining > 0
                        ? $"{resultStr}\n{remaining / 60:D2}:{remaining % 60:D2} 남음"
                        : $"{resultStr}\n[ 수령하기 ]";
                    var color = remaining > 0 ? UITheme.Instance.warning : UITheme.Instance.success;
                    var jobId = job.jobId;
                    UIFactory.CreateActionItem(contentRoot, label, 100f, color,
                        remaining > 0 ? null : (Action)(() => OnCompleteCraft(jobId)));
                }
            }
        }

        // ── 인벤토리 탭 ──────────────────────────────────────────────

        private void PopulateInventoryTab()
        {
            var sortLabel = inventorySortByGrade ? "정렬: 등급" : "정렬: 타입";
            UIFactory.CreateActionItem(contentRoot, sortLabel, 64f, UITheme.Instance.surfaceBorder,
                ToggleInventorySort);

            var all = InventoryService.GetAllEquipments();
            if (all.Count == 0)
            {
                UIFactory.CreateListItem(contentRoot, "보유 장비 없음", 92f, UITheme.Instance.muted);
                return;
            }

            GameCsvTables.TryLoad(out var tables, out _);

            IEnumerable<InventoryService.EquipmentRecord> sorted = inventorySortByGrade
                ? all.OrderByDescending(x => x.grade).ThenBy(x => x.equipType).ThenByDescending(x => x.statValue)
                : all.OrderBy(x => x.equipType).ThenByDescending(x => x.grade).ThenByDescending(x => x.statValue);

            foreach (var eq in sorted)
            {
                var ownerLabel = BuildOwnerLabel(eq.ownerMercenaryId, tables);
                var equippedMark = string.IsNullOrWhiteSpace(eq.ownerMercenaryId) ? "" : " [E]";
                var label  = $"[{eq.equipType}] {FormatEquipLabel(eq)}{equippedMark}\n{ownerLabel}";
                var tint   = Color.Lerp(UITheme.Instance.surface, UIFactory.GradeColor(eq.grade), 0.4f);
                UIFactory.CreateActionItem(contentRoot, label, 90f, tint);
            }
        }

        private void ToggleInventorySort()
        {
            inventorySortByGrade = !inventorySortByGrade;
            RefreshCurrentTab();
        }

        // ── 상세 오버레이 ─────────────────────────────────────────────

        private GameObject BuildDetailOverlay(Transform root)
        {
            var t = UITheme.Instance;

            var overlay = new GameObject("DetailOverlay");
            overlay.transform.SetParent(root, false);
            overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.92f);
            var overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            // 닫기 버튼 (우상단)
            var closeBtn = new GameObject("CloseBtn");
            closeBtn.transform.SetParent(overlay.transform, false);
            closeBtn.AddComponent<Image>().color = t.danger;
            var closeBtnComp = closeBtn.AddComponent<Button>();
            closeBtnComp.onClick.AddListener(() => overlay.SetActive(false));
            var closeBtnRect = closeBtn.GetComponent<RectTransform>();
            closeBtnRect.anchorMin = new Vector2(1f, 1f);
            closeBtnRect.anchorMax = new Vector2(1f, 1f);
            closeBtnRect.pivot     = new Vector2(1f, 1f);
            closeBtnRect.sizeDelta = new Vector2(120f, 80f);
            closeBtnRect.anchoredPosition = Vector2.zero;
            var closeTxt = new GameObject("Text");
            closeTxt.transform.SetParent(closeBtn.transform, false);
            var closeTxtComp = closeTxt.AddComponent<Text>();
            closeTxtComp.text = "닫기";
            closeTxtComp.font = t.GetFont();
            closeTxtComp.fontSize = t.fontSizeBody;
            closeTxtComp.alignment = TextAnchor.MiddleCenter;
            closeTxtComp.color = t.textPrimary;
            var closeTxtRect = closeTxt.GetComponent<RectTransform>();
            closeTxtRect.anchorMin = Vector2.zero;
            closeTxtRect.anchorMax = Vector2.one;
            closeTxtRect.offsetMin = Vector2.zero;
            closeTxtRect.offsetMax = Vector2.zero;

            // 스크롤 콘텐츠
            var contentArea = new GameObject("ContentArea");
            contentArea.transform.SetParent(overlay.transform, false);
            var contentRect = contentArea.AddComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = new Vector2(-120f, -80f); // 닫기 버튼 피하기

            detailScrollRoot = UIFactory.CreateScrollList(contentArea.transform, spacing: 8f);
            return overlay;
        }

        private void OpenDetailOverlay(string mercenaryId)
        {
            selectedMercenaryId = mercenaryId;
            detailOverlay.SetActive(true);
            PopulateDetailOverlay(mercenaryId);
        }

        private void PopulateDetailOverlay(string mercenaryId)
        {
            UIFactory.ClearChildren(detailScrollRoot);

            if (!GameCsvTables.TryLoad(out var tables, out _)) return;
            TalentCatalog.TryLoad(out var talents, out _);

            var record = PlayerAccountService.GetRecordByMercenaryId(mercenaryId);
            if (record == null) return;

            tables.TryGetCharacterRow(record.templateId, out var charRow);
            var name = string.IsNullOrWhiteSpace(charRow.Name) ? record.templateId : charRow.Name;

            // 이름 / 등급 / 레벨
            var expToNext = tables.GetExpToNextLevel(record.level);
            UIFactory.CreateActionItem(detailScrollRoot,
                $"{name}  G{charRow.Grade}  Lv.{record.level}\nEXP {record.exp} / {expToNext}",
                110f, UITheme.Instance.surface);

            // 재능
            var talentName = talents != null ? talents.GetTalentName(record.talentTag) : record.talentTag;
            var talentDesc = talents != null ? talents.GetTalentDescription(record.talentTag) : string.Empty;
            UIFactory.CreateActionItem(detailScrollRoot,
                $"재능: {talentName}\n{talentDesc}",
                100f, UITheme.Instance.surfaceRaised);

            // 스탯
            if (tables.TryGetCombatUnit(record.templateId, out var unitRow))
            {
                UIFactory.CreateActionItem(detailScrollRoot,
                    $"HP {unitRow.MaxHp}  MP {unitRow.MaxMana}\n" +
                    $"ATK {unitRow.Attack}  DEF {unitRow.Defense}  AGI {unitRow.Agility}\n" +
                    $"{unitRow.DamageType} / {unitRow.AttackRangeType}",
                    120f, UITheme.Instance.surfaceRaised);
            }

            // ── 장비 슬롯 ──────────────────────────────────────────
            UIFactory.CreateActionItem(detailScrollRoot, "── 장비 ──", 50f, UITheme.Instance.surfaceBorder);
            BuildEquipSection(record, "weapon",    "무기");
            BuildEquipSection(record, "armor",     "방어구");
            BuildEquipSection(record, "accessory", "장신구");
            BuildEquipSection(record, "extra",     "특수");

            // ── 승급 ───────────────────────────────────────────────
            UIFactory.CreateActionItem(detailScrollRoot, "── 승급 ──", 50f, UITheme.Instance.surfaceBorder);
            BuildPromotionSection(record, charRow, tables);
        }

        private void BuildEquipSection(PlayerAccountService.MercenaryRecord record,
            string equipType, string label)
        {
            var equippedId = PlayerAccountService.GetEquippedItemId(record.mercenaryId, equipType);

            if (!string.IsNullOrWhiteSpace(equippedId) &&
                InventoryService.TryGetEquipment(equippedId, out var equipped))
            {
                UIFactory.CreateActionItem(detailScrollRoot,
                    $"{label}: {FormatEquipLabel(equipped)}  [해제]",
                    80f, UITheme.Instance.danger,
                    () => { InventoryService.TryUnequip(equipped.equipmentId, out _); PopulateDetailOverlay(record.mercenaryId); });
            }
            else
            {
                UIFactory.CreateActionItem(detailScrollRoot,
                    $"{label}: (빈 슬롯)", 72f, UITheme.Instance.surface);
            }

            foreach (var eq in InventoryService.GetUnequippedEquipments(equipType).Take(3))
            {
                var tint = Color.Lerp(UITheme.Instance.surface, UIFactory.GradeColor(eq.grade), 0.45f);
                var eqId = eq.equipmentId;
                var mercId = record.mercenaryId;
                UIFactory.CreateActionItem(detailScrollRoot,
                    $"장착: {FormatEquipLabel(eq)}",
                    80f, tint,
                    () => { InventoryService.TryEquip(eqId, mercId, out _); PopulateDetailOverlay(mercId); });
            }
        }

        private void BuildPromotionSection(PlayerAccountService.MercenaryRecord record,
            CharacterRow charRow, GameCsvTables tables)
        {
            if (PlayerAccountService.IsPromoting(record.mercenaryId))
            {
                var remaining = PlayerAccountService.GetPromotionRemainingSeconds(record.mercenaryId);
                if (remaining > 0)
                {
                    UIFactory.CreateActionItem(detailScrollRoot,
                        $"승급 진행중: {remaining / 60:D2}:{remaining % 60:D2}", 72f, UITheme.Instance.warning);
                }
                else
                {
                    UIFactory.CreateActionItem(detailScrollRoot, "승급 완료 — 수령하기", 80f,
                        UITheme.Instance.success,
                        () => { PlayerAccountService.TryCompletePromotion(record.mercenaryId, out _);
                                PopulateDetailOverlay(record.mercenaryId); });
                }

                return;
            }

            BuildRouteButton(record, charRow, tables, "A", charRow.PromotionRouteA);
            BuildRouteButton(record, charRow, tables, "B", charRow.PromotionRouteB);

            if (string.IsNullOrEmpty(charRow.PromotionRouteA) &&
                string.IsNullOrEmpty(charRow.PromotionRouteB))
            {
                UIFactory.CreateActionItem(detailScrollRoot, "(승급 루트 없음)", 60f, UITheme.Instance.muted);
            }
        }

        private void BuildRouteButton(PlayerAccountService.MercenaryRecord record,
            CharacterRow charRow, GameCsvTables tables, string route, string targetId)
        {
            if (string.IsNullOrEmpty(targetId) ||
                !tables.TryGetPromotionRule(charRow.Grade, route, out var rule)) return;

            tables.TryGetCharacterRow(targetId, out var targetChar);
            var targetName = string.IsNullOrWhiteSpace(targetChar.Name) ? targetId : targetChar.Name;
            var canLevel   = record.level >= rule.RequiredLevel;
            var canAfford  = PlayerAccountService.Credits >= rule.CostCredits;
            var canPromote = canLevel && canAfford;
            var label = $"루트 {route}: {targetName}\n" +
                        $"Lv.{rule.RequiredLevel}+  {rule.CostCredits}C  " +
                        $"{rule.TimeSeconds / 60:D2}:{rule.TimeSeconds % 60:D2}" +
                        (!canLevel ? "  [레벨 부족]" : "") +
                        (!canAfford ? "  [크레딧 부족]" : "");
            var color = canPromote ? UITheme.Instance.primary : UITheme.Instance.muted;
            var mercId = record.mercenaryId;
            UIFactory.CreateActionItem(detailScrollRoot, label, 100f, color,
                canPromote ? (Action)(() =>
                {
                    PlayerAccountService.StartPromotion(mercId, targetId, rule.TimeSeconds, rule.CostCredits);
                    PopulateDetailOverlay(mercId);
                }) : null);
        }

        // ── 이벤트 핸들러 ────────────────────────────────────────────

        private void OnUpgradeOffice()
        {
            if (!GameCsvTables.TryLoad(out var tables, out _)) return;
            PlayerAccountService.TryUpgradeOffice(tables, out _);
            RefreshHeader();
        }

        private void OnStartCraft(string recipeId)
        {
            if (!GameCsvTables.TryLoad(out var tables, out _)) return;
            var recipe = tables.GetRecipes().FirstOrDefault(x => x.RecipeId == recipeId);
            if (string.IsNullOrWhiteSpace(recipe.RecipeId)) return;
            CraftingService.TryStartCraft(recipe, out _);
            RefreshCurrentTab();
        }

        private void OnCompleteCraft(string jobId)
        {
            if (!GameCsvTables.TryLoad(out var tables, out _)) return;
            CraftingService.TryCompleteCraft(jobId, tables, out _, out _);
            RefreshCurrentTab();
        }

        // ── 포맷 헬퍼 ────────────────────────────────────────────────

        private static string FormatEquipLabel(InventoryService.EquipmentRecord eq)
        {
            if (eq == null) return "(null)";
            var tier = eq.grade switch
            {
                1 => "Normal", 2 => "Rare", 3 => "Elite",
                4 => "Epic",   5 => "Legend", _ => $"G{eq.grade}"
            };
            return $"[{tier}] {eq.itemName} +{eq.statValue}";
        }

        private static string BuildOwnerLabel(string ownerMercenaryId, GameCsvTables tables)
        {
            if (string.IsNullOrWhiteSpace(ownerMercenaryId)) return "(미장착)";
            if (tables == null) return $"착용: {ownerMercenaryId}";
            var record = PlayerAccountService.GetRecordByMercenaryId(ownerMercenaryId);
            if (record == null) return $"착용: {ownerMercenaryId}";
            tables.TryGetCharacterRow(record.templateId, out var charRow);
            var name = string.IsNullOrWhiteSpace(charRow.Name) ? record.templateId : charRow.Name;
            return $"착용: {name} Lv.{record.level}";
        }

        private static string BuildMaterialSummary(GameCsvTables tables)
        {
            var mats = InventoryService.GetAllMaterials();
            if (mats.Count == 0) return "재료: 없음";
            var top = mats.OrderByDescending(x => x.amount).Take(5)
                .Select(x => $"{tables.GetItemName(x.itemId)}: {x.amount}");
            return "재료: " + string.Join("  ", top);
        }

        private static string BuildRecipeLabel(RecipeRow recipe, GameCsvTables tables)
        {
            var header = $"{recipe.ResultEquipType} G{recipe.ResultGrade} +{recipe.StatValue}" +
                         $"  {recipe.CostCredits}C  {recipe.CraftSeconds}s";
            var mats = recipe.GetMaterialRequirements();
            if (mats.Count == 0) return header;
            var parts = mats.Select(x =>
            {
                var name = tables.GetItemName(x.ItemId);
                var have = InventoryService.GetMaterialAmount(x.ItemId);
                return $"{(have >= x.Amount ? "✓" : "✗")}{name} x{x.Amount}({have})";
            });
            return $"{header}\n{string.Join("  ", parts)}";
        }
    }
}
