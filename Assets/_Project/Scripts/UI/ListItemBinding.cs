using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectH.UI
{
    public sealed class ListItemBinding : MonoBehaviour
    {
        public TMP_Text label;
        public Image background;
        public Button button;

        public void Set(string text, Color bg, Action onClick = null, float height = 0f)
        {
            label.text = text;
            background.color = bg;
            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(() => onClick());
            button.interactable = onClick != null;
            if (height > 0f)
            {
                var le = GetComponent<LayoutElement>();
                if (le != null) le.preferredHeight = height;
            }
        }
    }
}
