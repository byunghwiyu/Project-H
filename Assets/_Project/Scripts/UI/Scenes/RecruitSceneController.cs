using System.Collections.Generic;
using ProjectH.Account;
using ProjectH.Core;
using ProjectH.Data.Tables;
using UnityEngine;
using UnityEngine.UI;
using ProjectH.UI;

namespace ProjectH.UI.Scenes
{
    public sealed class RecruitSceneController : MonoBehaviour
    {
        private sealed class RecruitOffer
        {
            public string TemplateId;
            public string Name;
            public string RoleTag;
            public string TalentTag;
            public string TalentName;
            public string TalentDescription;
            public int Grade;
            public int MaxHp;
            public int Attack;
            public int Agility;
        }

        [SerializeField] private string recruitPoolId = "default";
        [SerializeField] private int offerCountOverride;

        private readonly System.Random rng = new System.Random();
        private Text headerInfoText;
        private Text statusText;
        private Transform listRoot;
        private bool isFirstLoad = true;

        private void Start()
        {
            BuildUI();
            RefreshOffers();
        }

        private void BuildUI()
        {
            var t = UITheme.Instance;
            var canvas = UIFactory.CreateCanvas("RecruitCanvas");
            var root = UIFactory.CreateFullPanel(canvas.transform);

            // ── 상단 바 ──────────────────────────────────────────────
            var topBar = UIFactory.CreateTopBar(root);

            // 좌측: 타이틀 (stretch 앵커)
            UIFactory.CreateBarText(topBar, "용병 모집", TextRole.Heading,
                TextAnchor.MiddleLeft, left: UIFactory.HorizPad, right: 480f);

            // 우측: 레벨/크레딧 (stretch 앵커)
            headerInfoText = UIFactory.CreateBarText(topBar, string.Empty, TextRole.Caption,
                TextAnchor.MiddleRight, left: 400f, right: UIFactory.HorizPad);

            // ── 상태 텍스트 (탑바 바로 아래 작은 영역) ──────────────
            var statusBar = new GameObject("StatusBar");
            statusBar.transform.SetParent(root, false);
            statusBar.AddComponent<Image>().color = t.surface;
            var statusRect = statusBar.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 1f);
            statusRect.anchorMax = new Vector2(1f, 1f);
            statusRect.pivot     = new Vector2(0.5f, 1f);
            statusRect.offsetMin = new Vector2(0f, -(UIFactory.TopBarH + 64f));
            statusRect.offsetMax = new Vector2(0f, -UIFactory.TopBarH);

            statusText = UIFactory.CreateText(statusBar.transform, string.Empty, TextRole.Caption,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            var stRect = statusText.GetComponent<RectTransform>();
            stRect.anchorMin = Vector2.zero;
            stRect.anchorMax = Vector2.one;
            stRect.offsetMin = new Vector2(UIFactory.HorizPad, 0f);
            stRect.offsetMax = new Vector2(-UIFactory.HorizPad, 0f);

            // ── 콘텐츠 (스크롤 리스트) ───────────────────────────────
            var contentArea = UIFactory.CreateContentArea(root,
                topInset: UIFactory.TopBarH + 64f,
                bottomInset: UIFactory.BottomNavH);
            listRoot = UIFactory.CreateScrollList(contentArea, spacing: 10f);

            // ── 하단 내비 ────────────────────────────────────────────
            var bottomBar = UIFactory.CreateBottomBar(root);
            var navRow = UIFactory.CreateHorizontalRow(bottomBar, spacing: 4f);
            UIFactory.CreateNavButton(navRow, "리프레시", RefreshOffers, ButtonVariant.Muted);
            UIFactory.CreateNavButton(navRow, "사무소", () => NavigateTo("Office"), ButtonVariant.Nav);
            UIFactory.CreateNavButton(navRow, "현장", () => NavigateTo("Dungeon"), ButtonVariant.Nav);
        }

