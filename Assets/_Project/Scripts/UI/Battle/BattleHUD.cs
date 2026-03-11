using System;
using System.Collections.Generic;
using System.Linq;
using ProjectH.Battle;
using ProjectH.Core;
using ProjectH.Data.Tables;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectH.UI.Battle
{
    /// <summary>
    /// 전투 뷰어 HUD. 참고 레이아웃:
    ///   상단: 현장 이름 헤더
    ///   중앙: 카메라 뷰포트 (전투 장면)
    ///   뷰포트 아래: Wave/프로그레스 바
    ///   그 아래: 전투 로그
    ///   하단: 후퇴하기 / 닫기 버튼
    /// </summary>
    public sealed class BattleHUD : MonoBehaviour
    {
        private BattleEventBus eventBus;
        private BattleRoster roster;
        private Func<string, Vector3> getWorldPos;
        private Func<string, string> nameResolver;

        private Canvas overlayCanvas;
        private RectTransform canvasRect;
        private Image progressFill;
        private Text waveLabel;
        private Text battleLogText;

        private readonly Queue<string> logLines = new();
        private const int MaxLogLines = 6;

        // 뷰포트 영역 (1080x1920 기준 정규화 앵커)
        private const float ViewportTop = 0.96f;
        private const float ViewportBottom = 0.42f;
        private const float ViewportLeft = 0.02f;
        private const float ViewportRight = 0.98f;

        public void Setup(
            BattleEventBus eventBus,
            BattleRoster roster,
            Func<string, Vector3> getWorldPos,
            Func<string, string> nameResolver,
            string locationName,
            Action onRetreat,
            Action onClose)
        {
            this.eventBus = eventBus;
            this.roster = roster;
            this.getWorldPos = getWorldPos;
            this.nameResolver = nameResolver;

            BuildUI(locationName, onRetreat, onClose);
            eventBus.OnPublished += OnBattleEvent;
        }

        private void OnDestroy()
        {
            if (eventBus != null)
                eventBus.OnPublished -= OnBattleEvent;
        }

        public void SetProgress(float elapsed, float interval)
        {
            if (progressFill == null) return;
            progressFill.fillAmount = interval > 0f ? Mathf.Clamp01(elapsed / interval) : 0f;
        }

        public void OnWaveCleared(int clearedCount)
        {
            AddLog($"<color=#FFFFFF>-- Wave {clearedCount} 클리어 --</color>");
            if (waveLabel != null)
                waveLabel.text = $"Wave {clearedCount + 1}";
        }

        public void LogMessage(string richText) => AddLog(richText);

        public void ShowGameOver(Action onReturn)
        {
            AddLog("<color=#FF3333>-- 전투 패배 --</color>");
            ShowOverlay("전투 패배", new Color(1f, 0.3f, 0.3f), onReturn);
        }

        public void ShowVictory(
            int wavesCleared, int maxWaves,
            IReadOnlyList<DropRow> drops,
            IReadOnlyList<string> eventLogs,
            Action onReturn)
        {
            AddLog($"<color=#FFD700>-- 파견 완료 ({wavesCleared}/{maxWaves} waves) --</color>");
            ShowOverlay("파견 완료", new Color(0.85f, 0.75f, 0.25f), onReturn);
        }

        private void ShowOverlay(string title, Color titleColor, Action onReturn)
        {
            var go = new GameObject("EndOverlay");
            go.transform.SetParent(overlayCanvas.transform, false);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);
            var bgRect = go.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            var titleTxt = CreateText(go.transform, title, 48, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(600f, 80f));
            titleTxt.color = titleColor;

            var btnGo = new GameObject("ReturnBtn");
            btnGo.transform.SetParent(go.transform, false);
            btnGo.AddComponent<Image>().color = new Color(0.18f, 0.22f, 0.28f, 0.95f);
            btnGo.AddComponent<Button>().onClick.AddListener(() => onReturn?.Invoke());
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(280f, 64f);
            btnRect.anchoredPosition = new Vector2(0f, -70f);
            CreateText(btnGo.transform, "현장으로 복귀", 22, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(280f, 64f));
        }

        // ────────────────────── UI 빌드 ──────────────────────

        private void BuildUI(string locationName, Action onRetreat, Action onClose)
        {
            var canvasGO = new GameObject("BattleCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            canvasGO.AddComponent<GraphicRaycaster>();
            overlayCanvas = canvas;
            canvasRect = canvas.GetComponent<RectTransform>();

            // 뷰포트 밖 영역만 배경으로 채움 (뷰포트 위쪽 + 아래쪽)
            BuildBackground();

            BuildLocationHeader(locationName);
            BuildViewportBorderStrips();
            BuildWaveBar();
            BuildBattleLog();
            BuildBottomButtons(onRetreat, onClose);
        }

        /// <summary>뷰포트 영역을 제외한 상/하 배경</summary>
        private void BuildBackground()
        {
            var dark = new Color(0.08f, 0.08f, 0.10f, 1f);

            // 상단 배경 (뷰포트 위 ~ 화면 꼭대기)
            var topBg = CreatePanel(overlayCanvas.transform, "TopBG", dark);
            var topRect = topBg.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, ViewportTop);
            topRect.anchorMax = Vector2.one;
            topRect.offsetMin = Vector2.zero;
            topRect.offsetMax = Vector2.zero;

            // 하단 배경 (화면 바닥 ~ 뷰포트 아래)
            var botBg = CreatePanel(overlayCanvas.transform, "BottomBG", dark);
            var botRect = botBg.GetComponent<RectTransform>();
            botRect.anchorMin = Vector2.zero;
            botRect.anchorMax = new Vector2(1f, ViewportBottom);
            botRect.offsetMin = Vector2.zero;
            botRect.offsetMax = Vector2.zero;
        }

        /// <summary>상단 현장 이름 헤더</summary>
        private void BuildLocationHeader(string locationName)
        {
            var headerGo = CreatePanel(overlayCanvas.transform, "LocationHeader",
                new Color(0.08f, 0.08f, 0.10f, 1f));
            var headerRect = headerGo.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, ViewportTop);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.offsetMin = Vector2.zero;
            headerRect.offsetMax = Vector2.zero;

            var nameTxt = CreateText(headerGo.transform, locationName, 28, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.zero);
            var nameRect = nameTxt.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.offsetMin = new Vector2(24f, 0f);
            nameRect.offsetMax = new Vector2(-24f, 0f);
            nameTxt.fontStyle = FontStyle.Bold;
        }

        /// <summary>뷰포트 주변 4면 얇은 테두리 (카메라 영역을 가리지 않음)</summary>
        private void BuildViewportBorderStrips()
        {
            var borderColor = new Color(0.25f, 0.25f, 0.28f, 1f);
            const float t = 0.003f; // 테두리 두께 (정규화)

            // 상 (뷰포트 위쪽 얇은 선)
            BuildBorderStrip("BorderTop", borderColor,
                new Vector2(ViewportLeft - t, ViewportTop),
                new Vector2(ViewportRight + t, ViewportTop + t));
            // 하
            BuildBorderStrip("BorderBottom", borderColor,
                new Vector2(ViewportLeft - t, ViewportBottom - t),
                new Vector2(ViewportRight + t, ViewportBottom));
            // 좌
            BuildBorderStrip("BorderLeft", borderColor,
                new Vector2(ViewportLeft - t, ViewportBottom - t),
                new Vector2(ViewportLeft, ViewportTop + t));
            // 우
            BuildBorderStrip("BorderRight", borderColor,
                new Vector2(ViewportRight, ViewportBottom - t),
                new Vector2(ViewportRight + t, ViewportTop + t));
        }

        private void BuildBorderStrip(string objName, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = CreatePanel(overlayCanvas.transform, objName, color);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().raycastTarget = false;
        }

        /// <summary>뷰포트 바로 아래: Wave 프로그레스 바</summary>
        private void BuildWaveBar()
        {
            const float barTopAnchor = ViewportBottom;
            const float barBottomAnchor = barTopAnchor - 0.018f; // ~34px

            var barBgGo = CreatePanel(overlayCanvas.transform, "WaveBar",
                new Color(0.12f, 0.12f, 0.14f, 1f));
            var barBgRect = barBgGo.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(ViewportLeft, barBottomAnchor);
            barBgRect.anchorMax = new Vector2(ViewportRight, barTopAnchor);
            barBgRect.offsetMin = Vector2.zero;
            barBgRect.offsetMax = Vector2.zero;

            // Fill
            var fillGo = CreatePanel(barBgGo.transform, "ProgressFill",
                new Color(0.2f, 0.65f, 1f, 0.9f));
            progressFill = fillGo.GetComponent<Image>();
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFill.fillAmount = 0f;
            progressFill.raycastTarget = false;
            Stretch(fillGo.GetComponent<RectTransform>());

            // Wave label (우측)
            waveLabel = CreateText(barBgGo.transform, "Wave 1", 16, TextAnchor.MiddleRight,
                Vector2.zero, Vector2.zero);
            var waveLabelRect = waveLabel.GetComponent<RectTransform>();
            waveLabelRect.anchorMin = new Vector2(0.7f, 0f);
            waveLabelRect.anchorMax = new Vector2(1f, 1f);
            waveLabelRect.offsetMin = new Vector2(0f, 0f);
            waveLabelRect.offsetMax = new Vector2(-12f, 0f);
        }

        /// <summary>전투 로그 영역</summary>
        private void BuildBattleLog()
        {
            const float logTop = ViewportBottom - 0.018f; // wave bar 아래
            const float logBottom = logTop - 0.10f; // ~192px

            var logPanelGo = CreatePanel(overlayCanvas.transform, "LogPanel",
                new Color(0.06f, 0.06f, 0.08f, 0.85f));
            var logPanelRect = logPanelGo.GetComponent<RectTransform>();
            logPanelRect.anchorMin = new Vector2(ViewportLeft, logBottom);
            logPanelRect.anchorMax = new Vector2(ViewportRight, logTop);
            logPanelRect.offsetMin = Vector2.zero;
            logPanelRect.offsetMax = Vector2.zero;

            battleLogText = CreateText(logPanelGo.transform, string.Empty, 18,
                TextAnchor.UpperLeft, Vector2.zero, Vector2.zero);
            battleLogText.supportRichText = true;
            battleLogText.verticalOverflow = VerticalWrapMode.Overflow;
            var logTextRect = battleLogText.GetComponent<RectTransform>();
            logTextRect.anchorMin = Vector2.zero;
            logTextRect.anchorMax = Vector2.one;
            logTextRect.offsetMin = new Vector2(16f, 8f);
            logTextRect.offsetMax = new Vector2(-16f, -8f);
        }

        /// <summary>하단 버튼: 후퇴하기 / 닫기</summary>
        private void BuildBottomButtons(Action onRetreat, Action onClose)
        {
            var btnSize = new Vector2(160f, 56f);
            var spacing = 20f;

            // 닫기 (우하단)
            BuildBottomButton("닫기", new Vector2(-24f, 24f), btnSize,
                new Color(0.2f, 0.2f, 0.24f, 0.95f), onClose);

            // 후퇴하기 (닫기 왼쪽)
            BuildBottomButton("후퇴하기", new Vector2(-24f - btnSize.x - spacing, 24f), btnSize,
                new Color(0.35f, 0.15f, 0.15f, 0.95f), onRetreat);
        }

        private void BuildBottomButton(string label, Vector2 offset, Vector2 size, Color bgColor, Action onClick)
        {
            var go = new GameObject(label + "Btn");
            go.transform.SetParent(overlayCanvas.transform, false);
            go.AddComponent<Image>().color = bgColor;
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
            CreateText(go.transform, label, 20, TextAnchor.MiddleCenter, Vector2.zero, size);
        }

        // ────────────────────── 이벤트 처리 ──────────────────────

        private void OnBattleEvent(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.TurnStarted:
                {
                    var srcName = ResolveName(e.SourceRuntimeId);
                    var tgtName = ResolveName(e.TargetRuntimeId);
                    var srcColor = IsAlly(e.SourceRuntimeId) ? "#33FF88" : "#FF5555";
                    var tgtColor = IsAlly(e.TargetRuntimeId) ? "#33FF88" : "#FF5555";
                    AddLog($"<color={srcColor}>{srcName}</color>이(가) <color={tgtColor}>{tgtName}</color>을(를) 공격합니다.");
                    break;
                }
                case BattleEventType.Damaged:
                {
                    var name = ResolveName(e.TargetRuntimeId);
                    var col = IsAlly(e.TargetRuntimeId) ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.75f, 0.2f);
                    AddLog($" <color=#{Hex(col)}>{name} -{e.Value}</color>");
                    SpawnFloating(e.TargetRuntimeId, $"-{e.Value}", col);
                    break;
                }
                case BattleEventType.Healed:
                {
                    var srcName = ResolveName(e.SourceRuntimeId);
                    var tgtName = ResolveName(e.TargetRuntimeId);
                    var col = new Color(0.3f, 1f, 0.5f);
                    AddLog($" <color=#{Hex(col)}>{srcName}이(가) {tgtName}의 HP를 {e.Value} 치유했습니다.</color>");
                    SpawnFloating(e.TargetRuntimeId, $"+{e.Value}", col);
                    break;
                }
                case BattleEventType.Evaded:
                {
                    var name = ResolveName(e.TargetRuntimeId);
                    var col = new Color(0.45f, 0.85f, 1f);
                    AddLog($" <color=#{Hex(col)}>{name} 회피!</color>");
                    SpawnFloating(e.TargetRuntimeId, "EVADE", col);
                    break;
                }
                case BattleEventType.ThornsReflected:
                {
                    var name = ResolveName(e.TargetRuntimeId);
                    var col = new Color(0.8f, 0.35f, 1f);
                    AddLog($" 가시 반사 -> <color=#{Hex(col)}>{name} -{e.Value}</color>");
                    SpawnFloating(e.TargetRuntimeId, $"THORN -{e.Value}", col);
                    break;
                }
                case BattleEventType.CounterAttacked:
                {
                    var name = ResolveName(e.TargetRuntimeId);
                    var col = new Color(1f, 0.55f, 0.1f);
                    AddLog($" 반격 -> <color=#{Hex(col)}>{name} -{e.Value}</color>");
                    SpawnFloating(e.TargetRuntimeId, $"반격 -{e.Value}", col);
                    break;
                }
                case BattleEventType.Died:
                {
                    var name = ResolveName(e.TargetRuntimeId);
                    AddLog($" <color=#888888>{name} 사망</color>");
                    break;
                }
                case BattleEventType.ActiveSkillUsed:
                {
                    var name = ResolveName(e.SourceRuntimeId);
                    var col = IsAlly(e.SourceRuntimeId) ? "#33FF88" : "#FF5555";
                    AddLog($"<color={col}>{name} 스킬 발동!</color>");
                    break;
                }
            }
        }

        private string ResolveName(string runtimeId)
        {
            if (nameResolver != null)
            {
                var name = nameResolver(runtimeId);
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
            return runtimeId;
        }

        private void AddLog(string line)
        {
            logLines.Enqueue(line);
            while (logLines.Count > MaxLogLines) logLines.Dequeue();
            if (battleLogText != null)
                battleLogText.text = string.Join("\n", logLines);
        }

        private void SpawnFloating(string runtimeId, string text, Color color)
        {
            if (getWorldPos == null || overlayCanvas == null) return;
            var worldPos = getWorldPos(runtimeId) + new Vector3(0f, 0.6f, 0f);
            FloatingText.Spawn(overlayCanvas, canvasRect, worldPos, text, color);
        }

        // ────────────────────── 유틸 ──────────────────────

        private bool IsAlly(string runtimeId)
        {
            return roster != null &&
                   roster.TryGetUnit(runtimeId, out var unit) &&
                   unit.Team == BattleTeam.Ally;
        }

        private static string Hex(Color c)
        {
            return $"{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}";
        }

        private static GameObject CreatePanel(Transform parent, string objName, Color color)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = color;
            go.AddComponent<RectTransform>();
            return go;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Text CreateText(Transform parent, string value, int fontSize,
            TextAnchor anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = value;
            text.raycastTarget = false;
            var rect = text.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return text;
        }
    }
}
