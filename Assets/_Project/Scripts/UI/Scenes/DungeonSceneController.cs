using System.Collections.Generic;
using System.Linq;
using ProjectH.Account;
using ProjectH.Battle;
using ProjectH.Core;
using ProjectH.Data.Tables;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ProjectH.UI;

namespace ProjectH.UI.Scenes
{
    public sealed class DungeonSceneController : MonoBehaviour
    {
        // ── 씬 UI 참조 ───────────────────────────────────────────────
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Transform fieldListRoot;
        [SerializeField] private Transform canvasRoot;

        // ── 파견 오버레이 ────────────────────────────────────────────
        [SerializeField] private GameObject dispatchOverlay;
        [SerializeField] private Transform partyListRoot;
        [SerializeField] private TMP_Text overlayTitle;
        [SerializeField] private TMP_Text overlayStatus;
        [SerializeField] private Button confirmBtn;
        [SerializeField] private Button dispatchCloseBtn;

        // ── 내비 버튼 ────────────────────────────────────────────────
        [SerializeField] private Button recruitNavBtn;
        [SerializeField] private Button officeNavBtn;
        [SerializeField] private Button refreshNavBtn;

        // ── 프리팹 ───────────────────────────────────────────────────
        [SerializeField] private SessionCardBinding sessionCardPrefab;
        [SerializeField] private ListItemBinding listItemCardPrefab;

        private string selectedLocationId;
        private readonly List<string> selectedAllies = new List<string>();
        private int maxPartySize = 4;

        private GameObject resultOverlay;
        private GameObject reportOverlay;

        private void Start()
        {
            BattleSessionManager.EnsureExists();
            confirmBtn.onClick.AddListener(OnConfirmDispatch);
            dispatchCloseBtn.onClick.AddListener(() => dispatchOverlay.SetActive(false));
            recruitNavBtn.onClick.AddListener(() => SceneNavigator.TryLoad("Recruit"));
            officeNavBtn.onClick.AddListener(() => SceneNavigator.TryLoad("Office"));
            refreshNavBtn.onClick.AddListener(RefreshFields);
            RefreshFields();
            InvokeRepeating(nameof(PeriodicRefresh), 2f, 2f);
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

        private void PeriodicRefresh()
        {
            if (dispatchOverlay != null && dispatchOverlay.activeSelf) return;
            if (resultOverlay != null && resultOverlay.activeSelf) return;
            if (reportOverlay != null && reportOverlay.activeSelf) return;
            RefreshFields();
        }

        // ── 현장 목록 ────────────────────────────────────────────────

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
                Instantiate(listItemCardPrefab, fieldListRoot)
                    .Set("개방된 현장 없음", UITheme.Instance.muted, null, 92f);
                return;
            }

            var manager = BattleSessionManager.Instance;
            foreach (var loc in open)
            {
                var activeSession = manager?.GetActiveSession(loc.LocationId);
                if (activeSession != null)
                    BuildActiveSessionCard(loc, activeSession);
                else
                {
                    var locId   = loc.LocationId;
                    var locName = loc.Name;
                    Instantiate(listItemCardPrefab, fieldListRoot)
                        .Set($"{loc.Name}\n{loc.LocationId}", UITheme.Instance.surfaceRaised,
                            () => OpenDispatch(locId, locName), 110f);
                }
            }
        }

        // ── 활성 세션 카드 ──────────────────────────────────────────

        private void BuildActiveSessionCard(LocationRow loc, BattleSession session)
        {
            var card = Instantiate(sessionCardPrefab, fieldListRoot);
            card.locationName.text = loc.Name;

            foreach (var ally in session.AllyStates)
                BuildAllyIcon(card.iconRow, ally);

            card.statusText.text = session.StatusText;

            if (card.progressFill != null)
                card.progressFill.fillAmount = Mathf.Clamp01(session.RewardProgress);

            var locId = loc.LocationId;
            card.cardBtn.onClick.AddListener(() =>
            {
                var mgr = BattleSessionManager.Instance;
                if (mgr != null)
                {
                    mgr.ViewingLocationId = locId;
                    SceneNavigator.TryLoad("Battle");
                }
            });

            var sessionRef = session;
            card.reportBtn.onClick.AddListener(() => ShowReport(sessionRef));
        }

        private void BuildAllyIcon(Transform parent, AllySessionState ally)
        {
            var iconGo = new GameObject("AllyIcon");
            iconGo.transform.SetParent(parent, false);
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = new Color(0.25f, 0.25f, 0.3f, 1f);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(56f, 56f);

            if (!string.IsNullOrWhiteSpace(ally.iconResourcePath))
            {
                var sprite = Resources.Load<Sprite>(ally.iconResourcePath + "_icon");
                if (sprite != null) iconImg.sprite = sprite;
            }

            var hpBgGo = new GameObject("HpBg");
            hpBgGo.transform.SetParent(iconGo.transform, false);
            hpBgGo.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 1f);
            var hpBgRect = hpBgGo.GetComponent<RectTransform>();
            hpBgRect.anchorMin = new Vector2(0f, 0f);
            hpBgRect.anchorMax = new Vector2(1f, 0f);
            hpBgRect.pivot     = new Vector2(0.5f, 1f);
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

            var (_, titleLbl) = UIFactory.CreateListItem(scrollRoot,
                $"보고서 - {session.LocationName}", 56f, t.surfaceBorder);
            titleLbl.color    = new Color(0.85f, 0.65f, 0.2f, 1f);
            titleLbl.fontSize = t.fontSizeHeading;

            UIFactory.CreateListItem(scrollRoot,
                "이 보고서는 마지막으로 전리품을 수집한 이후의 탐험에 대해 다룹니다.",
                44f, t.surface);

