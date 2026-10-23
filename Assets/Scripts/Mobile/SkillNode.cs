using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 스킬 트리의 노드 하나. skillIndex로 Skill.xlsx의 행을 찾아 쓰고, 누르면 SkillTreeScreen에 알린다.
    // 배우기 전엔 흐리게, 고르면 조금 크게 보인다
    [RequireComponent(typeof(Button))]
    public class SkillNode : MonoBehaviour
    {
        [SerializeField] int skillIndex;              // Skill.xlsx index (3001~). 0이면 아직 비어 있는 노드
        [SerializeField] Image icon;
        [SerializeField] TMP_Text levelText;          // "0/1"
        [SerializeField] Color lockedTint = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] float selectedScale = 1.12f;

        public int SkillIndex => skillIndex;
        public SkillData Data => GameData.Skills.Get(skillIndex);
        public event System.Action<SkillNode> Clicked;

        void Awake() => GetComponent<Button>().onClick.AddListener(() => Clicked?.Invoke(this));

        void OnEnable()
        {
            SkillProgress.Changed += OnChanged;
            Refresh();
        }

        void OnDisable() => SkillProgress.Changed -= OnChanged;

        void OnChanged(int index)
        {
            if (index == 0 || index == skillIndex) Refresh();
        }

        public void SetSelected(bool selected) => transform.localScale = Vector3.one * (selected ? selectedScale : 1f);

        public void Refresh()
        {
            var data = Data;
            int level = SkillProgress.GetLevel(skillIndex);
            if (levelText != null) levelText.text = data != null ? $"{level}/{SkillProgress.MaxLevel(data)}" : "?";
            if (icon != null) icon.color = level > 0 ? Color.white : lockedTint;
            if (data == null && skillIndex != 0) Debug.LogWarning($"[SkillNode] {name}: Skill 표에 {skillIndex}번이 없다", this);
        }
    }
}
