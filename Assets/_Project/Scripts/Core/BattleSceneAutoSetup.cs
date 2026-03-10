using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectH.Core
{
    public static class BattleSceneAutoSetup
    {
        private static bool initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyForScene(scene);
        }

        private static void ApplyForScene(Scene scene)
        {
            if (!scene.name.Equals("Battle"))
            {
                return;
            }

            var bootstrap = Object.FindFirstObjectByType<BattleBootstrap>();
            if (bootstrap == null)
            {
                var root = new GameObject("BattleRoot");
                bootstrap = root.AddComponent<BattleBootstrap>();
            }

            EnsurePauseCanvas(bootstrap);
        }

        private static void EnsurePauseCanvas(BattleBootstrap bootstrap)
        {
            if (Object.FindFirstObjectByType<Canvas>() != null)
            {
                return;
            }

            var canvasGo = new GameObject("BattleCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var buttonGo = new GameObject("PauseButton");
            buttonGo.transform.SetParent(canvasGo.transform, false);

            var image = buttonGo.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            var button = buttonGo.AddComponent<Button>();
            button.onClick.AddListener(bootstrap.TogglePause);

            var rt = buttonGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(140f, 44f);
            rt.anchoredPosition = new Vector2(-24f, -24f);

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(buttonGo.transform, false);
            var text = textGo.AddComponent<Text>();
            text.text = "Pause";
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.white;

            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
        }
    }
}
