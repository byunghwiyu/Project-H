using UnityEngine;

namespace ProjectH.Core
{
    public sealed class BootLoader : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("[boot] BootLoader started, navigating to Recruit...");
            SceneNavigator.TryLoad("Recruit");
        }
    }
}
