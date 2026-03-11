using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectH.UI
{
    public enum TextRole { Title, Heading, Body, Caption }

    public enum ButtonVariant { Primary, Success, Danger, Warning, Muted, Nav }

    /// <summary>
    /// 모바일 세로(1080×1920) 기준 UI 생성 팩토리.
    /// 색상/폰트/사이즈는 UITheme.Instance에서 읽어 코드 변경 없이 교체 가능.
    /// </summary>
    public static class UIFactory
    {
        // ── 레이아웃 상수 (1080×1920 기준) ───────────────────────────
        public const float TopBarH    = 120f;
        public const float TabBarH    = 80f;
        public const float BottomNavH = 120f;
        public const float ItemH      = 92f;
        public const float HorizPad   = 20f;

        // ── Canvas ─────────────────────────────────────────────────────

        public static Canvas CreateCanvas(string name)
        {
            EnsureEventSystem();
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // 너비 기준 → 세로 폰에서 일관된 너비
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        // ── 전체화면 루트 패널 ─────────────────────────────────────────

        public static RectTransform CreateFullPanel(Transform parent, Color? color = null)
        {
            var go = new GameObject("FullPanel");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = color ?? UITheme.Instance.bg;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        // ── 상단 / 탭 / 하단 바 ────────────────────────────────────────

        public static RectTransform CreateTopBar(Transform parent, Color? color = null)
        {
            var go = new GameObject("TopBar");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = color ?? UITheme.Instance.surface;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot     = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -TopBarH);
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static RectTransform CreateTabBar(Transform parent, Color? color = null)
        {
            var go = new GameObject("TabBar");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = color ?? UITheme.Instance.surfaceBorder;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot     = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -(TopBarH + TabBarH));
            rect.offsetMax = new Vector2(0f, -TopBarH);
            return rect;
        }

        public static RectTransform CreateBottomBar(Transform parent, Color? color = null)
        {
            var go = new GameObject("BottomNav");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = color ?? UITheme.Instance.surface;
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot     = new Vector2(0.5f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(0f, BottomNavH);
            return rect;
        }

        // ── 콘텐츠 영역 ────────────────────────────────────────────────

        public static RectTransform CreateContentArea(Transform parent,
            float topInset = TopBarH, float bottomInset = BottomNavH, Color? color = null)
        {
            var go = new GameObject("ContentArea");
            go.transform.SetParent(parent, false);
            if (color.HasValue) go.AddComponent<Image>().color = color.Value;
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0f, bottomInset);
            rect.offsetMax = new Vector2(0f, -topInset);
            return rect;
        }

        // ── 스크롤 리스트 ──────────────────────────────────────────────

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
            // RectMask2D: Image+Mask 조합의 스텐실 버퍼 문제 없이 안전하게 클리핑
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

        // ── 수평 행 ────────────────────────────────────────────────────

        public static Transform CreateHorizontalRow(Transform parent, float spacing = 0f,
            Color? color = null)
        {
            var go = new GameObject("HRow");
            go.transform.SetParent(parent, false);
            if (color.HasValue) go.AddComponent<Image>().color = color.Value;
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset((int)HorizPad, (int)HorizPad, 14, 14);
            return go.transform;
        }

        // ── LayoutGroup용 버튼 ─────────────────────────────────────────

        public static Button CreateNavButton(Transform parent, string label, Action onClick,
            ButtonVariant variant = ButtonVariant.Nav)
        {
            var t = UITheme.Instance;
            var go = new GameObject("NavBtn");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = VariantToColor(t, variant);
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            go.AddComponent<LayoutElement>();

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.AddComponent<Text>();
            text.font = t.GetFont();
            text.fontSize = t.fontSizeBody;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = t.textPrimary;
            text.text = label;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return btn;
        }

        // ── 바 내부 텍스트 (stretch 앵커 기반) ────────────────────────

        /// <summary>
        /// TopBar / BottomBar 내부에 stretch 앵커로 텍스트를 배치합니다.
        /// left/right: 부모 좌우 끝에서의 inset (px).
        /// </summary>
        public static Text CreateBarText(Transform parent, string value, TextRole role,
            TextAnchor anchor, float left, float right, Color? colorOverride = null)
        {
            var t = UITheme.Instance;
            var go = new GameObject("BarText");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = t.GetFont();
            text.fontSize = RoleToFontSize(t, role);
            text.alignment = anchor;
            text.color = colorOverride ?? RoleToTextColor(t, role);
            text.text = value;
            text.supportRichText = true;
            text.resizeTextForBestFit = false;
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, 0f);
            rect.offsetMax = new Vector2(-right, 0f);
            return text;
        }

        // ── 기존 메서드 (하위 호환 + BattleHUD 사용) ──────────────────

        public static RectTransform CreatePanel(Transform parent, Vector2 pos, Vector2 size,
            Color? color = null)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = color ?? UITheme.Instance.surface;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return rect;
        }

        public static RectTransform CreateOverlayPanel(Transform parent, Vector2 size)
        {
            var go = new GameObject("Overlay");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = UITheme.Instance.overlay;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        public static Text CreateText(Transform parent, string value, TextRole role,
            TextAnchor anchor, Vector2 pos, Vector2 size,
            Color? colorOverride = null, int fontSizeOverride = 0)
        {
            var t = UITheme.Instance;
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = t.GetFont();
            text.fontSize = fontSizeOverride > 0 ? fontSizeOverride : RoleToFontSize(t, role);
            text.alignment = anchor;
            text.color = colorOverride ?? RoleToTextColor(t, role);
            text.text = value;
            text.supportRichText = true;
            var rect = text.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return text;
        }

        public static Button CreateButton(Transform parent, string label, Vector2 pos, Vector2 size,
            Action onClick, ButtonVariant variant = ButtonVariant.Primary)
        {
            var t = UITheme.Instance;
            var go = new GameObject("Btn");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = VariantToColor(t, variant);
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            CreateText(go.transform, label, TextRole.Body, TextAnchor.MiddleCenter,
                Vector2.zero, size, colorOverride: t.textPrimary);
            return btn;
        }

        public static (GameObject root, Text label) CreateListItem(Transform parent, string text,
            float height = 92f, Color? bgColor = null, Action onClick = null, float textWidth = 0f)
        {
            var t = UITheme.Instance;
            var go = new GameObject("ListItem");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = bgColor ?? t.surfaceRaised;
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            var lbl = MakeItemText(go, text, TextAnchor.MiddleLeft, t);
            return (go, lbl);
        }

        public static (GameObject root, Text label) CreateActionItem(Transform parent, string text,
            float height = 92f, Color? bgColor = null, Action onClick = null, float textWidth = 0f)
        {
            var t = UITheme.Instance;
            var go = new GameObject("ActionItem");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = bgColor ?? t.surfaceRaised;
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            var lbl = MakeItemText(go, text, TextAnchor.MiddleCenter, t);
            return (go, lbl);
        }

        private static Text MakeItemText(GameObject parent, string value, TextAnchor alignment,
            UITheme t)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent.transform, false);
            var txt = go.AddComponent<Text>();
            txt.font = t.GetFont();
            txt.fontSize = t.fontSizeBody;
            txt.alignment = alignment;
            txt.color = t.textPrimary;
            txt.text = value;
            txt.supportRichText = true;
            var rect = txt.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(HorizPad, 4f);
            rect.offsetMax = new Vector2(-HorizPad, -4f);
            return txt;
        }

        public static Transform CreateVerticalList(Transform parent, Vector2 pos, Vector2 size,
            float spacing = 6f)
        {
            var go = new GameObject("VList");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            var vg = go.AddComponent<VerticalLayoutGroup>();
            vg.spacing = spacing;
            vg.childControlHeight = true;
            vg.childControlWidth = true;
            vg.childForceExpandHeight = false;
            vg.childForceExpandWidth = true;
            return go.transform;
        }

        public static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child != null) UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        public static Color GradeColor(int grade) => UITheme.Instance.GetGradeColor(grade);

        private static int RoleToFontSize(UITheme t, TextRole role) => role switch
        {
            TextRole.Title   => t.fontSizeTitle,
            TextRole.Heading => t.fontSizeHeading,
            TextRole.Body    => t.fontSizeBody,
            TextRole.Caption => t.fontSizeCaption,
            _                => t.fontSizeLabel,
        };

        private static Color RoleToTextColor(UITheme t, TextRole role) => role switch
        {
            TextRole.Title   => t.textPrimary,
            TextRole.Heading => t.textPrimary,
            TextRole.Body    => t.textSecondary,
            TextRole.Caption => t.textMuted,
            _                => t.textMuted,
        };

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
    }
}
