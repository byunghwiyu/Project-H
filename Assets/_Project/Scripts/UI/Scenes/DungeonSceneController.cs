using System.Collections.Generic;
using ProjectH.Account;
using ProjectH.Core;
using ProjectH.Data.Tables;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectH.UI.Scenes
{
    public sealed class DungeonSceneController : MonoBehaviour
    {
        private Text statusText;
        private Transform dungeonListRoot;
        private GameObject popup;
        private Transform popupListRoot;
        private Text popupTitle;

        private string selectedLocationId;
        private readonly List<string> selectedAllies = new List<string>();
        private int maxPartySize = 4;

        private void Start()
        {
            BuildUi();
            RefreshDungeons();
        }

        private void BuildUi()
        {
            var canvas = new GameObject("DungeonCanvas").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.gameObject.AddComponent<CanvasScaler>();
            canvas.gameObject.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("Panel");
            panel.transform.SetParent(canvas.transform, false);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.09f, 0.11f, 0.95f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(860f, 650f);

            CreateText(panel.transform, "던전 파견", 30, TextAnchor.UpperCenter, new Vector2(0f, 290f), new Vector2(780f, 60f));
            statusText = CreateText(panel.transform, string.Empty, 18, TextAnchor.UpperLeft, new Vector2(0f, 240f), new Vector2(780f, 50f));

            var listObj = new GameObject("DungeonList");
            listObj.transform.SetParent(panel.transform, false);
            dungeonListRoot = listObj.transform;
            var listRect = listObj.AddComponent<RectTransform>();
            listRect.sizeDelta = new Vector2(780f, 390f);
            listRect.anchoredPosition = new Vector2(0f, 20f);
            var layout = listObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            CreateButton(panel.transform, "용병 모집", new Vector2(-260f, -280f), () => SceneNavigator.TryLoad("Recruit"));
            CreateButton(panel.transform, "사무실", new Vector2(0f, -280f), () => SceneNavigator.TryLoad("Office"));
            CreateButton(panel.transform, "새로고침", new Vector2(260f, -280f), RefreshDungeons);

            popup = CreatePopup(panel.transform);
            popup.SetActive(false);
        }

        private void RefreshDungeons()
        {
            if (dungeonListRoot == null)
            {
                BuildUi();
            }

            ClearChildrenSafe(dungeonListRoot);

            if (!GameCsvTables.TryLoad(out var tables, out var error))
            {
                statusText.text = $"테이블 로드 실패: {error}";
                return;
            }

            maxPartySize = Mathf.Max(1, tables.GetDefineInt("maxPartySize", 4));

            var open = tables.GetOpenLocations();
            if (open.Count == 0)
            {
                statusText.text = "오픈된 던전이 없습니다.";
                return;
            }

            statusText.text = "던전을 선택하고 파견 인원을 지정하세요.";
            foreach (var loc in open)
            {
                var label = $"{loc.Name} ({loc.LocationId})";
                CreateListButton(dungeonListRoot, label, () => OpenDispatchPopup(loc.LocationId, loc.Name));
            }
        }

        private void OpenDispatchPopup(string locationId, string locationName)
        {
            selectedLocationId = locationId;
            selectedAllies.Clear();

            popupTitle.text = $"{locationName} 파견 인원 선택 (최대 {maxPartySize})";
            popup.SetActive(true);

            ClearChildrenSafe(popupListRoot);

            if (!GameCsvTables.TryLoad(out var tables, out var error))
            {
                statusText.text = $"테이블 로드 실패: {error}";
                return;
            }

            var owned = PlayerAccountService.OwnedTemplateIds;
            if (owned.Count == 0)
            {
                statusText.text = "보유 용병이 없습니다. Recruit 씬에서 고용하세요.";
                return;
            }

            foreach (var id in owned)
            {
                var label = id;
                if (tables.TryGetCombatUnit(id, out var row))
                {
                    label = $"{row.Name} ({row.EntityId})";
                }

                CreateSelectableButton(popupListRoot, label, id);
            }
        }

        private void CreateSelectableButton(Transform parent, string label, string templateId)
        {
            var btnObj = new GameObject("MercButton");
            btnObj.transform.SetParent(parent, false);
            var image = btnObj.AddComponent<Image>();
            image.color = new Color(0.18f, 0.2f, 0.24f, 1f);
            var button = btnObj.AddComponent<Button>();
            var layout = btnObj.AddComponent<LayoutElement>();
            layout.preferredHeight = 48f;

            var text = CreateText(btnObj.transform, "[ ] " + label, 16, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(640f, 44f));
            text.GetComponent<RectTransform>().anchoredPosition = new Vector2(10f, 0f);

            button.onClick.AddListener(() =>
            {
                if (selectedAllies.Contains(templateId))
                {
                    selectedAllies.Remove(templateId);
                    text.text = "[ ] " + label;
                    return;
                }

                if (selectedAllies.Count >= maxPartySize)
                {
                    statusText.text = $"최대 파견 인원은 {maxPartySize}명입니다.";
                    return;
                }

                selectedAllies.Add(templateId);
                text.text = "[선택] " + label;
            });
        }

        private GameObject CreatePopup(Transform parent)
        {
            var go = new GameObject("DispatchPopup");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.92f);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(720f, 520f);
            rect.anchoredPosition = new Vector2(0f, 0f);

            popupTitle = CreateText(go.transform, "파견 인원 선택", 24, TextAnchor.UpperCenter, new Vector2(0f, 220f), new Vector2(680f, 50f));

            var listObj = new GameObject("PopupList");
            listObj.transform.SetParent(go.transform, false);
            popupListRoot = listObj.transform;
            var listRect = listObj.AddComponent<RectTransform>();
            listRect.sizeDelta = new Vector2(680f, 330f);
            listRect.anchoredPosition = new Vector2(0f, 20f);
            var layout = listObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            CreateButton(go.transform, "파견 시작", new Vector2(-140f, -220f), OnConfirmDispatch);
            CreateButton(go.transform, "닫기", new Vector2(140f, -220f), () => go.SetActive(false));
            return go;
        }

        private void OnConfirmDispatch()
        {
            if (string.IsNullOrWhiteSpace(selectedLocationId))
            {
                statusText.text = "던전을 먼저 선택하세요.";
                return;
            }

            if (selectedAllies.Count == 0)
            {
                statusText.text = "최소 1명의 용병을 선택하세요.";
                return;
            }

            PlayerAccountService.SetDispatch(selectedLocationId, 1, selectedAllies);
            SceneNavigator.TryLoad("Battle");
        }

        private static Text CreateText(Transform parent, string value, int fontSize, TextAnchor anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = value;
            var rect = text.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return text;
        }

        private static void CreateButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.17f, 0.24f, 0.33f, 1f);
            var button = go.AddComponent<Button>();
            button.onClick.AddListener(onClick);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220f, 44f);
            rect.anchoredPosition = pos;

            var text = CreateText(go.transform, label, 18, TextAnchor.MiddleCenter, Vector2.zero, rect.sizeDelta);
            text.color = Color.white;
        }

        private static void CreateListButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("DungeonButton");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.16f, 0.2f, 0.27f, 1f);
            var button = go.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 52f;

            var text = CreateText(go.transform, label, 17, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(740f, 46f));
            text.GetComponent<RectTransform>().anchoredPosition = new Vector2(10f, 0f);
        }

        private static void ClearChildrenSafe(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }
    }
}
