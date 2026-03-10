using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectH.UI.Battle
{
    public sealed class FloatingText : MonoBehaviour
    {
        /// <summary>
        /// 월드 좌표 기준으로 플로팅 텍스트를 Overlay Canvas 위에 스폰합니다.
        /// </summary>
        public static void Spawn(Canvas canvas, RectTransform canvasRect, Vector3 worldPos, string text, Color color)
        {
            var go = new GameObject("FloatingText");
            go.transform.SetParent(canvasRect, false);

            // Text 먼저 추가 → RectTransform 자동 생성
            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            label.supportRichText = false;

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180f, 38f);

            // 월드 → 스크린 → Canvas 로컬 좌표 변환
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;
            var screenPos = RectTransformUtility.WorldToScreenPoint(Camera.main, worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, cam, out var localPos);
            rect.anchoredPosition = localPos + Vector2.up * 30f;

            var ft = go.AddComponent<FloatingText>();
            ft.StartCoroutine(ft.Animate(rect, label));
        }

        private IEnumerator Animate(RectTransform rect, Text label)
        {
            const float duration = 0.85f;
            var elapsed = 0f;
            var startPos = rect.anchoredPosition;
            var startColor = label.color;

            while (elapsed < duration)
            {
                if (rect == null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                var p = elapsed / duration;
                rect.anchoredPosition = startPos + Vector2.up * (65f * p);
                label.color = new Color(startColor.r, startColor.g, startColor.b, 1f - p);
                yield return null;
            }

            if (rect != null)
            {
                Destroy(rect.gameObject);
            }
        }
    }
}
