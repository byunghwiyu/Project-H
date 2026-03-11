using System.Collections;
using UnityEngine;

namespace ProjectH.UI.Battle
{
    public sealed class UnitView : MonoBehaviour
    {
        [SerializeField] private string runtimeUnitId;
        [SerializeField] private string templateId;
        [SerializeField] private bool isEnemy;

        [Header("Combat Motion")]
        [SerializeField] private Transform attackReceivePoint;
        [SerializeField] private float approachGap = 0.2f;
        [SerializeField] private float approachYOffset = 0f;
        [SerializeField] private float approachSpeed = 9f;
        [SerializeField] private float retreatSpeed = 10f;
        [SerializeField] private float attackHoldSec = 0.08f;

        private static readonly int AttackTrigger = Animator.StringToHash("Attack");
        private static readonly int HitTrigger = Animator.StringToHash("Hit");
        private static readonly int DieTrigger = Animator.StringToHash("Die");

        // Refs
        private Animator animator;
        private SpriteRenderer mainRenderer;
        private Color originalColor;

        // Gauge bar fills (SpriteRenderer-based, world space)
        private const float BarWidth = 0.9f;
        private const float HpBarLocalY = 0.06f;
        private const float MpBarLocalY = -0.10f;
        private SpriteRenderer hpFill;
        private SpriteRenderer mpFill;

        // Coroutine guards
        private Coroutine flashCoroutine;

        public string RuntimeUnitId => runtimeUnitId;
        public float ApproachGap => Mathf.Max(0f, approachGap);
        public float ApproachYOffset => approachYOffset;
        public float ApproachSpeed => Mathf.Max(0.01f, approachSpeed);
        public float RetreatSpeed => Mathf.Max(0.01f, retreatSpeed);
        public float AttackHoldSec => Mathf.Max(0f, attackHoldSec);

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        public void Bind(string runtimeId, string template, bool enemy)
        {
            runtimeUnitId = runtimeId;
            templateId = template;
            isEnemy = enemy;
            name = runtimeId;

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            // Cache the unit's main sprite renderer (root first, then children)
            mainRenderer = GetComponent<SpriteRenderer>();
            if (mainRenderer == null)
            {
                mainRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (mainRenderer != null)
            {
                originalColor = mainRenderer.color;
            }

            BuildBars();
        }

        // ── HP / MP bars ────────────────────────────────────────────

        private void BuildBars()
        {
            var barSprite = BuildBarSprite();

            var barRoot = new GameObject("Bars");
            barRoot.transform.SetParent(transform, false);
            barRoot.transform.localPosition = new Vector3(0f, 0.72f, 0f);

            // HP bar: background + fill
            CreateBarSR(barRoot.transform, "HPBarBG",
                new Color(0.1f, 0.1f, 0.1f, 0.85f),
                new Vector3(0f, HpBarLocalY, 0f),
                new Vector3(BarWidth, 0.10f, 1f),
                barSprite, sortOrder: 11);

            hpFill = CreateBarSR(barRoot.transform, "HPBarFill",
                new Color(0.25f, 0.85f, 0.3f, 0.9f),
                new Vector3(0f, HpBarLocalY, 0f),
                new Vector3(BarWidth, 0.09f, 1f),
                barSprite, sortOrder: 12);

            // MP bar: background + fill
            CreateBarSR(barRoot.transform, "MPBarBG",
                new Color(0.05f, 0.05f, 0.18f, 0.85f),
                new Vector3(0f, MpBarLocalY, 0f),
                new Vector3(BarWidth, 0.07f, 1f),
                barSprite, sortOrder: 11);

            mpFill = CreateBarSR(barRoot.transform, "MPBarFill",
                new Color(0.2f, 0.5f, 1f, 0.9f),
                new Vector3(0f, MpBarLocalY, 0f),
                new Vector3(BarWidth, 0.065f, 1f),
                barSprite, sortOrder: 12);
        }

        private static SpriteRenderer CreateBarSR(Transform parent, string objName, Color color,
            Vector3 localPos, Vector3 localScale, Sprite sprite, int sortOrder)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortOrder;
            return sr;
        }

        private static Sprite BuildBarSprite()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color[16];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }

