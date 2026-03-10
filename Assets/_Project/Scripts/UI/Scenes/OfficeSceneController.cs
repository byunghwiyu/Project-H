using ProjectH.Account;
using ProjectH.Core;
using ProjectH.Data.Tables;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectH.UI.Scenes
{
    public sealed class OfficeSceneController : MonoBehaviour
    {
        private Text bodyText;

        private void Start()
        {
            BuildUi();
            Refresh();
        }

        private void BuildUi()
        {
            var canvas = new GameObject("OfficeCanvas").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.gameObject.AddComponent<CanvasScaler>();
            canvas.gameObject.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("Panel");
            panel.transform.SetParent(canvas.transform, false);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.09f, 0.1f, 0.12f, 0.95f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(820f, 640f);

            CreateText(panel.transform, "Office - Owned Mercenaries", 30, TextAnchor.UpperCenter, new Vector2(0f, 280f), new Vector2(760f, 60f));
            bodyText = CreateText(panel.transform, string.Empty, 18, TextAnchor.UpperLeft, new Vector2(0f, 220f), new Vector2(760f, 470f));

            CreateButton(panel.transform, "Recruit", new Vector2(-240f, -270f), () => NavigateTo("Recruit"));
            CreateButton(panel.transform, "Dungeon", new Vector2(0f, -270f), () => NavigateTo("Dungeon"));
            CreateButton(panel.transform, "Refresh", new Vector2(240f, -270f), Refresh);
        }

        private void Refresh()
        {
            if (!GameCsvTables.TryLoad(out var tables, out var error))
            {
                bodyText.text = $"Failed to load tables: {error}";
                return;
            }

            var owned = PlayerAccountService.OwnedTemplateIds;
            if (owned.Count == 0)
            {
                bodyText.text = "No owned mercenaries. Recruit first.";
                return;
            }

            var lines = "";
            for (var i = 0; i < owned.Count; i++)
            {
                var id = owned[i];
                if (tables.TryGetCombatUnit(id, out var row))
                {
                    lines += $"{i + 1}. {row.Name} ({row.EntityId})  HP:{row.MaxHp} ATK:{row.Attack} AGI:{row.Agility}\n";
                }
                else
                {
                    lines += $"{i + 1}. {id} (missing in combat_units)\n";
                }
            }

            bodyText.text = lines;
        }

        private void NavigateTo(string sceneName)
        {
            if (SceneNavigator.TryLoad(sceneName))
            {
                return;
            }

            bodyText.text = $"Scene load failed: {sceneName}. Check Build Settings and scene names.";
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
    }
}
