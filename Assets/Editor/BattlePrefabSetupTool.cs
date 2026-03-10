#if UNITY_EDITOR
using System;
using System.IO;
using ProjectH.UI.Battle;
using UnityEditor;
using UnityEngine;

public static class BattlePrefabSetupTool
{
    private const string UnitsFolder = "Assets/_Project/Resources/Prefabs/Units";

    [MenuItem("ProjectH/Setup Battle Unit Prefabs")]
    public static void SetupBattleUnitPrefabs()
    {
        if (!AssetDatabase.IsValidFolder(UnitsFolder))
        {
            Debug.LogError($"[ProjectH] Folder not found: {UnitsFolder}");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { UnitsFolder });
        var changed = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            var dirty = false;

            try
            {
                var sr = root.GetComponent<SpriteRenderer>();
                if (sr == null)
                {
                    sr = root.AddComponent<SpriteRenderer>();
                    dirty = true;
                }

                if (sr.sortingOrder != 10)
                {
                    sr.sortingOrder = 10;
                    dirty = true;
                }

                var animator = root.GetComponent<Animator>();
                if (animator == null)
                {
                    root.AddComponent<Animator>();
                    dirty = true;
                }

                var view = root.GetComponent<UnitView>();
                if (view == null)
                {
                    root.AddComponent<UnitView>();
                    dirty = true;
                }

                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProjectH] Failed setup {path}: {ex}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ProjectH] Battle prefab setup complete. changed={changed}");
    }
}
#endif
