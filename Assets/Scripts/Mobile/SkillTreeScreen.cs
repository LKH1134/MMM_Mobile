using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // SkillTree 씬 하단 메뉴. 세계수·스테이지 진입은 씬 이동, 의뢰·공방·도감은 팝업.
    // 팝업은 한 번에 하나만 연다 (다른 메뉴를 누르면 열려 있던 것은 닫힌다). 팝업은 프리팹이고 처음 열 때 이 캔버스 아래에 만든다.
    // 스킬 노드를 누르면 SkillInfo가 열리고, 같은 노드를 다시 누르거나 빈 곳을 누르면 닫힌다.
    public class SkillTreeScreen : MonoBehaviour
    {
        [SerializeField] SkillTreeView treeView;      // 시작할 때 Skill 표대로 노드를 깐다
        [SerializeField] RectTransform nodeRoot;      // 이 아래의 SkillNode를 모두 찾는다
        [SerializeField] SkillInfoPanel skillInfo;
        [SerializeField] Button blankArea;            // 노드 뒤 투명 판. 누르면 SkillInfo를 닫는다

        SkillNode selected;

        [SerializeField] Button worldTreeButton;
        [SerializeField] Button questButton;
        [SerializeField] Button indexButton;
        [SerializeField] Button workshopButton;
        [SerializeField] Button stageButton;

        [SerializeField] PopupSlot questPopup;
        [SerializeField] PopupSlot indexPopup;
        [SerializeField] PopupSlot workshopPopup;

        [SerializeField] string worldTreeScene = "WorldTree";
        [SerializeField] string stageScene = "InGame";

        void Awake()
        {
            worldTreeButton.onClick.AddListener(() => Go(worldTreeScene));
            stageButton.onClick.AddListener(() => Go(stageScene));
            questButton.onClick.AddListener(() => Show(questPopup));
            indexButton.onClick.AddListener(() => Show(indexPopup));
            workshopButton.onClick.AddListener(() => Show(workshopPopup));

            if (treeView != null) treeView.Build();
            if (nodeRoot != null)
                foreach (var node in nodeRoot.GetComponentsInChildren<SkillNode>(true)) node.Clicked += Select;
            if (blankArea != null) blankArea.onClick.AddListener(Deselect);
            if (skillInfo != null) skillInfo.Hide();
        }

        void Update()
        {
            // 안드로이드 뒤로 가기 = 열린 팝업·스킬 정보 닫기
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                CloseAll();
                Deselect();
            }
        }

        void Select(SkillNode node)
        {
            if (node == selected)
            {
                Deselect();
                return;
            }
            var data = node.Data;
            if (data == null) return;
            if (selected != null) selected.SetSelected(false);
            selected = node;
            node.SetSelected(true);
            skillInfo.Show(data);
        }

        void Deselect()
        {
            if (selected != null) selected.SetSelected(false);
            selected = null;
            if (skillInfo != null) skillInfo.Hide();
        }

        void Show(PopupSlot popup)
        {
            if (popup.IsOpen) { popup.Close(); return; }
            CloseAll();
            popup.Get(transform).Open();
        }

        void CloseAll()
        {
            questPopup.Close();
            indexPopup.Close();
            workshopPopup.Close();
        }

        static void Go(string scene)
        {
            if (Application.CanStreamedLevelBeLoaded(scene)) SceneManager.LoadScene(scene);
            else Debug.LogWarning($"[SkillTreeScreen] 빌드 설정에 없는 씬: {scene}");
        }
    }
}
