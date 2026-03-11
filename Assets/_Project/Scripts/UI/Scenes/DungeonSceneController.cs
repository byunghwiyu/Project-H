using System.Collections.Generic;
using ProjectH.Account;
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

        private void Start()
        {
            BuildUI();
            RefreshFields();
        }

        private void BuildUI()
        {
            var t = UITheme.Instance;
            var canvas = UIFactory.CreateCanvas("DungeonCanvas");
            var root = UIFactory.CreateFullPanel(canvas.transform);

            // ── 상단 바 ──────────────────────────────────────────────
            var topBar = UIFactory.CreateTopBar(root);

            UIFactory.CreateBarText(topBar, "현장 선택", TextRole.Heading,
                TextAnchor.MiddleLeft, left: UIFactory.HorizPad, right: 400f);

            statusText = UIFactory.CreateBarText(topBar, string.Empty, TextRole.Caption,
                TextAnchor.MiddleRight, left: 500f, right: UIFactory.HorizPad);

            // ── 콘텐츠 ───────────────────────────────────────────────
            var contentArea = UIFactory.CreateContentArea(root);
            fieldListRoot = UIFactory.CreateScrollList(contentArea, spacing: 10f);

            // ── 하단 내비 ────────────────────────────────────────────
            var bottomBar = UIFactory.CreateBottomBar(root);
            var navRow = UIFactory.CreateHorizontalRow(bottomBar, spacing: 4f);
            UIFactory.CreateNavButton(navRow, "모집", () => SceneNavigator.TryLoad("Recruit"), ButtonVariant.Nav);
            UIFactory.CreateNavButton(navRow, "사무소", () => SceneNavigator.TryLoad("Office"), ButtonVariant.Nav);
            UIFactory.CreateNavButton(navRow, "새로고침", RefreshFields, ButtonVariant.Muted);

            // ── 파견 오버레이 ─────────────────────────────────────────
            dispatchOverlay = BuildDispatchOverlay(root);
            dispatchOverlay.SetActive(false);
        }

        private void RefreshFields()
        {
            UIFactory.ClearChildren(fieldListRoot);

            if (!GameCsvTables.TryLoad(out var tables, out var error))
            {
                statusText.text = $"로드 실패";
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

            foreach (var loc in open)
            {
                UIFactory.CreateListItem(fieldListRoot,
                    $"{loc.Name}\n{loc.LocationId}", 110f,
                    UITheme.Instance.surfaceRaised,
                    () => OpenDispatch(loc.LocationId, loc.Name));
            }
        }

        private void OpenDispatch(string locationId, string locationName)
        {
            selectedLocationId = locationId;
            selectedAllies.Clear();
            overlayTitle.text   = locationName;
            overlayStatus.text  = $"최대 {maxPartySize}명 선택";
            UIFactory.ClearChildren(partyListRoot);
            dispatchOverlay.SetActive(true);

            if (!GameCsvTables.TryLoad(out var tables, out _)) return;
            TalentCatalog.TryLoad(out var talents, out _);

            var owned = PlayerAccountService.GetOwnedMercenaries();
            if (owned.Count == 0)
            {
                UIFactory.CreateListItem(partyListRoot, "보유 용병 없음", 92f, UITheme.Instance.muted);
                return;
            }

            for (var i = 0; i < owned.Count; i++)
            {
                var record = owned[i];
                tables.TryGetCharacterRow(record.templateId, out var charRow);
                var name   = string.IsNullOrWhiteSpace(charRow.Name) ? record.templateId : charRow.Name;
                var talent = talents != null ? talents.GetTalentName(record.talentTag) : record.talentTag;
                BuildSelectableEntry(partyListRoot, $"#{i + 1}  {name}  Lv.{record.level}\n{talent}",
                    record.mercenaryId);
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
                    lbl.text  = "[ ] " + label;
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
                    lbl.text  = "[✓] " + label;
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

            // 카드
            var card = new GameObject("Card");
            card.transform.SetParent(overlay.transform, false);
            card.AddComponent<Image>().color = t.surface;
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0f, 0f);
            cardRect.anchorMax = new Vector2(1f, 1f);
            cardRect.offsetMin = new Vector2(24f, UIFactory.BottomNavH + 24f);
            cardRect.offsetMax = new Vector2(-24f, -(UIFactory.TopBarH + 24f));

            // 제목 바
            var titleBar = new GameObject("TitleBar");
            titleBar.transform.SetParent(card.transform, false);
            titleBar.AddComponent<Image>().color = t.surfaceBorder;
            var titleRect = titleBar.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot     = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(0f, -100f);
            titleRect.offsetMax = Vector2.zero;

            overlayTitle = UIFactory.CreateText(titleBar.transform, "파견 편성", TextRole.Heading,
                TextAnchor.MiddleLeft, new Vector2(UIFactory.HorizPad + 10f, 0f), new Vector2(560f, 100f));

            overlayStatus = UIFactory.CreateText(titleBar.transform, string.Empty, TextRole.Caption,
                TextAnchor.MiddleRight, new Vector2(-(UIFactory.HorizPad + 10f), 0f), new Vector2(280f, 100f));

            // 리스트 영역
            var listArea = new GameObject("ListArea");
            listArea.transform.SetParent(card.transform, false);
            var listRect = listArea.AddComponent<RectTransform>();
            listRect.anchorMin = Vector2.zero;
            listRect.anchorMax = Vector2.one;
            listRect.offsetMin = new Vector2(0f, 110f);
            listRect.offsetMax = new Vector2(0f, -100f);
            partyListRoot = UIFactory.CreateScrollList(listArea.transform, spacing: 8f);

            // 버튼 바
            var btnBar = new GameObject("BtnBar");
            btnBar.transform.SetParent(card.transform, false);
            var btnRect = btnBar.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0f, 0f);
            btnRect.anchorMax = new Vector2(1f, 0f);
            btnRect.pivot     = new Vector2(0.5f, 0f);
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

            PlayerAccountService.SetDispatch(selectedLocationId, 1, selectedAllies);
            SceneNavigator.TryLoad("Battle");
        }
    }
}