        private void RefreshOffers()
        {
            UIFactory.ClearChildren(listRoot);

            if (!GameCsvTables.TryLoad(out var tables, out var tableError))
            {
                statusText.text = $"테이블 로드 실패: {tableError}";
                return;
            }

            if (!TalentCatalog.TryLoad(out var talents, out _))
            {
                statusText.text = "재능 테이블 로드 실패";
                return;
            }

            var officeLevel = PlayerAccountService.GetOfficeLevel();
            var rerollCost = 0;
            var offerCount = offerCountOverride;
            var minGrade = 0;
            var maxGrade = 0;

            if (tables.TryGetOfficeLevelRow(officeLevel, out var levelRow))
            {
                rerollCost = levelRow.RerollCostCredits;
                if (offerCount <= 0) offerCount = levelRow.OfferCount;
                minGrade = levelRow.MinGrade;
                maxGrade = levelRow.MaxGrade;
            }

            if (!isFirstLoad && rerollCost > 0 && !PlayerAccountService.TrySpendCredits(rerollCost))
            {
                statusText.text = $"크레딧 부족 (필요: {rerollCost}C)";
                return;
            }

            isFirstLoad = false;

            if (!tables.TryDrawRecruitOffers(recruitPoolId, offerCount, minGrade, maxGrade,
                    out var offers, out var offerError))
            {
                statusText.text = $"모집 실패: {offerError}";
                return;
            }

            // 헤더 정보 갱신
            headerInfoText.text =
                $"Lv.{officeLevel}  G{minGrade}-{maxGrade}  리롤 {rerollCost}C  |  {PlayerAccountService.Credits}C";
            statusText.text = "카드를 선택해 모집하세요.";

            foreach (var offer in offers)
            {
                var model = BuildOffer(offer, tables, talents);
                var label = BuildOfferLabel(model);
                var gradeColor = Color.Lerp(UITheme.Instance.surfaceRaised,
                    UIFactory.GradeColor(model.Grade), 0.25f);
                UIFactory.CreateListItem(listRoot, label, 120f, gradeColor,
                    () => Recruit(model));
            }

            // ContentSizeFitter가 즉시 반영되도록 강제 갱신
            if (listRoot is RectTransform rt)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        private RecruitOffer BuildOffer(CombatUnitRow row, GameCsvTables tables, TalentCatalog talents)
        {
            tables.TryGetCharacterRow(row.EntityId, out var characterRow);
            var roleTag = characterRow.RoleTag;
            var talentTag = talents.DrawTalentTag(roleTag, rng);
            return new RecruitOffer
            {
                TemplateId       = row.EntityId,
                Name             = string.IsNullOrWhiteSpace(characterRow.Name) ? row.Name : characterRow.Name,
                RoleTag          = roleTag,
                TalentTag        = talentTag,
                TalentName       = talents.GetTalentName(talentTag),
                TalentDescription= talents.GetTalentDescription(talentTag),
                Grade            = characterRow.Grade,
                MaxHp            = row.MaxHp,
                Attack           = row.Attack,
                Agility          = row.Agility,
            };
        }

        private void Recruit(RecruitOffer offer)
        {
            PlayerAccountService.Recruit(offer.TemplateId, offer.TalentTag);
            headerInfoText.text = headerInfoText.text.Split('|')[0] + $"| {PlayerAccountService.Credits}C";
            statusText.text = $"모집 완료: {offer.Name} / {offer.TalentName}";
        }

        private static string BuildOfferLabel(RecruitOffer offer)
        {
            var gradeName = offer.Grade switch { 1 => "Normal", 2 => "Rare", 3 => "Elite",
                4 => "Epic", 5 => "Legend", _ => $"G{offer.Grade}" };
            return $"[{gradeName}] {offer.Name}  ({offer.RoleTag})\n" +
                   $"HP {offer.MaxHp}  ATK {offer.Attack}  AGI {offer.Agility}\n" +
                   $"재능: {offer.TalentName}  {offer.TalentDescription}";
        }

        private void NavigateTo(string sceneName)
        {
            if (!SceneNavigator.TryLoad(sceneName))
                statusText.text = $"씬 이동 실패: {sceneName}";
        }
    }
}
