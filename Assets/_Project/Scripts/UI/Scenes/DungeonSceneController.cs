using System.Collections.Generic;
using System.Linq;
using ProjectH.Account;
using ProjectH.Battle;
using ProjectH.Core;
using ProjectH.Data.Tables;
using UnityEngine;
using UnityEngine.UI;
using ProjectH.UI;

namespace ProjectH.UI.Scenes
{
    public sealed class DungeonSceneController : MonoBehaviour
    {
        private Text statusText;
        private Transform fieldListRoot;
        private GameObject dispatchOverlay;
        private Transform partyListRoot;
        private Text overlayTitle;
        private Text overlayStatus;
        private string selectedLocationId;
        private readonly List<string> selectedAllies = new List<string>();
        private int maxPartySize = 4;

        private GameObject resultOverlay;
        private GameObject reportOverlay;
        private Transform canvasRoot;

        private void Start()
        {
            BattleSessionManager.EnsureExists();
            BuildUI();
            RefreshFields();
        }

        private void Update()
        {
            var manager = BattleSessionManager.Instance;
            if (manager == null) return;

            if ((resultOverlay == null || !resultOverlay.activeSelf) && manager.CompletedSessions.Count > 0)
            {
                ShowResult(manager.CompletedSessions[0]);
            }
        }

        private void BuildUI()
        {
            var t = UITheme.Instance;
            var canvas = UIFactory.CreateCanvas("DungeonCanvas");
            canvasRoot = canvas.transform;
            var root = UIFactory.CreateFullPanel(canvas.transform);

            var topBar = UIFactory.CreateTopBar(root);
            UIFactory.CreateBarText(topBar, "현장 선택", TextRole.Heading,
                TextAnchor.MiddleLeft, left: UIFactory.HorizPad, right: 400f);
            statusText = UIFactory.CreateBarText(topBar, string.Empty, TextRole.Caption,
                TextAnchor.MiddleRight, left: 500f, right: UIFactory.HorizPad);

            var contentArea = UIFactory.CreateContentArea(root);
            fieldListRoot = UIFactory.CreateScrollList(contentArea, spacing: 10f);

            var bottomBar = UIFactory.CreateBottomBar(root);
            var navRow = UIFactory.CreateHorizontalRow(bottomBar, spacing: 4f);
            UIFactory.CreateNavButton(navRow, "모집", () => SceneNavigator.TryLoad("Recruit"), ButtonVariant.Nav);
            UIFactory.CreateNavButton(navRow, "사무소", () => SceneNavigator.TryLoad("Office"), ButtonVariant.Nav);
            UIFactory.CreateNavButton(navRow, "새로고침", RefreshFields, ButtonVariant.Muted);

            dispatchOverlay = BuildDispatchOverlay(root);
            dispatchOverlay.SetActive(false);

            InvokeRepeating(nameof(PeriodicRefresh), 2f, 2f);
        }

        private void PeriodicRefresh()
        {
            if (dispatchOverlay != null && dispatchOverlay.activeSelf) return;
            if (resultOverlay != null && resultOverlay.activeSelf) return;
            if (reportOverlay != null && reportOverlay.activeSelf) return;
            RefreshFields();
        }

        private void RefreshFields()
        {
            UIFactory.ClearChildren(fieldListRoot);

            if (!GameCsvTables.TryLoad(out var tables, out _))
            {
                statusText.text = "로드 실패";
                return;
            }

            maxPartySize = Mathf.Max(1, tables.GetDefineInt("maxPartySize", 4));
            statusText.text = $"{PlayerAccountService.Credits}C";

            var open = tables.GetOpenLocations();
            if (open.Count == 0)
            {
                UIFactory.CreateListItem(fieldListRoot, "개방된 현장 없음", 92f, UITheme.Instance.muted);
                return;
            }

            var manager = BattleSessionManager.Instance;
            foreach (var loc in open)
            {
                var activeSession = manager?.GetActiveSession(loc.LocationId);
                if (activeSession != null)
                    BuildActiveSessionCard(loc, activeSession);
                else
                    UIFactory.CreateListItem(fieldListRoot,
                        $"{loc.Name}\n{loc.LocationId}", 110f,
                        UITheme.Instance.surfaceRaised,
                        () => OpenDispatch(loc.LocationId, loc.Name));
            }
        }

