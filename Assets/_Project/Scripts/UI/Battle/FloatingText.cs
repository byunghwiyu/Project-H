using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectH.UI.Battle
{
    public sealed class FloatingText : MonoBehaviour
    {
        private const float TotalDuration = 1.0f;
        private const float ScaleInDuration = 0.12f;
        private const float FloatDistance = 90f;

        /// <summary>
        /// 월드 좌표 기준으로 플로팅 텍스트를 Overlay Canvas 위에 스폰합니다.
        /// </summary>
        public static void Spawn(Canvas canvas, RectTransform canvasRect,
            Vector3 worldPos, string text, Color color, bool large = false)
        {
            var go = new GameObject("FloatingText");
            go.transform.SetParent(canvasRect, false);

            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = large ? 32 : 24;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            label.supportRichText = false;

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 48f);

            // 월드 → 스크린 → Canvas 로컬 좌표 변환
            var screenPos = RectTransformUtility.WorldToScreenPoint(Camera.main, worldPos);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main,
                out var localPos);
            rect.anchoredPosition = localPos + Vector2.up * 20f;

            var ft = go.AddComponent<FloatingText>();
            ft.StartCoroutine(ft.Animate(rect, label));
        }

        private IEnumerator Animate(RectTransform rect, Text label)
        {
            if (rect == null) yield break;

            var startPos = rect.anchoredPosition;
            var startColor = label.color;

            // ① 스케일 팝인: 1.6x → 1.0x
            var elapsed = 0f;
            rect.localScale = new Vector3(1.6f, 1.6f, 1f);
            while (elapsed < ScaleInDuration)
            {
                if (rect == null) yield break;
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / ScaleInDuration);
                rect.localScale = Vector3.Lerp(new Vector3(1.6f, 1.6f, 1f), Vector3.one, t);
                yield return null;
            }

            rect.localScale = Vector3.one;

            // ② 위로 떠오르며 페이드아웃
            var floatElapsed = 0f;
            var floatDuration = TotalDuration - ScaleInDuration;
            while (floatElapsed < floatDuration)
            {
                if (rect == null) yield break;
                floatElapsed += Time.deltaTime;
                var p = floatElapsed / floatDuration;
                rect.anchoredPosition = startPos + Vector2.up * (FloatDistance * p);
                label.color = new Color(startColor.r, startColor.g, startColor.b,
                    Mathf.Clamp01(1f - p * 1.1f));
                yield return null;
            }

            if (rect != null) Destroy(rect.gameObject);
        }
    }
}
