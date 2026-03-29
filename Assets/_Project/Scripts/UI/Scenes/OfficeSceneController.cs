using System;
using System.Collections.Generic;
using System.Linq;
using ProjectH.Account;
using ProjectH.Core;
using ProjectH.Data.Tables;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ProjectH.UI;

namespace ProjectH.UI.Scenes
{
    public sealed class OfficeSceneController : MonoBehaviour
    {
        private enum Tab { Mercs, Craft, Inventory }

        // ── 공통 UI ──────────────────────────────────────────────────
        [SerializeField] private TMP_Text headerCreditsText;
        [SerializeField] private TMP_Text headerLevelText;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private Button upgradeBtn;

        // ── 탭 버튼 ───────────────────────────────────────────────────
        [SerializeField] private Button tabMercsBtn;
        [SerializeField] private Button tabCraftBtn;
        [SerializeField] private Button tabInvBtn;

        // ── 상세 오버레이 ──────────────────────────────────────────────
        [SerializeField] private GameObject detailOverlay;
        [SerializeField] private Transform detailScrollRoot;
        [SerializeField] private Button detailCloseBtn;

        // ── 하단 내비 ─────────────────────────────────────────────────
        [SerializeField] private Button recruitNavBtn;
        [SerializeField] private Button dungeonNavBtn;
        [SerializeField] private Button refreshNavBtn;

        // ── 프리팹 ────────────────────────────────────────────────────
        [SerializeField] private ListItemBinding listItemCardPrefab;
        [SerializeField] private ActionCardBinding actionCardPrefab;

        private Tab currentTab = Tab.Mercs;
        private string selectedMercenaryId;
        private bool inventorySortByGrade = true;

        private float timerElapsed;
        private const float TimerInterval = 1f;

        private void Start()
        {
            tabMercsBtn.onClick.AddListener(() => SwitchTab(Tab.Mercs));
            tabCraftBtn.onClick.AddListener(() => SwitchTab(Tab.Craft));
            tabInvBtn.onClick.AddListener(() => SwitchTab(Tab.Inventory));
            detailCloseBtn.onClick.AddListener(() => detailOverlay.SetActive(false));
            upgradeBtn.onClick.AddListener(OnUpgradeOffice);
            recruitNavBtn.onClick.AddListener(() => SceneNavigator.TryLoad("Recruit"));
            dungeonNavBtn.onClick.AddListener(() => SceneNavigator.TryLoad("Dungeon"));
            refreshNavBtn.onClick.AddListener(RefreshCurrentTab);
            SwitchTab(Tab.Mercs);
        }

        private void Update()
        {
            timerElapsed += Time.deltaTime;
            if (timerElapsed < TimerInterval) return;
            timerElapsed = 0f;

            headerCreditsText.text = $"{PlayerAccountService.Credits}C";

            if (detailOverlay != null && detailOverlay.activeSelf &&
                !string.IsNullOrEmpty(selectedMercenaryId) &&
                PlayerAccountService.IsPromoting(selectedMercenaryId))
            {
                PopulateDetailOverlay(selectedMercenaryId);
                return;
            }

            if (currentTab == Tab.Craft)
            {
                RefreshCurrentTab();
            }
        }

        // ── 헤더 갱신 ────────────────────────────────────────────────

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
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            RefreshHeader();
        }

        // ── 용병 탭 ──────────────────────────────────────────────────

        private void PopulateMercsTab()
        {
            var owned = PlayerAccountService.GetOwnedMercenaries();
            if (owned.Count == 0)
            {
                Instantiate(listItemCardPrefab, contentRoot)
                    .Set("보유 용병 없음\n모집 탭에서 용병을 고용하세요.", UITheme.Instance.muted, null, 92f);
                return;
            }

            if (!GameCsvTables.TryLoad(out var tables, out _)) return;
            TalentCatalog.TryLoad(out var talents, out _);

            for (var i = 0; i < owned.Count; i++)
            {
                var record = owned[i];
                tables.TryGetCharacterRow(record.templateId, out var charRow);
                var name   = string.IsNullOrWhiteSpace(charRow.Name) ? record.templateId : charRow.Name;
                var talent = talents != null ? talents.GetTalentName(record.talentTag) : record.talentTag;
                var promo  = PlayerAccountService.IsPromoting(record.mercenaryId) ? "  [승급중]" : string.Empty;
                var label  = $"#{i + 1}  {name}  Lv.{record.level}  G{charRow.Grade}{promo}\n{talent}";
                var bgColor = PlayerAccountService.IsPromoting(record.mercenaryId)
                    ? UITheme.Instance.warning
                    : UITheme.Instance.surfaceRaised;
                var mercId = record.mercenaryId;
                Instantiate(listItemCardPrefab, contentRoot)
                    .Set(label, bgColor, () => OpenDetailOverlay(mercId), 100f);
            }
        }

