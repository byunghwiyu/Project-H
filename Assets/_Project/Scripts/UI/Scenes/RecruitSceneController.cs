using ProjectH.Account;
using ProjectH.Core;
using ProjectH.Data.Tables;
using TMPro;
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

        [SerializeField] private TMP_Text headerInfoText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Transform listRoot;
        [SerializeField] private ListItemBinding listItemCardPrefab;

        [SerializeField] private Button refreshBtn;
        [SerializeField] private Button officeBtn;
        [SerializeField] private Button dungeonBtn;

        private readonly System.Random rng = new System.Random();
        private bool isFirstLoad = true;

        private void Start()
        {
            refreshBtn.onClick.AddListener(RefreshOffers);
            officeBtn.onClick.AddListener(() => SceneNavigator.TryLoad("Office"));
            dungeonBtn.onClick.AddListener(() => SceneNavigator.TryLoad("Dungeon"));
            RefreshOffers();
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

            headerInfoText.text =
                $"Lv.{officeLevel}  G{minGrade}-{maxGrade}  리롤 {rerollCost}C  |  {PlayerAccountService.Credits}C";
            statusText.text = "카드를 선택해 모집하세요.";

            foreach (var offer in offers)
            {
                var model = BuildOffer(offer, tables, talents);
                var label = BuildOfferLabel(model);
                var gradeColor = Color.Lerp(UITheme.Instance.surfaceRaised,
                    UIFactory.GradeColor(model.Grade), 0.25f);
                var card = Instantiate(listItemCardPrefab, listRoot);
                card.Set(label, gradeColor, () => Recruit(model), 120f);
            }

            if (listRoot is RectTransform rt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        private RecruitOffer BuildOffer(CombatUnitRow row, GameCsvTables tables, TalentCatalog talents)
        {
            tables.TryGetCharacterRow(row.EntityId, out var characterRow);
            var roleTag = characterRow.RoleTag;
            var talentTag = talents.DrawTalentTag(roleTag, rng);
            return new RecruitOffer
            {
                TemplateId        = row.EntityId,
                Name              = string.IsNullOrWhiteSpace(characterRow.Name) ? row.Name : characterRow.Name,
                RoleTag           = roleTag,
                TalentTag         = talentTag,
                TalentName        = talents.GetTalentName(talentTag),
                TalentDescription = talents.GetTalentDescription(talentTag),
                Grade             = characterRow.Grade,
                MaxHp             = row.MaxHp,
                Attack            = row.Attack,
                Agility           = row.Agility,
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
            var gradeName = offer.Grade switch
            {
                1 => "Normal", 2 => "Rare", 3 => "Elite",
                4 => "Epic", 5 => "Legend", _ => $"G{offer.Grade}"
            };
            return $"[{gradeName}] {offer.Name}  ({offer.RoleTag})\n" +
                   $"HP {offer.MaxHp}  ATK {offer.Attack}  AGI {offer.Agility}\n" +
                   $"재능: {offer.TalentName}  {offer.TalentDescription}";
        }
    }
}
