using System.Collections.Generic;
using ProjectH.Account;
using ProjectH.Core;
using ProjectH.Data.Tables;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectH.UI.Scenes
{
    public sealed class RecruitSceneController : MonoBehaviour
    {
        [SerializeField] private string recruitPoolId = "default";
        [SerializeField] private int offerCountOverride;

        private Text statusText;
        private Transform listRoot;

        private void Start()
        {
            BuildUi();
            RefreshOffers();
        }

        private void BuildUi()
        {
            var canvas = new GameObject("RecruitCanvas").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.gameObject.AddComponent<CanvasScaler>();
            canvas.gameObject.AddComponent<GraphicRaycaster>();

            var panel = CreatePanel(canvas.transform, new Vector2(780f, 640f), new Vector2(0.5f, 0.5f));

            CreateText(panel, "Recruit Mercenaries", 30, TextAnchor.UpperCenter, new Vector2(0f, 280f), new Vector2(700f, 60f));
            statusText = CreateText(panel, string.Empty, 18, TextAnchor.UpperLeft, new Vector2(0f, 220f), new Vector2(700f, 60f));

            var listObj = new GameObject("OfferList");
            listObj.transform.SetParent(panel, false);
            var listRect = listObj.AddComponent<RectTransform>();
            listRect.sizeDelta = new Vector2(700f, 360f);
            listRect.anchoredPosition = new Vector2(0f, 10f);
            var layout = listObj.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 10f;
            listRoot = listObj.transform;

            CreateButton(panel, "Refresh", new Vector2(-240f, -270f), RefreshOffers);
            CreateButton(panel, "Office", new Vector2(0f, -270f), () => NavigateTo("Office"));
            CreateButton(panel, "Dungeon", new Vector2(240f, -270f), () => NavigateTo("Dungeon"));
        }

        private void RefreshOffers()
        {
            if (listRoot == null)
            {
                statusText.text = "Offer list root is missing.";
                return;
            }

            ClearChildrenSafe(listRoot);

            if (!GameCsvTables.TryLoad(out var tables, out var error))
            {
                statusText.text = $"Failed to load tables: {error}";
                return;
            }

            if (!tables.TryDrawRecruitOffers(recruitPoolId, offerCountOverride, out var offers, out error))
            {
                statusText.text = $"Failed to draw offers: {error}";
                return;
            }

            statusText.text = "Select an offer to recruit.";

            foreach (var offer in offers)
            {
                var id = offer.EntityId;
                var label = $"{offer.Name} ({id})  HP:{offer.MaxHp} ATK:{offer.Attack} AGI:{offer.Agility}";
                CreateOfferButton(listRoot, label, () =>
                {
                    PlayerAccountService.Recruit(id);
                    statusText.text = $"Recruited: {id}. Move to Office or Dungeon.";
                });
            }
        }

        private void NavigateTo(string sceneName)
        {
            if (SceneNavigator.TryLoad(sceneName))
            {
                return;
            }

            statusText.text = $"Scene load failed: {sceneName}. Check Build Settings and scene names.";
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

        private static RectTransform CreatePanel(Transform parent, Vector2 size, Vector2 anchor)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.07f, 0.08f, 0.1f, 0.95f);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return rect;
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
            rect.sizeDelta = new Vector2(200f, 44f);
            rect.anchoredPosition = pos;

            var text = CreateText(go.transform, label, 18, TextAnchor.MiddleCenter, Vector2.zero, rect.sizeDelta);
            text.color = Color.white;
        }

        private static void CreateOfferButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("OfferButton");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.14f, 0.18f, 0.22f, 1f);
            var button = go.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 56f;

            var text = CreateText(go.transform, label, 16, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(680f, 50f));
            text.color = new Color(0.9f, 0.95f, 1f, 1f);
            text.GetComponent<RectTransform>().anchoredPosition = new Vector2(10f, 0f);
        }
    }
}
