using TMPro;
using UnityEngine;

namespace ProjectH.UI
{
    [CreateAssetMenu(fileName = "UITheme", menuName = "Project H/UI Theme")]
    public sealed class UITheme : ScriptableObject
    {
        private static UITheme instance;

        public static UITheme Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<UITheme>("UITheme");
                    if (instance == null)
                    {
                        instance = CreateInstance<UITheme>();
                    }
                }

                return instance;
            }
        }

        // ── Surfaces ──────────────────────────────────────────────────
        // Layer 0: page background
        public Color bg = new Color(0.06f, 0.07f, 0.09f, 1f);
        // Layer 1: panel / card
        public Color surface = new Color(0.10f, 0.12f, 0.15f, 1f);
        // Layer 2: elevated list item / raised card
        public Color surfaceRaised = new Color(0.14f, 0.17f, 0.21f, 1f);
        // Layer 3: border / separator
        public Color surfaceBorder = new Color(0.20f, 0.23f, 0.30f, 1f);
        // Overlay for modals
        public Color overlay = new Color(0.00f, 0.00f, 0.00f, 0.90f);

        // ── Semantic ──────────────────────────────────────────────────
        // Nav / primary action
        public Color primary = new Color(0.22f, 0.38f, 0.62f, 1f);
        // Confirm / equip / claim
        public Color success = new Color(0.17f, 0.40f, 0.22f, 1f);
        // Danger / unequip / remove
        public Color danger = new Color(0.48f, 0.18f, 0.18f, 1f);
        // Warning / in-progress
        public Color warning = new Color(0.18f, 0.22f, 0.40f, 1f);
        // Disabled / no-op button
        public Color muted = new Color(0.13f, 0.14f, 0.17f, 1f);

        // ── Text ──────────────────────────────────────────────────────
        public Color textPrimary = new Color(0.93f, 0.95f, 1.00f, 1f);
        public Color textSecondary = new Color(0.78f, 0.82f, 0.90f, 1f);
        public Color textMuted = new Color(0.60f, 0.64f, 0.72f, 1f);

        // ── Grade colors ──────────────────────────────────────────────
        public Color grade1 = new Color(0.55f, 0.57f, 0.63f, 1f);  // Normal  (gray)
        public Color grade2 = new Color(0.28f, 0.65f, 0.36f, 1f);  // Rare    (green)
        public Color grade3 = new Color(0.28f, 0.50f, 0.80f, 1f);  // Elite   (blue)
        public Color grade4 = new Color(0.62f, 0.30f, 0.78f, 1f);  // Epic    (purple)
        public Color grade5 = new Color(0.80f, 0.60f, 0.15f, 1f);  // Legend  (gold)

        // ── Typography (모바일 세로 1080×1920 기준) ───────────────────
        public int fontSizeTitle   = 36;
        public int fontSizeHeading = 28;
        public int fontSizeBody    = 22;
        public int fontSizeCaption = 18;
        public int fontSizeLabel   = 16;

        // ── Font ──────────────────────────────────────────────────────
        // TMP 폰트 에셋 (Font Asset Creator로 생성한 SDF 에셋 지정)
        [SerializeField] private TMP_FontAsset tmpFont;

        // Legacy Text용 (BattleHUD 등 아직 전환 안 된 곳)
        public string fontResourcePath = string.Empty;

        // ─────────────────────────────────────────────────────────────

        public TMP_FontAsset GetTMPFont() => tmpFont;

        public Font GetFont()
        {
            if (!string.IsNullOrWhiteSpace(fontResourcePath))
            {
                var custom = Resources.Load<Font>(fontResourcePath);
                if (custom != null) return custom;
            }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public Color GetGradeColor(int grade)
        {
            return grade switch
            {
                1 => grade1,
                2 => grade2,
                3 => grade3,
                4 => grade4,
                5 => grade5,
                _ => grade1,
            };
        }
    }
}