        // ── 활성 세션 카드 ──────────────────────────────────────────

        private void BuildActiveSessionCard(LocationRow loc, BattleSession session)
        {
            var t = UITheme.Instance;

            var cardGo = new GameObject("SessionCard");
            cardGo.transform.SetParent(fieldListRoot, false);
            cardGo.AddComponent<Image>().color = t.surfaceRaised;
            var cardBtn = cardGo.AddComponent<Button>();
            var locId = loc.LocationId;
            cardBtn.onClick.AddListener(() =>
            {
                var mgr = BattleSessionManager.Instance;
                if (mgr != null)
                {
                    mgr.ViewingLocationId = locId;
                    SceneNavigator.TryLoad("Battle");
                }
            });
            var le = cardGo.AddComponent<LayoutElement>();
            le.preferredHeight = 180f;

            // 현장명
            var nameGo = CreateStretchText(cardGo.transform, loc.Name, t.fontSizeHeading, t.textPrimary);
            var nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(0.78f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.offsetMin = new Vector2(UIFactory.HorizPad, -44f);
            nameRect.offsetMax = new Vector2(0f, -8f);

            // 캐릭터 아이콘 행
            var iconRow = CreateIconRow(cardGo.transform);
            var iconRowRect = iconRow.GetComponent<RectTransform>();
            iconRowRect.anchorMin = new Vector2(0f, 1f);
            iconRowRect.anchorMax = new Vector2(0.78f, 1f);
            iconRowRect.pivot = new Vector2(0f, 1f);
            iconRowRect.offsetMin = new Vector2(0f, -116f);
            iconRowRect.offsetMax = new Vector2(0f, -48f);

            foreach (var ally in session.AllyStates)
                BuildAllyIcon(iconRow.transform, ally);

            // 상태 텍스트
            var statusGo = CreateStretchText(cardGo.transform, session.StatusText, t.fontSizeCaption, t.textMuted);
            var statusRect = statusGo.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(0.7f, 0f);
            statusRect.pivot = new Vector2(0f, 0f);
            statusRect.offsetMin = new Vector2(UIFactory.HorizPad, 34f);
            statusRect.offsetMax = new Vector2(0f, 58f);

            // 프로그레스 바
            BuildProgressBar(cardGo.transform, session.RewardProgress);

            // 보고서 버튼
            var sessionRef = session;
            BuildReportButton(cardGo.transform, t, () => ShowReport(sessionRef));
        }

        private GameObject CreateIconRow(Transform parent)
        {
            var go = new GameObject("IconRow");
            go.transform.SetParent(parent, false);
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset((int)UIFactory.HorizPad, 0, 0, 0);
            go.AddComponent<RectTransform>();
            return go;
        }

        private void BuildAllyIcon(Transform parent, AllySessionState ally)
        {
            var iconGo = new GameObject("AllyIcon");
            iconGo.transform.SetParent(parent, false);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = new Color(0.25f, 0.25f, 0.3f, 1f);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(56f, 56f);

            // 캐릭터 아이콘 리소스 로드 시도
            if (!string.IsNullOrWhiteSpace(ally.iconResourcePath))
            {
                var sprite = Resources.Load<Sprite>(ally.iconResourcePath + "_icon");
                if (sprite != null) iconImg.sprite = sprite;
            }

            // HP 바
            var hpBgGo = new GameObject("HpBg");
            hpBgGo.transform.SetParent(iconGo.transform, false);
            hpBgGo.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);
            var hpBgRect = hpBgGo.GetComponent<RectTransform>();
            hpBgRect.anchorMin = new Vector2(0f, 0f);
            hpBgRect.anchorMax = new Vector2(1f, 0f);
            hpBgRect.pivot = new Vector2(0.5f, 1f);
            hpBgRect.offsetMin = new Vector2(2f, -8f);
            hpBgRect.offsetMax = new Vector2(-2f, 0f);

            var hpFillGo = new GameObject("HpFill");
            hpFillGo.transform.SetParent(hpBgGo.transform, false);
            var hpFill = hpFillGo.AddComponent<Image>();
            float hpRatio = ally.maxHp > 0 ? (float)ally.hp / ally.maxHp : 0f;
            hpFill.color = Color.Lerp(Color.red, Color.green, hpRatio);
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;
            hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            hpFill.fillAmount = hpRatio;
            hpFill.raycastTarget = false;
            var hpFillRect = hpFillGo.GetComponent<RectTransform>();
            hpFillRect.anchorMin = Vector2.zero;
            hpFillRect.anchorMax = Vector2.one;
            hpFillRect.offsetMin = Vector2.zero;
            hpFillRect.offsetMax = Vector2.zero;
        }

