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
    public sealed class BattleHUD : MonoBehaviour
    {
        private BattleEventBus eventBus;
        private BattleRoster roster;
        private Func<string, Vector3> getWorldPos;

        private Canvas overlayCanvas;
        private RectTransform canvasRect;
        private Image progressFill;
        private Text waveLabel;
        private Text battleLogText;

        private readonly Queue<string> logLines = new();
        private const int MaxLogLines = 18;

        public void Setup(
            BattleEventBus eventBus,
            BattleRoster roster,
            Func<string, Vector3> getWorldPos,
            Action onRetreat,
            Action onTogglePause)
        {
            this.eventBus = eventBus;
            this.roster = roster;
            this.getWorldPos = getWorldPos;

            BuildUI(onRetreat, onTogglePause);
            eventBus.OnPublished += OnBattleEvent;
        }

        private void OnDestroy()
        {
            if (eventBus != null)
            {
                eventBus.OnPublished -= OnBattleEvent;
            }
        }

        /// <summary>매 프레임 호출. 턴 대기 진행도를 업데이트합니다.</summary>
        public void SetProgress(float elapsed, float interval)
        {
            if (progressFill == null)
            {
                return;
            }

            progressFill.fillAmount = interval > 0f ? Mathf.Clamp01(elapsed / interval) : 0f;
        }

        /// <summary>Wave 클리어 시 호출. 로그에 기록하고 Wave 카운터를 갱신합니다.</summary>
        public void OnWaveCleared(int clearedCount)
        {
            AddLog($"<color=#FFFFFF>── Wave {clearedCount} 클리어 ──</color>");
            if (waveLabel != null)
            {
                waveLabel.text = $"Wave {clearedCount + 1}";
            }
        }

        /// <summary>시스템 메시지를 전투 로그에 추가합니다.</summary>
        public void LogMessage(string richText)
        {
            AddLog(richText);
        }

        /// <summary>전투 패배 시 호출. 패배 오버레이를 표시합니다.</summary>
        public void ShowGameOver(Action onReturn)
        {
            AddLog("<color=#FF3333>── 전투 패배 ──</color>");

            var go = new GameObject("GameOverOverlay");
            go.transform.SetParent(overlayCanvas.transform, false);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);
            var bgRect = go.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;

            CreateText(go.transform, "전투 패배", 52, TextAnchor.MiddleCenter, new Vector2(0f, 70f), new Vector2(500f, 90f));
            CreateButton(go.transform, "현장으로 복귀", Vector2.zero, new Vector2(220f, 52f), onReturn);
        }

        /// <summary>파견 완료 시 결과 화면을 표시합니다.</summary>
        public void ShowVictory(
            int wavesCleared,
            int maxWaves,
            IReadOnlyList<DropRow> drops,
            IReadOnlyList<string> eventLogs,
            Action onReturn)
        {
            AddLog($"<color=#FFD700>── 파견 완료 ({wavesCleared}/{maxWaves} waves) ──</color>");

            // 전체를 덮는 반투명 배경
            var overlay = new GameObject("VictoryOverlay");
            overlay.transform.SetParent(overlayCanvas.transform, false);
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0.04f, 0.82f);
            var overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;

            // 결과 카드 (중앙)
            var card = new GameObject("ResultCard");
            card.transform.SetParent(overlay.transform, false);
            card.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.14f, 1f);
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(560f, 560f);
            cardRect.anchoredPosition = Vector2.zero;

            // 제목
            CreateText(card.transform, "Dispatch Complete", 28, TextAnchor.UpperCenter,
                new Vector2(0f, 248f), new Vector2(520f, 48f)).color = new Color(0.85f, 0.75f, 0.25f);

            // 웨이브 수
            CreateText(card.transform, $"Waves Cleared: {wavesCleared} / {maxWaves}", 16, TextAnchor.UpperCenter,
                new Vector2(0f, 200f), new Vector2(520f, 30f)).color = new Color(0.75f, 0.85f, 1f);

            // 드랍 목록
            var dropSummary = BuildDropSummary(drops);
            CreateText(card.transform, "Drops:\n" + (dropSummary.Length > 0 ? dropSummary : "(none)"),
                14, TextAnchor.UpperLeft, new Vector2(-250f, 158f), new Vector2(520f, 180f))
                .color = new Color(0.82f, 0.88f, 0.95f);

            // 이벤트 로그 (레벨업 등)
            var events = eventLogs.Count > 0
                ? string.Join("\n", eventLogs.Take(6))
                : "(none)";
            CreateText(card.transform, "Events:\n" + events,
                13, TextAnchor.UpperLeft, new Vector2(-250f, -60f), new Vector2(520f, 120f))
                .color = new Color(1f, 0.88f, 0.45f);

            // 복귀 버튼
            CreateButton(card.transform, "Return to Office",
                new Vector2(0f, -230f), new Vector2(240f, 52f), onReturn);
        }

        private static string BuildDropSummary(IReadOnlyList<DropRow> drops)
        {
            if (drops == null || drops.Count == 0)
            {
                return string.Empty;
            }

            // 장비는 개별 표시, 재료는 합산
            var lines = new List<string>();
            var materials = new Dictionary<string, int>();
            foreach (var d in drops)
            {
                if (d.IsEquipment)
                {
                    var tier = d.Grade switch { 1 => "Normal", 2 => "Rare", 3 => "Elite",
                        4 => "Epic", 5 => "Legend", _ => $"G{d.Grade}" };
                    lines.Add($"[{tier}] {d.ItemName} +{d.StatValue}");
                }
                else
                {
                    materials.TryGetValue(d.ItemName, out var cnt);
                    materials[d.ItemName] = cnt + 1;
                }
            }

            foreach (var kv in materials)
            {
                lines.Add($"{kv.Key} x{kv.Value}");
            }

            return string.Join("\n", lines.Take(8));
        }

        // ────────────────────── UI 빌드 ──────────────────────

        private void BuildUI(Action onRetreat, Action onTogglePause)
        {
            // 메인 Canvas (ScreenSpaceOverlay, 세로 모드 1080×1920)
            var canvasGO = new GameObject("BattleCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f; // 너비 기준
            canvasGO.AddComponent<GraphicRaycaster>();
            overlayCanvas = canvas;
            canvasRect = canvas.GetComponent<RectTransform>();

            BuildProgressBar();
            BuildBattleLog();
            BuildButtons(onRetreat, onTogglePause);
        }

        private void BuildProgressBar()
        {
            // 배경 (상단 전체 너비 스트레치)
            var bgGO = new GameObject("ProgressBG");
            bgGO.transform.SetParent(overlayCanvas.transform, false);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.88f);
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 1f);
            bgRect.anchorMax = new Vector2(1f, 1f);
            bgRect.pivot = new Vector2(0.5f, 1f);
            bgRect.sizeDelta = new Vector2(0f, 42f);
            bgRect.anchoredPosition = Vector2.zero;

            // 채움 이미지
            var fillGO = new GameObject("ProgressFill");
            fillGO.transform.SetParent(bgGO.transform, false);
            progressFill = fillGO.AddComponent<Image>();
            progressFill.color = new Color(0.2f, 0.65f, 1f, 0.9f);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFill.fillAmount = 0f;
            progressFill.raycastTarget = false;
            var fillRect = fillGO.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            // Wave 라벨 (우측)
            waveLabel = CreateText(bgGO.transform, "Wave 1", 16, TextAnchor.MiddleRight, Vector2.zero, new Vector2(160f, 38f));
            var waveLabelRect = waveLabel.GetComponent<RectTransform>();
            waveLabelRect.anchorMin = new Vector2(1f, 0f);
            waveLabelRect.anchorMax = new Vector2(1f, 1f);
            waveLabelRect.pivot = new Vector2(1f, 0.5f);
            waveLabelRect.sizeDelta = new Vector2(160f, 0f);
            waveLabelRect.anchoredPosition = new Vector2(-12f, 0f);
        }

        private void BuildBattleLog()
        {
            var logPanelGO = new GameObject("LogPanel");
            logPanelGO.transform.SetParent(overlayCanvas.transform, false);
            var logPanelImg = logPanelGO.AddComponent<Image>();
            logPanelImg.color = new Color(0f, 0f, 0f, 0.55f);
            var logPanelRect = logPanelGO.GetComponent<RectTransform>();
            logPanelRect.anchorMin = new Vector2(0f, 0f);
            logPanelRect.anchorMax = new Vector2(0.72f, 0f);
            logPanelRect.pivot = new Vector2(0f, 0f);
            logPanelRect.sizeDelta = new Vector2(0f, 320f);
            logPanelRect.anchoredPosition = Vector2.zero;

            battleLogText = CreateText(logPanelGO.transform, string.Empty, 13, TextAnchor.LowerLeft, Vector2.zero, Vector2.zero);
            battleLogText.supportRichText = true;
            battleLogText.verticalOverflow = VerticalWrapMode.Overflow;
            var logTextRect = battleLogText.GetComponent<RectTransform>();
            logTextRect.anchorMin = Vector2.zero;
            logTextRect.anchorMax = Vector2.one;
            logTextRect.sizeDelta = new Vector2(-20f, -10f);
            logTextRect.anchoredPosition = new Vector2(10f, 5f);
        }

        private void BuildButtons(Action onRetreat, Action onTogglePause)
        {
            // 후퇴 버튼 (하단 우측)
            CreateCornerButton("후퇴", new Vector2(-12f, 12f), new Vector2(180f, 72f), onRetreat);
            // 일시정지 버튼 (그 위)
            CreateCornerButton("일시정지", new Vector2(-12f, 92f), new Vector2(180f, 72f), onTogglePause);
        }

        /// <summary>하단 우측 앵커 기준 버튼을 생성합니다.</summary>
        private void CreateCornerButton(string label, Vector2 offset, Vector2 size, Action onClick)
        {
            var go = new GameObject(label + "Button");
            go.transform.SetParent(overlayCanvas.transform, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.28f, 0.95f);
            var button = go.AddComponent<Button>();
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;

            CreateText(go.transform, label, 17, TextAnchor.MiddleCenter, Vector2.zero, size);
        }

        // ────────────────────── 이벤트 처리 ──────────────────────

        private void OnBattleEvent(BattleEvent e)
        {
            switch (e.Type)
            {
                case BattleEventType.TurnStarted:
                {
                    var tag = IsAlly(e.SourceRuntimeId)
                        ? "<color=#33FF88>[아군]</color>"
                        : "<color=#FF5555>[적군]</color>";
                    AddLog($"{tag} {e.SourceRuntimeId} → {e.TargetRuntimeId}");
                    break;
                }
                case BattleEventType.Damaged:
                {
                    var col = IsAlly(e.TargetRuntimeId) ? new Color(1f, 0.3f, 0.3f) : new Color(1f, 0.75f, 0.2f);
                    AddLog($"  {e.TargetRuntimeId} <color=#{Hex(col)}>-{e.Value}</color>");
                    SpawnFloating(e.TargetRuntimeId, $"-{e.Value}", col);
                    break;
                }
                case BattleEventType.Healed:
                {
                    var col = new Color(0.3f, 1f, 0.5f);
                    AddLog($"  {e.TargetRuntimeId} <color=#{Hex(col)}>+{e.Value} 회복</color>");
                    SpawnFloating(e.TargetRuntimeId, $"+{e.Value}", col);
                    break;
                }
                case BattleEventType.Evaded:
                {
                    var col = new Color(0.45f, 0.85f, 1f);
                    AddLog($"  {e.TargetRuntimeId} <color=#{Hex(col)}>회피</color>");
                    SpawnFloating(e.TargetRuntimeId, "EVADE", col);
                    break;
                }
                case BattleEventType.ThornsReflected:
                {
                    var col = new Color(0.8f, 0.35f, 1f);
                    AddLog($"  가시 → {e.TargetRuntimeId} <color=#{Hex(col)}>-{e.Value}</color>");
                    SpawnFloating(e.TargetRuntimeId, $"THORN -{e.Value}", col);
                    break;
                }
                case BattleEventType.CounterAttacked:
                {
                    var col = new Color(1f, 0.55f, 0.1f);
                    AddLog($"  반격 → {e.TargetRuntimeId} <color=#{Hex(col)}>-{e.Value}</color>");
                    SpawnFloating(e.TargetRuntimeId, $"반격 -{e.Value}", col);
                    break;
                }
                case BattleEventType.Died:
                    AddLog($"  <color=#888888>{e.TargetRuntimeId} 사망</color>");
                    break;
                case BattleEventType.ActiveSkillUsed:
                {
                    var tag = IsAlly(e.SourceRuntimeId) ? "<color=#33FF88>" : "<color=#FF5555>";
                    AddLog($"{tag}{e.SourceRuntimeId} 스킬 발동!</color>");
                    break;
                }
            }
        }

        private void AddLog(string line)
        {
            logLines.Enqueue(line);
            while (logLines.Count > MaxLogLines)
            {
                logLines.Dequeue();
            }

            if (battleLogText != null)
            {
                battleLogText.text = string.Join("\n", logLines);
            }
        }

        private void SpawnFloating(string runtimeId, string text, Color color)
        {
            if (getWorldPos == null || overlayCanvas == null)
            {
                return;
            }

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

        private static Text CreateText(Transform parent, string value, int fontSize, TextAnchor anchor, Vector2 pos, Vector2 size)
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

        private static void CreateButton(Transform parent, string label, Vector2 pos, Vector2 size, Action onClick)
        {
            var go = new GameObject(label + "Button");
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.22f, 0.28f, 0.95f);
            var button = go.AddComponent<Button>();
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            CreateText(go.transform, label, 18, TextAnchor.MiddleCenter, Vector2.zero, size);
        }
    }
}
