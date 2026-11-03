using UnityEngine;

namespace MoreMush.Mobile
{
    // 업그레이드 화면의 '배우기' 버튼을 고른 스킬 노드 바로 위에 띄운다. 버튼 기능(가격 표시·레벨 업)은 SkillInfoPanel이 맡는다.
    // 버튼을 노드와 같은 판 안으로 옮겨 스크롤·확대를 따라가게 하고, 스킬 정보 창이 닫히면 같이 숨긴다
    public class LearnButtonFollow : MonoBehaviour
    {
        [SerializeField] RectTransform button;     // SkillInfo의 LearnButton
        [SerializeField] SkillInfoPanel info;
        [SerializeField] RectTransform nodeRoot;   // 이 아래의 SkillNode를 모두 찾는다
        [SerializeField] float gap = 10f;          // 노드 윗변과 버튼 사이

        SkillNode target;

        // 노드는 SkillTreeScreen.Awake에서 깔리므로 Start에서 찾는다
        void Start()
        {
            foreach (var node in nodeRoot.GetComponentsInChildren<SkillNode>(true)) node.Clicked += n => target = n;
            button.anchorMin = button.anchorMax = new Vector2(0.5f, 0.5f);
            button.pivot = new Vector2(0.5f, 0f);
            button.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            bool show = target != null && info.IsOpen;
            if (button.gameObject.activeSelf != show) button.gameObject.SetActive(show);
            if (!show) return;

            var node = (RectTransform)target.transform;
            if (button.parent != node.parent) button.SetParent(node.parent, false);
            button.SetAsLastSibling();   // 다른 노드에 가리지 않게 맨 위에 그린다

            // 노드 윗변 가운데 + gap (노드 크기와 선택 확대를 반영)
            button.position = node.TransformPoint(new Vector3(node.rect.center.x, node.rect.yMax, 0f));
            button.anchoredPosition += new Vector2(0f, gap);
        }
    }
}