        /// <summary>HP / MP 수치를 받아 게이지바를 갱신합니다.</summary>
        public void UpdateStats(int hp, int maxHp, int mp, int maxMp)
        {
            // HP fill width + color (green → red)
            if (hpFill != null)
            {
                var ratio = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
                SetBarFill(hpFill, ratio, BarWidth, HpBarLocalY);
                hpFill.color = Color.Lerp(
                    new Color(0.95f, 0.15f, 0.1f, 0.9f),   // red (low)
                    new Color(0.25f, 0.85f, 0.3f, 0.9f),    // green (full)
                    ratio);
            }

            // MP fill width
            if (mpFill != null)
            {
                var ratio = maxMp > 0 ? Mathf.Clamp01((float)mp / maxMp) : 0f;
                SetBarFill(mpFill, ratio, BarWidth, MpBarLocalY);
            }
        }

        private static void SetBarFill(SpriteRenderer fill, float ratio, float barWidth, float localY)
        {
            // Scale X to represent ratio, offset X to keep left-aligned
            var fillWidth = barWidth * ratio;
            var offsetX = -barWidth * (1f - ratio) * 0.5f;
            var s = fill.transform.localScale;
            fill.transform.localScale = new Vector3(fillWidth, s.y, s.z);
            var p = fill.transform.localPosition;
            fill.transform.localPosition = new Vector3(offsetX, localY, p.z);
        }

        // ── Combat events ────────────────────────────────────────────

        public void OnTurnStarted()
        {
            SetTriggerSafe(AttackTrigger);
            StartCoroutine(AttackPulse());
        }

        public void OnDamaged(int damage)
        {
            SetTriggerSafe(HitTrigger);
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(HitFlash());
        }

        public void OnDied()
        {
            SetTriggerSafe(DieTrigger);
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            StartCoroutine(DeathFade());
        }

        // ── Visual effects ────────────────────────────────────────────

        /// <summary>공격 시 유닛이 살짝 커졌다 돌아오는 펄스 효과.</summary>
        private IEnumerator AttackPulse()
        {
            const float halfDur = 0.08f;
            var original = transform.localScale;
            var peak = original * 1.18f;
            var t = 0f;

            while (t < halfDur)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(original, peak, t / halfDur);
                yield return null;
            }

            t = 0f;
            while (t < halfDur)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(peak, original, t / halfDur);
                yield return null;
            }

            transform.localScale = original;
        }

        /// <summary>피격 시 빨간색으로 플래시 후 원래 색 복귀.</summary>
        private IEnumerator HitFlash()
        {
            if (mainRenderer == null) yield break;

            const float duration = 0.22f;
            var hitColor = new Color(1f, 0.15f, 0.15f, 1f);
            mainRenderer.color = hitColor;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (mainRenderer == null) yield break;
                mainRenderer.color = Color.Lerp(hitColor, originalColor, elapsed / duration);
                yield return null;
            }

            if (mainRenderer != null) mainRenderer.color = originalColor;
        }

        /// <summary>사망 시 회색으로 페이드 아웃 후 비활성화.</summary>
        private IEnumerator DeathFade()
        {
            const float duration = 0.45f;
            var deadColor = new Color(0.35f, 0.35f, 0.35f, 0f);
            var startColor = mainRenderer != null ? mainRenderer.color : Color.white;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (mainRenderer != null)
                {
                    mainRenderer.color = Color.Lerp(startColor, deadColor, elapsed / duration);
                }
                yield return null;
            }

            gameObject.SetActive(false);
        }

        // ── Approach point ────────────────────────────────────────────

        public Vector3 GetApproachPointForAttacker(UnitView attacker, float gap, float yOffset)
        {
            if (attackReceivePoint != null)
            {
                var fixedPoint = attackReceivePoint.position;
                fixedPoint.y += yOffset;
                if (attacker != null)
                {
                    fixedPoint.z = attacker.transform.position.z;
                }

                return fixedPoint;
            }

            var attackerX = attacker != null ? attacker.transform.position.x : transform.position.x - 1f;
            var dir = Mathf.Sign(attackerX - transform.position.x);
            if (Mathf.Approximately(dir, 0f))
            {
                dir = -1f;
            }

            var attackerHalf = attacker != null ? attacker.GetHalfWidth() : 0.5f;
            var dist = GetHalfWidth() + attackerHalf + Mathf.Max(0f, gap);
            var p = transform.position + new Vector3(dir * dist, yOffset, 0f);

            if (attacker != null)
            {
                p.z = attacker.transform.position.z;
            }

            return p;
        }

        private float GetHalfWidth()
        {
            var r = GetComponentInChildren<Renderer>();
            if (r != null)
            {
                return Mathf.Max(0.05f, r.bounds.extents.x);
            }

            return 0.5f;
        }

        // ── Animator ────────────────────────────────────────────

        private void SetTriggerSafe(int triggerHash)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            animator.SetTrigger(triggerHash);
        }
    }
}
