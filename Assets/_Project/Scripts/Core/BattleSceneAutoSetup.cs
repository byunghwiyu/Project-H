using UnityEngine;
using UnityEngine.SceneManagement;

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

            if (Object.FindFirstObjectByType<BattleBootstrap>() == null)
            {
                var root = new GameObject("BattleRoot");
                root.AddComponent<BattleBootstrap>();
            }
        }
    }
}