        private void BuildProgressBar(Transform parent, float progress)
        {
            var barBgGo = new GameObject("BarBG");
            barBgGo.transform.SetParent(parent, false);
            barBgGo.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);
            var barBgRect = barBgGo.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0f, 0f);
            barBgRect.anchorMax = new Vector2(0.78f, 0f);
            barBgRect.pivot = new Vector2(0f, 0f);
            barBgRect.offsetMin = new Vector2(UIFactory.HorizPad, 10f);
            barBgRect.offsetMax = new Vector2(-UIFactory.HorizPad, 30f);

            var barFillGo = new GameObject("BarFill");
            barFillGo.transform.SetParent(barBgGo.transform, false);
            var barFill = barFillGo.AddComponent<Image>();
            barFill.color = new Color(0.85f, 0.6f, 0.1f, 1f);
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            barFill.fillAmount = Mathf.Clamp01(progress);
            barFill.raycastTarget = false;
            var barFillRect = barFillGo.GetComponent<RectTransform>();
            barFillRect.anchorMin = Vector2.zero;
            barFillRect.anchorMax = Vector2.one;
            barFillRect.offsetMin = Vector2.zero;
            barFillRect.offsetMax = Vector2.zero;
        }

        private void BuildReportButton(Transform parent, UITheme t, System.Action onClick)
        {
            var btnGo = new GameObject("ReportBtn");
            btnGo.transform.SetParent(parent, false);
            btnGo.AddComponent<Image>().color = t.primary;
            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1f, 0f);
            btnRect.anchorMax = new Vector2(1f, 1f);
            btnRect.pivot = new Vector2(1f, 0.5f);
            btnRect.offsetMin = new Vector2(-80f, 16f);
            btnRect.offsetMax = new Vector2(-12f, -16f);

            var txtGo = CreateStretchText(btnGo.transform, "보고서", t.fontSizeCaption, t.textPrimary);
            var txtRect = txtGo.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            txtGo.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;
        }

        // ── 보고서 팝업 ────────────────────────────────────────────

        private void ShowReport(BattleSession session)
        {
            if (reportOverlay != null) Destroy(reportOverlay);

            var t = UITheme.Instance;
            reportOverlay = new GameObject("ReportOverlay");
            reportOverlay.transform.SetParent(canvasRoot, false);
            var bg = reportOverlay.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.85f);
            var btn = reportOverlay.AddComponent<Button>();
            btn.onClick.AddListener(() => { if (reportOverlay != null) Destroy(reportOverlay); });
            var bgRect = reportOverlay.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var card = new GameObject("ReportCard");
            card.transform.SetParent(reportOverlay.transform, false);
            card.AddComponent<Image>().color = t.surface;
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.03f, 0.15f);
            cardRect.anchorMax = new Vector2(0.97f, 0.75f);
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;

            var scrollRoot = UIFactory.CreateScrollList(card.transform, spacing: 6f);

            // 제목
            var (titleGo, titleLbl) = UIFactory.CreateListItem(scrollRoot,
                $"보고서 - {session.LocationName}", 56f, t.surfaceBorder);
            titleLbl.color = new Color(0.85f, 0.65f, 0.2f, 1f);
            titleLbl.fontSize = t.fontSizeHeading;

            // 설명
            UIFactory.CreateListItem(scrollRoot,
                "이 보고서는 마지막으로 전리품을 수집한 이후의 탐험에 대해 다룹니다.",
                44f, t.surface);

            // 통계
            var elapsedSec = session.GetElapsedSeconds();
            var minutes = Mathf.FloorToInt(elapsedSec / 60f);
            var seconds = Mathf.FloorToInt(elapsedSec % 60f);
            var expPerHour = session.GetExpPerHour();

            UIFactory.CreateListItem(scrollRoot, $"파견 시간: {minutes}:{seconds:00}", 40f, t.surfaceRaised);
            UIFactory.CreateListItem(scrollRoot, $"클리어한 지역 수: {session.WavesClearedCount}", 40f, t.surfaceRaised);
            UIFactory.CreateListItem(scrollRoot, $"획득한 경험치: {session.TotalExpEarned}", 40f, t.surfaceRaised);
            UIFactory.CreateListItem(scrollRoot, $"시간 당 경험치: {expPerHour:F1}", 40f, t.surfaceRaised);

            // 파견 캐릭터 상태
            UIFactory.CreateListItem(scrollRoot, "── 파견 캐릭터 ──", 36f, t.surfaceBorder);
            foreach (var ally in session.AllyStates)
            {
                float hpRatio = ally.maxHp > 0 ? (float)ally.hp / ally.maxHp : 0f;
                var aliveText = ally.hp > 0 ? $"HP {ally.hp}/{ally.maxHp}" : "사망";
                UIFactory.CreateListItem(scrollRoot, $"{ally.displayName}  {aliveText}", 40f, t.surfaceRaised);
            }

            // 닫기 버튼
            UIFactory.CreateActionItem(scrollRoot, "닫기", 56f, t.muted,
                () => { if (reportOverlay != null) Destroy(reportOverlay); });
        }

        // ── 결과 화면 ──────────────────────────────────────────────

        private void ShowResult(BattleSession session)
        {
            if (resultOverlay != null) Destroy(resultOverlay);

            var t = UITheme.Instance;
            resultOverlay = new GameObject("ResultOverlay");
            resultOverlay.transform.SetParent(canvasRoot, false);
            var bg = resultOverlay.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0.04f, 0.92f);
            var bgRect = resultOverlay.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var card = new GameObject("ResultCard");
            card.transform.SetParent(resultOverlay.transform, false);
            card.AddComponent<Image>().color = t.surface;
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.03f, 0.04f);
            cardRect.anchorMax = new Vector2(0.97f, 0.96f);
            cardRect.offsetMin = Vector2.zero;
            cardRect.offsetMax = Vector2.zero;

            var scrollRoot = UIFactory.CreateScrollList(card.transform, spacing: 8f);

            // 제목
            var isVictory = session.CompletionReason == "victory";
            var titleColor = isVictory ? new Color(0.85f, 0.75f, 0.25f) : new Color(1f, 0.3f, 0.3f);
            var titleLabel = isVictory ? "파견 완료" : "전투 패배";
            var (titleGo, titleLbl) = UIFactory.CreateListItem(scrollRoot,
                $"{titleLabel} - {session.LocationName}", 72f, t.surfaceBorder);
            titleLbl.color = titleColor;
            titleLbl.alignment = TextAnchor.MiddleCenter;
            titleLbl.fontSize = t.fontSizeHeading;

            // 요약
            var elapsedSec = session.GetElapsedSeconds();
            var minutes = Mathf.FloorToInt(elapsedSec / 60f);
            var seconds = Mathf.FloorToInt(elapsedSec % 60f);

            UIFactory.CreateListItem(scrollRoot,
                $"전투 시간: {minutes}:{seconds:00}  |  웨이브: {session.WavesClearedCount}  |  보상: {session.CollectedRewardCount}/{session.RewardBudget}",
                56f, t.surfaceRaised);

            UIFactory.CreateListItem(scrollRoot,
                $"총 크레딧: +{session.TotalCreditsEarned}C  |  총 경험치: +{session.TotalExpEarned}",
                56f, t.surfaceRaised);

            // 캐릭터별 경험치
            UIFactory.CreateListItem(scrollRoot, "── 캐릭터별 경험치 ──", 44f, t.surfaceBorder);
            foreach (var ally in session.AllyStates)
            {
                session.PerMercenaryExp.TryGetValue(ally.mercenaryId, out var exp);
                var aliveText = ally.hp > 0 ? $"HP {ally.hp}/{ally.maxHp}" : "사망";
                UIFactory.CreateListItem(scrollRoot,
                    $"{ally.displayName}  +{exp} EXP  ({aliveText})", 52f, t.surfaceRaised);
            }

            // 획득 보상
            UIFactory.CreateListItem(scrollRoot, "── 획득 보상 ──", 44f, t.surfaceBorder);

            var materialStacks = new Dictionary<string, int>();
            var equipmentLines = new List<string>();
            foreach (var drop in session.CollectedDrops)
            {
                if (drop.IsEquipment)
                {
                    var tier = drop.Grade switch
                    {
                        1 => "Normal", 2 => "Rare", 3 => "Elite",
                        4 => "Epic", 5 => "Legend", _ => $"G{drop.Grade}"
                    };
                    equipmentLines.Add($"[{tier}] {drop.ItemName} +{drop.StatValue}");
                }
                else
                {
                    materialStacks.TryGetValue(drop.ItemName, out var cnt);
                    materialStacks[drop.ItemName] = cnt + 1;
                }
            }

            foreach (var kv in materialStacks)
                UIFactory.CreateListItem(scrollRoot, $"{kv.Key} x{kv.Value}", 44f, t.surfaceRaised);
            foreach (var line in equipmentLines)
                UIFactory.CreateListItem(scrollRoot, line, 44f, t.surfaceRaised);
            if (session.CollectedDrops.Count == 0)
                UIFactory.CreateListItem(scrollRoot, "(없음)", 44f, t.muted);

            // 확인 버튼
            var sessionId = session.SessionId;
            UIFactory.CreateActionItem(scrollRoot, "확인", 72f, t.primary, () =>
            {
                BattleSessionManager.Instance?.DismissCompleted(sessionId);
                if (resultOverlay != null) Destroy(resultOverlay);
                RefreshFields();
            });
        }

        // ── 파견 오버레이 ───────────────────────────────────────────

        private void OpenDispatch(string locationId, string locationName)
        {
            selectedLocationId = locationId;
            selectedAllies.Clear();
            overlayTitle.text = locationName;
            overlayStatus.text = $"최대 {maxPartySize}명 선택";
            UIFactory.ClearChildren(partyListRoot);
            dispatchOverlay.SetActive(true);

            if (!GameCsvTables.TryLoad(out var tables, out _)) return;
            TalentCatalog.TryLoad(out var talents, out _);

            var owned = PlayerAccountService.GetOwnedMercenaries();
            var manager = BattleSessionManager.Instance;

            if (owned.Count == 0)
            {
                UIFactory.CreateListItem(partyListRoot, "보유 용병 없음", 92f, UITheme.Instance.muted);
                return;
            }

            for (var i = 0; i < owned.Count; i++)
            {
                var record = owned[i];
                var dispatched = manager != null && manager.IsMercenaryDispatched(record.mercenaryId);
                tables.TryGetCharacterRow(record.templateId, out var charRow);
                var name = string.IsNullOrWhiteSpace(charRow.Name) ? record.templateId : charRow.Name;
                var talent = talents != null ? talents.GetTalentName(record.talentTag) : record.talentTag;
                var label = $"#{i + 1}  {name}  Lv.{record.level}\n{talent}";

                if (dispatched)
                    UIFactory.CreateListItem(partyListRoot, "[파견중] " + label, 100f, UITheme.Instance.muted);
                else
                    BuildSelectableEntry(partyListRoot, label, record.mercenaryId);
            }
        }

        private void BuildSelectableEntry(Transform parent, string label, string mercenaryId)
        {
            var t = UITheme.Instance;
            var (go, lbl) = UIFactory.CreateListItem(parent, "[ ] " + label, 100f, t.surfaceRaised);
            var img = go.GetComponent<Image>();
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                if (selectedAllies.Contains(mercenaryId))
                {
                    selectedAllies.Remove(mercenaryId);
                    lbl.text = "[ ] " + label;
                    img.color = t.surfaceRaised;
                }
                else
                {
                    if (selectedAllies.Count >= maxPartySize)
                    {
                        overlayStatus.text = $"최대 {maxPartySize}명 초과";
                        return;
                    }

                    selectedAllies.Add(mercenaryId);
                    lbl.text = "[✓] " + label;
                    img.color = t.success;
                }

                overlayStatus.text = $"{selectedAllies.Count} / {maxPartySize} 선택";
            });
        }

        private GameObject BuildDispatchOverlay(Transform root)
        {
            var t = UITheme.Instance;

            var overlay = new GameObject("DispatchOverlay");
            overlay.transform.SetParent(root, false);
            overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);
            var overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            var card = new GameObject("Card");
            card.transform.SetParent(overlay.transform, false);
            card.AddComponent<Image>().color = t.surface;
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0f, 0f);
            cardRect.anchorMax = new Vector2(1f, 1f);
            cardRect.offsetMin = new Vector2(24f, UIFactory.BottomNavH + 24f);
            cardRect.offsetMax = new Vector2(-24f, -(UIFactory.TopBarH + 24f));

            var titleBar = new GameObject("TitleBar");
            titleBar.transform.SetParent(card.transform, false);
            titleBar.AddComponent<Image>().color = t.surfaceBorder;
            var titleRect = titleBar.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(0f, -100f);
            titleRect.offsetMax = Vector2.zero;

            overlayTitle = UIFactory.CreateText(titleBar.transform, "파견 편성", TextRole.Heading,
                TextAnchor.MiddleLeft, new Vector2(UIFactory.HorizPad + 10f, 0f), new Vector2(560f, 100f));
            overlayStatus = UIFactory.CreateText(titleBar.transform, string.Empty, TextRole.Caption,
                TextAnchor.MiddleRight, new Vector2(-(UIFactory.HorizPad + 10f), 0f), new Vector2(280f, 100f));

            var listArea = new GameObject("ListArea");
            listArea.transform.SetParent(card.transform, false);
            var listRect = listArea.AddComponent<RectTransform>();
            listRect.anchorMin = Vector2.zero;
            listRect.anchorMax = Vector2.one;
            listRect.offsetMin = new Vector2(0f, 110f);
            listRect.offsetMax = new Vector2(0f, -100f);
            partyListRoot = UIFactory.CreateScrollList(listArea.transform, spacing: 8f);

            var btnBar = new GameObject("BtnBar");
            btnBar.transform.SetParent(card.transform, false);
            var btnRect = btnBar.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0f, 0f);
            btnRect.anchorMax = new Vector2(1f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = new Vector2(0f, 110f);
            var btnRow = UIFactory.CreateHorizontalRow(btnBar.transform, spacing: 8f);
            UIFactory.CreateNavButton(btnRow, "파견 확정", OnConfirmDispatch, ButtonVariant.Success);
            UIFactory.CreateNavButton(btnRow, "닫기", () => overlay.SetActive(false), ButtonVariant.Muted);

            return overlay;
        }

        private void OnConfirmDispatch()
        {
            if (string.IsNullOrWhiteSpace(selectedLocationId) || selectedAllies.Count == 0)
            {
                overlayStatus.text = "현장 또는 용병을 선택하세요.";
                return;
            }

            var manager = BattleSessionManager.Instance;
            if (manager == null)
            {
                overlayStatus.text = "세션 매니저 오류";
                return;
            }

            GameCsvTables.TryLoad(out var tables, out _);
            var locationName = selectedLocationId;
            var locations = tables?.GetOpenLocations();
            if (locations != null)
            {
                var loc = locations.FirstOrDefault(l => l.LocationId == selectedLocationId);
                if (!string.IsNullOrWhiteSpace(loc.Name))
                    locationName = loc.Name;
            }

            if (!manager.TryStartSession(selectedLocationId, locationName, selectedAllies, out var error))
            {
                overlayStatus.text = error;
                return;
            }

            dispatchOverlay.SetActive(false);
            manager.ViewingLocationId = selectedLocationId;
            SceneNavigator.TryLoad("Battle");
        }

        // ── 유틸 ───────────────────────────────────────────────────

        private static GameObject CreateStretchText(Transform parent, string value, int fontSize, Color color)
        {
            var t = UITheme.Instance;
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = t.GetFont();
            text.fontSize = fontSize;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            text.supportRichText = true;
            go.AddComponent<RectTransform>();
            return go;
        }
    }
}
