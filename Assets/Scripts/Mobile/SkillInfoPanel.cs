using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 스킬 정보 창 (SkillTree 씬의 SkillInfo). 노드를 누르면 열려 이름·레벨·설명을 보여 주고, 배우기 버튼으로 레벨을 올린다.
    // 임시: 가격은 표시·차감하지 않는다 (지갑 기능이 생기면 붙인다)
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
            descText.text = skill.desc ?? "";

            bool maxed = SkillProgress.IsMaxed(skill);
            bool ready = SkillProgress.RequirementsMet(skill);
            learnButton.interactable = !maxed && ready;
            learnLabel.text = maxed ? "최대 레벨" : !ready ? "선행 스킬 필요" : level == 0 ? "배우기" : "레벨 업";
        }
    }
}
