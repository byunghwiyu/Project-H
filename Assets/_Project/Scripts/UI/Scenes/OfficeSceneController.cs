using System;
using System.Collections.Generic;
using ProjectH.Account;
using ProjectH.Core;
using ProjectH.Data.Tables;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectH.UI.Scenes
{
    public sealed class OfficeSceneController : MonoBehaviour
    {
        // ── 레이아웃 참조 ──────────────────────────────
        private Transform mercListRoot;
        private Text creditText;

        // 상세 패널
        private GameObject detailPanel;
        private Text detailNameText;
        private Text detailStatsText;
        private Text detailExpText;
        private Text detailPromotionStatus;
        private Transform promotionButtonRoot;

        // 현재 선택된 용병 templateId
        private string selectedTemplateId;

        // 타이머 갱신용
        private float timerRefreshElapsed;
        private const float TimerRefreshInterval = 1f;

        // ── 라이프사이클 ──────────────────────────────

        private void Start()
        {
            BuildUI();
            RefreshList();
        }

        private void Update()
        {
            timerRefreshElapsed += Time.deltaTime;
            if (timerRefreshElapsed >= TimerRefreshInterval)
            {
                timerRefreshElapsed = 0f;
                if (!string.IsNullOrEmpty(selectedTemplateId) && PlayerAccountService.IsPromoting(selectedTemplateId))
                {
                    RefreshDetail(selectedTemplateId);
                }
            }
        }

        // ── UI 빌드 ──────────────────────────────────

        private void BuildUI()
        {
            var canvas = new GameObject("OfficeCanvas").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.gameObject.AddComponent<CanvasScaler>();
            canvas.gameObject.AddComponent<GraphicRaycaster>();

            // 메인 패널
            var panel = MakePanel(canvas.transform, Vector2.zero, new Vector2(880f, 660f), new Color(0.08f, 0.09f, 0.11f, 0.97f));

            // 제목
            MakeText(panel, "사무실", 28, TextAnchor.UpperCenter, new Vector2(0f, 295f), new Vector2(840f, 48f));

            // 크레딧 표시 (우상단)
            creditText = MakeText(panel, "C: 0", 16, TextAnchor.UpperRight, new Vector2(400f, 285f), new Vector2(180f, 36f));

            // ── 좌측: 용병 목록 ──
            var listBG = MakePanel(panel, new Vector2(-220f, 20f), new Vector2(400f, 490f), new Color(0.05f, 0.06f, 0.08f, 1f));
            MakeText(listBG.transform, "보유 용병", 16, TextAnchor.UpperCenter, new Vector2(0f, 218f), new Vector2(380f, 36f));

            var listRoot = new GameObject("ListRoot");
            listRoot.transform.SetParent(listBG.transform, false);
            var listRect = listRoot.AddComponent<RectTransform>();
            listRect.sizeDelta = new Vector2(380f, 420f);
            listRect.anchoredPosition = new Vector2(0f, -10f);
            var vLayout = listRoot.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 6f;
            vLayout.childControlHeight = true;
            vLayout.childControlWidth = true;
            vLayout.childForceExpandHeight = false;
            mercListRoot = listRoot.transform;

            // ── 우측: 상세 패널 ──
            detailPanel = new GameObject("DetailPanel");
            detailPanel.transform.SetParent(panel, false);
            var detailBG = detailPanel.AddComponent<Image>();
            detailBG.color = new Color(0.05f, 0.06f, 0.08f, 1f);
            var detailRect = detailPanel.GetComponent<RectTransform>();
            detailRect.sizeDelta = new Vector2(420f, 490f);
            detailRect.anchoredPosition = new Vector2(220f, 20f);

            detailNameText  = MakeText(detailPanel.transform, "─ 용병을 선택하세요 ─", 17, TextAnchor.UpperCenter, new Vector2(0f, 210f), new Vector2(400f, 36f));
            detailExpText   = MakeText(detailPanel.transform, string.Empty, 14, TextAnchor.UpperCenter, new Vector2(0f, 170f), new Vector2(400f, 28f));
            detailStatsText = MakeText(detailPanel.transform, string.Empty, 13, TextAnchor.UpperLeft, new Vector2(-185f, 130f), new Vector2(395f, 120f));

            detailPromotionStatus = MakeText(detailPanel.transform, string.Empty, 14, TextAnchor.UpperCenter, new Vector2(0f, 0f), new Vector2(400f, 28f));

            var promoRoot = new GameObject("PromoButtons");
            promoRoot.transform.SetParent(detailPanel.transform, false);
            var promoRect = promoRoot.AddComponent<RectTransform>();
            promoRect.sizeDelta = new Vector2(400f, 180f);
            promoRect.anchoredPosition = new Vector2(0f, -60f);
            var promoLayout = promoRoot.AddComponent<VerticalLayoutGroup>();
            promoLayout.spacing = 8f;
            promoLayout.childControlHeight = true;
            promoLayout.childControlWidth = true;
            promoLayout.childForceExpandHeight = false;
            promotionButtonRoot = promoRoot.transform;

            detailPanel.SetActive(false);

            // ── 하단 버튼 ──
            MakeButton(panel, "인력소", new Vector2(-280f, -288f), () => SceneNavigator.TryLoad("Recruit"));
            MakeButton(panel, "현장", new Vector2(-70f, -288f), () => SceneNavigator.TryLoad("Dungeon"));
            MakeButton(panel, "새로고침", new Vector2(160f, -288f), RefreshList);
        }

        // ── 목록 갱신 ─────────────────────────────────

        private void RefreshList()
        {
            ClearChildren(mercListRoot);

            creditText.text = $"C: {PlayerAccountService.Credits}";

            var owned = PlayerAccountService.OwnedTemplateIds;
            if (owned.Count == 0)
            {
                MakeText(mercListRoot, "보유 용병 없음", 14, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(360f, 40f));
                return;
            }

            if (!GameCsvTables.TryLoad(out var tables, out _))
            {
                return;
            }

            foreach (var id in owned)
            {
                var localId = id;
                var label = BuildMercLabel(id, tables);
                CreateMercButton(mercListRoot, label, () => SelectMerc(localId, tables));
            }
        }

        private static string BuildMercLabel(string templateId, GameCsvTables tables)
        {
            var name = templateId;
            var grade = 1;
            if (tables.TryGetCharacterRow(templateId, out var charRow))
            {
                name = charRow.Name;
                grade = charRow.Grade;
            }

            var level = PlayerAccountService.GetLevel(templateId);
            var promoting = PlayerAccountService.IsPromoting(templateId);
            var suffix = promoting ? " [승급 중]" : string.Empty;
            return $"★{grade} {name}  Lv.{level}{suffix}";
        }

        // ── 용병 선택 & 상세 ──────────────────────────

        private void SelectMerc(string templateId, GameCsvTables tables)
        {
            selectedTemplateId = templateId;
            RefreshDetail(templateId);
            detailPanel.SetActive(true);
        }

        private void RefreshDetail(string templateId)
        {
            if (!GameCsvTables.TryLoad(out var tables, out _))
            {
                return;
            }

            // 이름/레벨/크레딧
            var name = templateId;
            var grade = 1;
            if (tables.TryGetCharacterRow(templateId, out var charRow))
            {
                name = charRow.Name;
                grade = charRow.Grade;
            }

            var level = PlayerAccountService.GetLevel(templateId);
            var exp = PlayerAccountService.GetExp(templateId);
            var expToNext = tables.GetExpToNextLevel(level);
            detailNameText.text = $"{name}  ★{grade}  Lv.{level}";
            detailExpText.text = $"EXP: {exp} / {expToNext}";

            // 스탯
            if (tables.TryGetCombatUnit(templateId, out var unitRow))
            {
                detailStatsText.text =
                    $"HP:{unitRow.MaxHp}  MP:{unitRow.MaxMana}\n" +
                    $"ATK:{unitRow.Attack}  DEF:{unitRow.Defense}  AGI:{unitRow.Agility}\n" +
                    $"Type: {unitRow.DamageType} / {unitRow.AttackRangeType}";
            }
            else
            {
                detailStatsText.text = "(combat_units 데이터 없음)";
            }

            // 승급 UI 갱신
            ClearChildren(promotionButtonRoot);
            BuildPromotionSection(templateId, grade, level, charRow, tables);
        }

        private void BuildPromotionSection(
            string templateId, int grade, int level,
            CharacterRow charRow, GameCsvTables tables)
        {
            // 이미 승급 진행 중
            if (PlayerAccountService.IsPromoting(templateId))
            {
                var remaining = PlayerAccountService.GetPromotionRemainingSeconds(templateId);
                if (remaining > 0)
                {
                    var mins = remaining / 60;
                    var secs = remaining % 60;
                    detailPromotionStatus.text = $"승급 진행 중  남은 시간: {mins:D2}:{secs:D2}";
                }
                else
                {
                    detailPromotionStatus.text = "승급 완료! 확인 버튼을 눌러주세요.";
                    var localId = templateId;
                    CreatePromoButton(promotionButtonRoot, "✓ 승급 확인", new Color(0.2f, 0.7f, 0.3f), () => OnConfirmPromotion(localId));
                }
                return;
            }

            detailPromotionStatus.text = string.Empty;

            // Route A / B 버튼 생성
            BuildRouteButton(templateId, grade, level, "A", charRow.PromotionRouteA, tables);
            BuildRouteButton(templateId, grade, level, "B", charRow.PromotionRouteB, tables);

            if (string.IsNullOrEmpty(charRow.PromotionRouteA) && string.IsNullOrEmpty(charRow.PromotionRouteB))
            {
                detailPromotionStatus.text = "(승급 경로 없음)";
            }
        }

        private void BuildRouteButton(
            string templateId, int grade, int level,
            string route, string targetId, GameCsvTables tables)
        {
            if (string.IsNullOrEmpty(targetId))
            {
                return;
            }

            if (!tables.TryGetPromotionRule(grade, route, out var rule))
            {
                return;
            }

            var targetName = targetId;
            if (tables.TryGetCharacterRow(targetId, out var targetChar))
            {
                targetName = targetChar.Name;
            }

            var canLevel = level >= rule.RequiredLevel;
            var canAfford = PlayerAccountService.Credits >= rule.CostCredits;
            var canPromote = canLevel && canAfford;

            var labelColor = canPromote ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            var btnColor = canPromote ? new Color(0.2f, 0.3f, 0.5f) : new Color(0.15f, 0.15f, 0.2f);

            var timeMin = rule.TimeSeconds / 60;
            var timeSec = rule.TimeSeconds % 60;
            var label = $"Route {route}: {targetName}\n  Lv.{rule.RequiredLevel}↑  {timeMin:D2}:{timeSec:D2}  {rule.CostCredits}C";

            if (!canLevel)  label += "  (레벨 부족)";
            if (!canAfford) label += "  (크레딧 부족)";

            var localTemplate = templateId;
            var localTarget = targetId;
            var localRule = rule;

            var btn = CreatePromoButton(promotionButtonRoot, label, btnColor,
                canPromote
                    ? (Action)(() => OnStartPromotion(localTemplate, localTarget, localRule))
                    : null);

            var btnText = btn.GetComponentInChildren<Text>();
            if (btnText != null) btnText.color = labelColor;
        }

        // ── 승급 액션 ─────────────────────────────────

        private void OnStartPromotion(string templateId, string targetTemplateId, PromotionRuleRow rule)
        {
            var success = PlayerAccountService.StartPromotion(
                templateId, targetTemplateId, rule.TimeSeconds, rule.CostCredits);

            if (!success)
            {
                Debug.LogWarning($"[office] StartPromotion failed: {templateId} → {targetTemplateId}");
                return;
            }

            Debug.Log($"[office] 승급 시작: {templateId} → {targetTemplateId} ({rule.TimeSeconds}초 / {rule.CostCredits}C)");
            creditText.text = $"C: {PlayerAccountService.Credits}";
            RefreshList();
            RefreshDetail(templateId);
        }

        private void OnConfirmPromotion(string templateId)
        {
            if (!PlayerAccountService.TryCompletePromotion(templateId, out var newId))
            {
                Debug.LogWarning($"[office] TryCompletePromotion failed: {templateId}");
                return;
            }

            Debug.Log($"[office] 승급 완료: {templateId} → {newId}");
            selectedTemplateId = newId;
            RefreshList();
            RefreshDetail(newId);
        }

        // ── UI 헬퍼 ──────────────────────────────────

        private void CreateMercButton(Transform parent, string label, Action onClick)
        {
            var go = new GameObject("MercBtn");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = new Color(0.14f, 0.18f, 0.24f, 1f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 48f;
            var txt = MakeText(go.transform, label, 14, TextAnchor.MiddleLeft, new Vector2(8f, 0f), new Vector2(350f, 44f));
            txt.color = Color.white;
        }

        private static GameObject CreatePromoButton(Transform parent, string label, Color bgColor, Action onClick)
        {
            var go = new GameObject("PromoBtn");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = bgColor;
            var btn = go.AddComponent<Button>();
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 58f;
            MakeText(go.transform, label, 13, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(390f, 54f));
            return go;
        }

        private static RectTransform MakePanel(Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = color;
            var r = go.GetComponent<RectTransform>();
            r.sizeDelta = size;
            r.anchoredPosition = pos;
            return r;
        }

        private static Text MakeText(Transform parent, string value, int fontSize,
            TextAnchor anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.alignment = anchor;
            t.color = Color.white;
            t.text = value;
            var r = t.GetComponent<RectTransform>();
            r.sizeDelta = size;
            r.anchoredPosition = pos;
            return t;
        }

        private static void MakeButton(Transform parent, string label, Vector2 pos, Action onClick)
        {
            var go = new GameObject(label + "Btn");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = new Color(0.17f, 0.24f, 0.33f, 1f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            var r = go.GetComponent<RectTransform>();
            r.sizeDelta = new Vector2(190f, 44f);
            r.anchoredPosition = pos;
            MakeText(go.transform, label, 17, TextAnchor.MiddleCenter, Vector2.zero, r.sizeDelta);
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var c = parent.GetChild(i);
                if (c != null) Destroy(c.gameObject);
            }
        }
    }
}
