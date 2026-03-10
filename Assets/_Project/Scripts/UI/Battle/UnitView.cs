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

        private Animator animator;

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
        }

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

        public void OnTurnStarted()
        {
            SetTriggerSafe(AttackTrigger, "Attack");
        }

        public void OnDamaged(int damage)
        {
            SetTriggerSafe(HitTrigger, "Hit");
        }

        public void OnDied()
        {
            SetTriggerSafe(DieTrigger, "Die");
            gameObject.SetActive(false);
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

        private void SetTriggerSafe(int triggerHash, string triggerName)
        {
            if (animator == null)
            {
                Debug.LogWarning($"[battle] {name}: Animator missing, trigger '{triggerName}' skipped");
                return;
            }

            animator.SetTrigger(triggerHash);
        }
    }
}