        // ── 제작 탭 ──────────────────────────────────────────────────

        private void PopulateCraftTab()
        {
            if (!GameCsvTables.TryLoad(out var tables, out _)) return;

            Instantiate(actionCardPrefab, contentRoot)
                .Set(BuildMaterialSummary(tables), UITheme.Instance.surface, null, 80f);
            Instantiate(actionCardPrefab, contentRoot)
                .Set($"보유 크레딧: {PlayerAccountService.Credits}C", UITheme.Instance.surface, null, 60f);

            Instantiate(actionCardPrefab, contentRoot)
                .Set("── 제작 가능 ──", UITheme.Instance.surfaceBorder, null, 50f);

            var recipes = tables.GetRecipes()
                .Where(x => PlayerAccountService.IsSupportedEquipType(x.ResultEquipType))
                .ToList();

            if (recipes.Count == 0)
            {
                Instantiate(actionCardPrefab, contentRoot)
                    .Set("레시피 없음", UITheme.Instance.muted, null, 72f);
            }
            else
            {
                foreach (var recipe in recipes)
                {
                    var canStart = CraftingService.CanStartCraft(recipe, out _);
                    var label    = BuildRecipeLabel(recipe, tables);
                    var color    = canStart ? UITheme.Instance.success : UITheme.Instance.muted;
                    var r        = recipe;
                    Instantiate(actionCardPrefab, contentRoot)
                        .Set(label, color, canStart ? (Action)(() => OnStartCraft(r.RecipeId)) : null, 120f);
                }
            }

            var jobs = CraftingService.GetJobs();
            if (jobs.Count > 0)
            {
                Instantiate(actionCardPrefab, contentRoot)
                    .Set("── 진행 중 ──", UITheme.Instance.surfaceBorder, null, 50f);
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
                    Instantiate(actionCardPrefab, contentRoot)
                        .Set(label, color,
                            remaining > 0 ? null : (Action)(() => OnCompleteCraft(jobId)), 100f);
                }
            }
        }

        // ── 인벤토리 탭 ──────────────────────────────────────────────

        private void PopulateInventoryTab()
        {
            var sortLabel = inventorySortByGrade ? "정렬: 등급" : "정렬: 타입";
            Instantiate(actionCardPrefab, contentRoot)
                .Set(sortLabel, UITheme.Instance.surfaceBorder, ToggleInventorySort, 64f);

            var all = InventoryService.GetAllEquipments();
            if (all.Count == 0)
            {
                Instantiate(listItemCardPrefab, contentRoot)
                    .Set("보유 장비 없음", UITheme.Instance.muted, null, 92f);
                return;
            }

            GameCsvTables.TryLoad(out var tables, out _);

            IEnumerable<InventoryService.EquipmentRecord> sorted = inventorySortByGrade
                ? all.OrderByDescending(x => x.grade).ThenBy(x => x.equipType).ThenByDescending(x => x.statValue)
                : all.OrderBy(x => x.equipType).ThenByDescending(x => x.grade).ThenByDescending(x => x.statValue);

            foreach (var eq in sorted)
            {
                var ownerLabel   = BuildOwnerLabel(eq.ownerMercenaryId, tables);
                var equippedMark = string.IsNullOrWhiteSpace(eq.ownerMercenaryId) ? "" : " [E]";
                var label        = $"[{eq.equipType}] {FormatEquipLabel(eq)}{equippedMark}\n{ownerLabel}";
                var tint         = Color.Lerp(UITheme.Instance.surface, UIFactory.GradeColor(eq.grade), 0.4f);
                Instantiate(actionCardPrefab, contentRoot).Set(label, tint, null, 90f);
            }
        }

        private void ToggleInventorySort()
        {
            inventorySortByGrade = !inventorySortByGrade;
            RefreshCurrentTab();
        }

        // ── 상세 오버레이 ─────────────────────────────────────────────

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

            var expToNext = tables.GetExpToNextLevel(record.level);
            Instantiate(actionCardPrefab, detailScrollRoot)
                .Set($"{name}  G{charRow.Grade}  Lv.{record.level}\nEXP {record.exp} / {expToNext}",
                    UITheme.Instance.surface, null, 110f);

            var talentName = talents != null ? talents.GetTalentName(record.talentTag) : record.talentTag;
            var talentDesc = talents != null ? talents.GetTalentDescription(record.talentTag) : string.Empty;
            Instantiate(actionCardPrefab, detailScrollRoot)
                .Set($"재능: {talentName}\n{talentDesc}", UITheme.Instance.surfaceRaised, null, 100f);

            if (tables.TryGetCombatUnit(record.templateId, out var unitRow))
            {
                Instantiate(actionCardPrefab, detailScrollRoot)
                    .Set($"HP {unitRow.MaxHp}  MP {unitRow.MaxMana}\n" +
                         $"ATK {unitRow.Attack}  DEF {unitRow.Defense}  AGI {unitRow.Agility}\n" +
                         $"{unitRow.DamageType} / {unitRow.AttackRangeType}",
                        UITheme.Instance.surfaceRaised, null, 120f);
            }

            Instantiate(actionCardPrefab, detailScrollRoot)
                .Set("── 장비 ──", UITheme.Instance.surfaceBorder, null, 50f);
            BuildEquipSection(record, "weapon",    "무기");
            BuildEquipSection(record, "armor",     "방어구");
            BuildEquipSection(record, "accessory", "장신구");
            BuildEquipSection(record, "extra",     "특수");

            Instantiate(actionCardPrefab, detailScrollRoot)
                .Set("── 승급 ──", UITheme.Instance.surfaceBorder, null, 50f);
            BuildPromotionSection(record, charRow, tables);

            if (detailScrollRoot is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        private void BuildEquipSection(PlayerAccountService.MercenaryRecord record,
            string equipType, string label)
        {
            var equippedId = PlayerAccountService.GetEquippedItemId(record.mercenaryId, equipType);

            if (!string.IsNullOrWhiteSpace(equippedId) &&
                InventoryService.TryGetEquipment(equippedId, out var equipped))
            {
                Instantiate(actionCardPrefab, detailScrollRoot)
                    .Set($"{label}: {FormatEquipLabel(equipped)}  [해제]",
                        UITheme.Instance.danger,
                        () =>
                        {
                            InventoryService.TryUnequip(equipped.equipmentId, out _);
                            PopulateDetailOverlay(record.mercenaryId);
                        }, 80f);
            }
            else
            {
                Instantiate(actionCardPrefab, detailScrollRoot)
                    .Set($"{label}: (빈 슬롯)", UITheme.Instance.surface, null, 72f);
            }

            foreach (var eq in InventoryService.GetUnequippedEquipments(equipType).Take(3))
            {
                var tint  = Color.Lerp(UITheme.Instance.surface, UIFactory.GradeColor(eq.grade), 0.45f);
                var eqId  = eq.equipmentId;
                var mercId = record.mercenaryId;
                Instantiate(actionCardPrefab, detailScrollRoot)
                    .Set($"장착: {FormatEquipLabel(eq)}", tint,
                        () =>
                        {
                            InventoryService.TryEquip(eqId, mercId, out _);
                            PopulateDetailOverlay(mercId);
                        }, 80f);
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
                    Instantiate(actionCardPrefab, detailScrollRoot)
                        .Set($"승급 진행중: {remaining / 60:D2}:{remaining % 60:D2}",
                            UITheme.Instance.warning, null, 72f);
                }
                else
                {
                    Instantiate(actionCardPrefab, detailScrollRoot)
                        .Set("승급 완료 — 수령하기", UITheme.Instance.success,
                            () =>
                            {
                                PlayerAccountService.TryCompletePromotion(record.mercenaryId, out _);
                                PopulateDetailOverlay(record.mercenaryId);
                            }, 80f);
                }
                return;
            }

            BuildRouteButton(record, charRow, tables, "A", charRow.PromotionRouteA);
            BuildRouteButton(record, charRow, tables, "B", charRow.PromotionRouteB);

            if (string.IsNullOrEmpty(charRow.PromotionRouteA) &&
                string.IsNullOrEmpty(charRow.PromotionRouteB))
            {
                Instantiate(actionCardPrefab, detailScrollRoot)
                    .Set("(승급 루트 없음)", UITheme.Instance.muted, null, 60f);
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
            var color  = canPromote ? UITheme.Instance.primary : UITheme.Instance.muted;
            var mercId = record.mercenaryId;
            Instantiate(actionCardPrefab, detailScrollRoot)
                .Set(label, color,
                    canPromote ? (Action)(() =>
                    {
                        PlayerAccountService.StartPromotion(mercId, targetId, rule.TimeSeconds, rule.CostCredits);
                        PopulateDetailOverlay(mercId);
                    }) : null, 100f);
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
