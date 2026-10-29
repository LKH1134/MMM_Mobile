using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 스킬 정보 창 (SkillTree 씬의 SkillInfo). 노드를 누르면 열려 이름·레벨·설명을 보여 주고, 배우기 버튼으로 레벨을 올린다.
    // 배우기 버튼에 다음 레벨 가격을 보여 주고, 재화가 모자라면 누를 수 없다 (차감은 SkillProgress.LevelUp)
    public class SkillInfoPanel : MonoBehaviour
    {
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text levelText;          // "Lv.0 / 1"
        [SerializeField] TMP_Text descText;
        [SerializeField] Button learnButton;
        [SerializeField] TMP_Text learnLabel;

        SkillData skill;

        public SkillData Skill => skill;
        public bool IsOpen => gameObject.activeSelf;

        void Awake()
        {
            learnButton.onClick.AddListener(Learn);
            SkillProgress.Changed += OnChanged;
        }

        void OnDestroy() => SkillProgress.Changed -= OnChanged;

        public void Show(SkillData data)
        {
            skill = data;
            gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            skill = null;
            gameObject.SetActive(false);
        }

        void Learn()
        {
            if (skill != null) SkillProgress.LevelUp(skill);
        }

        void OnChanged(int index)
        {
            if (skill != null && (index == 0 || index == skill.index)) Refresh();
        }

        void Refresh()
        {
            if (skill == null) return;
            int level = SkillProgress.GetLevel(skill.index);
            nameText.text = skill.name ?? skill.key;
            levelText.text = $"Lv.{level} / {SkillProgress.MaxLevel(skill)}";
            bool maxed = SkillProgress.IsMaxed(skill);
            var missing = SkillProgress.MissingRequirements(skill);
            bool ready = missing.Count == 0;

            // 못 채운 선행 조건을 설명 아래에 적는다
            var desc = new System.Text.StringBuilder(skill.desc ?? "");
            if (!maxed && !ready)
            {
                desc.Append("\n<size=80%>필요: ");
                for (int i = 0; i < missing.Count; i++)
                {
                    // 이전 노드의 첫 레벨
                    var need = missing[i];
                    if (i > 0) desc.Append(", ");
                    desc.Append(need.name).Append(" Lv.").Append(Mathf.Max(1, need.startLevel));
                }
                desc.Append("</size>");
            }
            descText.text = desc.ToString();

            // 다음 레벨 가격 (재화가 모자라면 누를 수 없다)
            long price = SkillProgress.NextPrice(skill, out var currency);
            bool afford = Wallet.CanAfford(currency, price);
            learnButton.interactable = !maxed && ready && afford;
            string verb = level == 0 ? "배우기" : "레벨 업";
            string cost = price > 0 ? $"\n<size=70%>{Wallet.Format(price)} {(currency == Currency.Mycelite ? "균사석" : "골드")}</size>" : "";
            learnLabel.text = maxed ? "최대 레벨" : !ready ? "선행 스킬 필요" : verb + cost;
        }

        void OnEnable() => Wallet.Changed += OnWalletChanged;
        void OnDisable() => Wallet.Changed -= OnWalletChanged;
        void OnWalletChanged() => Refresh();
    }
}
