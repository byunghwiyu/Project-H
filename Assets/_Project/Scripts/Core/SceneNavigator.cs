using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectH.Core
{
    public static class SceneNavigator
    {
        public static bool TryLoad(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[scene] sceneName is empty");
                return false;
            }

            var normalized = sceneName.Trim();
            if (TryResolveBuildIndex(normalized, out var buildIndex))
            {
                Debug.Log($"[scene] Loading '{normalized}' by buildIndex={buildIndex}");
                SceneManager.LoadScene(buildIndex, LoadSceneMode.Single);
                return true;
            }

            try
            {
                SceneManager.LoadScene(normalized, LoadSceneMode.Single);
                return true;
            }
            catch
            {
            }

            var fallbackPath = $"Assets/_Project/Scenes/{normalized}.unity";
            try
            {
                SceneManager.LoadScene(fallbackPath, LoadSceneMode.Single);
                return true;
            }
            catch
            {
            }

            Debug.LogError(
                $"[scene] Cannot load scene '{sceneName}'. Available in BuildSettings: {GetBuildSceneList()}");
            return false;
        }

        private static bool TryResolveBuildIndex(string targetSceneName, out int index)
        {
            index = -1;
            for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(i);
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.Equals(fileName, targetSceneName, System.StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        private static string GetBuildSceneList()
        {
            var list = string.Empty;
            for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(i);
                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(list))
                {
                    list += ", ";
                }

                list += fileName;
            }

            return string.IsNullOrEmpty(list) ? "(none)" : list;
        }
    }
}
