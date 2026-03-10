using ProjectH.UI.Scenes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ProjectH.Core
{
    public static class SceneFlowAutoSetup
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
            EnsureEventSystem();

            var key = NormalizeSceneKey(scene.name);

            if (key.Contains("recruit") || key.Contains("mercenaryrecruit") || key.Contains("용병모집"))
            {
                EnsureController<RecruitSceneController>("RecruitSceneController");
                return;
            }

            if (key.Contains("office") || key.Contains("lobby") || key.Contains("사무실"))
            {
                EnsureController<OfficeSceneController>("OfficeSceneController");
                return;
            }

            if (key.Contains("dungeon") || key.Contains("던전"))
            {
                EnsureController<DungeonSceneController>("DungeonSceneController");
            }
        }

        private static string NormalizeSceneKey(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim().ToLowerInvariant();
        }

        private static void EnsureController<T>(string rootName) where T : Component
        {
            var existing = Object.FindFirstObjectByType<T>();
            if (existing != null)
            {
                return;
            }

            var root = new GameObject(rootName);
            root.AddComponent<T>();
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var root = new GameObject("EventSystem");
            root.AddComponent<EventSystem>();

            var inputSystemType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemType != null)
            {
                root.AddComponent(inputSystemType);
                return;
            }

            root.AddComponent<StandaloneInputModule>();
        }
    }
}
