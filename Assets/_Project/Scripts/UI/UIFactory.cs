using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectH.UI
{
    public enum ButtonVariant { Primary, Success, Danger, Warning, Muted, Nav }

    /// <summary>
    /// 프리팹 전환 후 남은 유틸리티 메서드.
    /// - ClearChildren: 리스트 초기화
    /// - GradeColor / VariantToColor: 색상 조회
    /// - CreateScrollList / CreateListItem / CreateActionItem: 동적 오버레이용 (ShowReport, ShowResult)
    /// </summary>
    public static class UIFactory
    {
        public const float HorizPad = 20f;

        // ── 리스트 초기화 ──────────────────────────────────────────────

        public static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child != null) UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        // ── 색상 조회 ──────────────────────────────────────────────────

        public static Color GradeColor(int grade) => UITheme.Instance.GetGradeColor(grade);

        public static Color VariantToColor(UITheme t, ButtonVariant v) => v switch
        {
            ButtonVariant.Primary => t.primary,
            ButtonVariant.Success => t.success,
            ButtonVariant.Danger  => t.danger,
            ButtonVariant.Warning => t.warning,
            ButtonVariant.Muted   => t.muted,
            ButtonVariant.Nav     => t.primary,
            _                     => t.primary,
        };

        // ── 스크롤 리스트 (동적 오버레이용) ───────────────────────────

        public static Transform CreateScrollList(Transform parent, float spacing = 8f)
        {
            var scrollGO = new GameObject("ScrollView");
            scrollGO.transform.SetParent(parent, false);
            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.scrollSensitivity = 40f;
            scrollRect.decelerationRate = 0.12f;
            var scrollRectTr = scrollGO.GetComponent<RectTransform>();
            scrollRectTr.anchorMin = Vector2.zero;
            scrollRectTr.anchorMax = Vector2.one;
            scrollRectTr.offsetMin = Vector2.zero;
            scrollRectTr.offsetMax = Vector2.zero;

            var vpGO = new GameObject("Viewport");
            vpGO.transform.SetParent(scrollGO.transform, false);
            vpGO.AddComponent<RectMask2D>();
            var vpRect = vpGO.GetComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = Vector2.zero;
            vpRect.offsetMax = Vector2.zero;

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(vpGO.transform, false);
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot     = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.padding = new RectOffset((int)HorizPad, (int)HorizPad, 8, 8);

            var csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = vpRect;
            scrollRect.content  = contentRect;
            return contentGO.transform;
        }

        // ── 동적 리스트 아이템 (ShowReport / ShowResult 전용) ──────────

        public static (GameObject root, TMP_Text label) CreateListItem(Transform parent, string text,
            float height = 92f, Color? bgColor = null, Action onClick = null)
        {
            var t = UITheme.Instance;
            var go = new GameObject("ListItem");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = bgColor ?? t.surfaceRaised;
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            var lbl = MakeItemText(go, text, TextAlignmentOptions.MidlineLeft, t);
            return (go, lbl);
        }

        public static (GameObject root, TMP_Text label) CreateActionItem(Transform parent, string text,
            float height = 92f, Color? bgColor = null, Action onClick = null)
        {
            var t = UITheme.Instance;
            var go = new GameObject("ActionItem");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = bgColor ?? t.surfaceRaised;
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            var lbl = MakeItemText(go, text, TextAlignmentOptions.Midline, t);
            return (go, lbl);
        }

        private static TMP_Text MakeItemText(GameObject parent, string value,
            TextAlignmentOptions alignment, UITheme t)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent.transform, false);
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.font = t.GetTMPFont();
            txt.fontSize = t.fontSizeBody;
            txt.alignment = alignment;
            txt.color = t.textPrimary;
            txt.text = value;
            txt.raycastTarget = false;
            var rect = txt.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(HorizPad, 4f);
            rect.offsetMax = new Vector2(-HorizPad, -4f);
            return txt;
        }
    }
}