            var elapsedSec = session.GetElapsedSeconds();
            var minutes = Mathf.FloorToInt(elapsedSec / 60f);
            var seconds = Mathf.FloorToInt(elapsedSec % 60f);
            var expPerHour = session.GetExpPerHour();

            UIFactory.CreateListItem(scrollRoot, $"파견 시간: {minutes}:{seconds:00}", 40f, t.surfaceRaised);
            UIFactory.CreateListItem(scrollRoot, $"클리어한 지역 수: {session.WavesClearedCount}", 40f, t.surfaceRaised);
            UIFactory.CreateListItem(scrollRoot, $"획득한 경험치: {session.TotalExpEarned}", 40f, t.surfaceRaised);
            UIFactory.CreateListItem(scrollRoot, $"시간 당 경험치: {expPerHour:F1}", 40f, t.surfaceRaised);

            UIFactory.CreateListItem(scrollRoot, "── 파견 캐릭터 ──", 36f, t.surfaceBorder);
            foreach (var ally in session.AllyStates)
            {
                var aliveText = ally.hp > 0 ? $"HP {ally.hp}/{ally.maxHp}" : "사망";
                UIFactory.CreateListItem(scrollRoot, $"{ally.displayName}  {aliveText}", 40f, t.surfaceRaised);
            }

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

            var isVictory  = session.CompletionReason == "victory";
            var titleColor = isVictory ? new Color(0.85f, 0.75f, 0.25f) : new Color(1f, 0.3f, 0.3f);
            var titleLabel = isVictory ? "파견 완료" : "전투 패배";
            var (_, titleLbl) = UIFactory.CreateListItem(scrollRoot,
                $"{titleLabel} - {session.LocationName}", 72f, t.surfaceBorder);
            titleLbl.color     = titleColor;
            titleLbl.alignment = TextAlignmentOptions.Midline;
            titleLbl.fontSize  = t.fontSizeHeading;

            var elapsedSec = session.GetElapsedSeconds();
            var minutes = Mathf.FloorToInt(elapsedSec / 60f);
            var seconds = Mathf.FloorToInt(elapsedSec % 60f);

            UIFactory.CreateListItem(scrollRoot,
                $"전투 시간: {minutes}:{seconds:00}  |  웨이브: {session.WavesClearedCount}  |  보상: {session.CollectedRewardCount}/{session.RewardBudget}",
                56f, t.surfaceRaised);
            UIFactory.CreateListItem(scrollRoot,
                $"총 크레딧: +{session.TotalCreditsEarned}C  |  총 경험치: +{session.TotalExpEarned}",
                56f, t.surfaceRaised);

            UIFactory.CreateListItem(scrollRoot, "── 캐릭터별 경험치 ──", 44f, t.surfaceBorder);
            foreach (var ally in session.AllyStates)
            {
                session.PerMercenaryExp.TryGetValue(ally.mercenaryId, out var exp);
                var aliveText = ally.hp > 0 ? $"HP {ally.hp}/{ally.maxHp}" : "사망";
                UIFactory.CreateListItem(scrollRoot,
                    $"{ally.displayName}  +{exp} EXP  ({aliveText})", 52f, t.surfaceRaised);
            }

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
            overlayTitle.text  = locationName;
            overlayStatus.text = $"최대 {maxPartySize}명 선택";
            UIFactory.ClearChildren(partyListRoot);
            dispatchOverlay.SetActive(true);

            if (!GameCsvTables.TryLoad(out var tables, out _)) return;
            TalentCatalog.TryLoad(out var talents, out _);

            var owned   = PlayerAccountService.GetOwnedMercenaries();
            var manager = BattleSessionManager.Instance;

            if (owned.Count == 0)
            {
                Instantiate(listItemCardPrefab, partyListRoot)
                    .Set("보유 용병 없음", UITheme.Instance.muted, null, 92f);
                return;
            }

            for (var i = 0; i < owned.Count; i++)
            {
                var record     = owned[i];
                var dispatched = manager != null && manager.IsMercenaryDispatched(record.mercenaryId);
                tables.TryGetCharacterRow(record.templateId, out var charRow);
                var name   = string.IsNullOrWhiteSpace(charRow.Name) ? record.templateId : charRow.Name;
                var talent = talents != null ? talents.GetTalentName(record.talentTag) : record.talentTag;
                var label  = $"#{i + 1}  {name}  Lv.{record.level}\n{talent}";

                if (dispatched)
                {
                    Instantiate(listItemCardPrefab, partyListRoot)
                        .Set("[파견중] " + label, UITheme.Instance.muted, null, 100f);
                }
                else
                {
                    var mercId = record.mercenaryId;
                    var card   = Instantiate(listItemCardPrefab, partyListRoot);
                    card.label.text      = "[ ] " + label;
                    card.background.color = UITheme.Instance.surfaceRaised;
                    var le = card.GetComponent<LayoutElement>();
                    if (le != null) le.preferredHeight = 100f;
                    card.button.onClick.AddListener(() =>
                    {
                        var th = UITheme.Instance;
                        if (selectedAllies.Contains(mercId))
                        {
                            selectedAllies.Remove(mercId);
                            card.label.text       = "[ ] " + label;
                            card.background.color = th.surfaceRaised;
                        }
                        else
                        {
                            if (selectedAllies.Count >= maxPartySize)
                            {
                                overlayStatus.text = $"최대 {maxPartySize}명 초과";
                                return;
                            }
                            selectedAllies.Add(mercId);
                            card.label.text       = "[✓] " + label;
                            card.background.color = th.success;
                        }
                        overlayStatus.text = $"{selectedAllies.Count} / {maxPartySize} 선택";
                    });
                }
            }
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
            var locations    = tables?.GetOpenLocations();
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
    }
}
