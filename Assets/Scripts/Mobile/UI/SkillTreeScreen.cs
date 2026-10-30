using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // SkillTree 씬 하단 메뉴. 세계수·스테이지 진입은 씬 이동, 의뢰·공방·도감은 팝업.
    // 팝업은 한 번에 하나만 연다 (다른 메뉴를 누르면 열려 있던 것은 닫힌다). 팝업은 프리팹이고 처음 열 때 이 캔버스 아래에 만든다.
    // 스킬 노드를 누르면 SkillInfo가 열리고, 같은 노드를 다시 누르거나 빈 곳을 누르면 닫힌다.
    // 스킬 트리는 가지(독 · 식용 · 약용)마다 한 화면이고, 처음엔 식용을 보여 준다. 양옆 화살표로 이웃 가지로 넘긴다.
    public class SkillTreeScreen : MonoBehaviour
    {
        [SerializeField] SkillTreeView treeView;      // 가지를 넘길 때마다 그 가지의 노드를 다시 깐다
        [SerializeField] SkillInfoPanel skillInfo;
        [SerializeField] Button blankArea;            // 노드 뒤 투명 판. 누르면 SkillInfo를 닫는다

        SkillNode selected;

        [Header("가지 넘기기")]
        [SerializeField] SkillBranch firstBranch = SkillBranch.Edible;
        [SerializeField] Button prevBranchButton;     // 왼쪽 화살표
        [SerializeField] Button nextBranchButton;     // 오른쪽 화살표
        [SerializeField] TMP_Text prevBranchLabel;    // 화살표 아래 넘어갈 가지 이름
        [SerializeField] TMP_Text nextBranchLabel;

        static readonly SkillBranch[] BranchOrder = { SkillBranch.Toxic, SkillBranch.Edible, SkillBranch.Medicinal };
        int branchAt;

        [SerializeField] Button worldTreeButton;
        [SerializeField] Button questButton;
        [SerializeField] Button indexButton;
        [SerializeField] Button workshopButton;
        [SerializeField] Button stageButton;

        [SerializeField] Button optionButton;
        [SerializeField] PopupSlot optionPopup;       // 설정 (데이터 초기화). 다른 메뉴 팝업과 같이 한 번에 하나만

        [SerializeField] PopupSlot questPopup;
        [SerializeField] PopupSlot indexPopup;
        [SerializeField] PopupSlot workshopPopup;

        [SerializeField] TMP_Text stageName;          // 스테이지 입장 버튼 위 지금 스테이지 이름 (보스 스테이지면 표시)
        [SerializeField] TMP_Text goldText;           // 지갑 골드
        [SerializeField] TMP_Text myceliteText;       // 지갑 균사석

        [SerializeField] string worldTreeScene = "WorldTree";
        [SerializeField] string stageScene = "";      // 비우면 스테이지 버튼을 막는다 (기존 InGame 씬은 안 씀)

        void Awake()
        {
            worldTreeButton.onClick.AddListener(() => Go(worldTreeScene));
            if (string.IsNullOrEmpty(stageScene)) stageButton.interactable = false;
            else stageButton.onClick.AddListener(() => Go(stageScene));
            questButton.onClick.AddListener(() => Show(questPopup));
            indexButton.onClick.AddListener(() => Show(indexPopup));
            workshopButton.onClick.AddListener(() => Show(workshopPopup));
            if (optionButton != null && optionPopup != null) optionButton.onClick.AddListener(() => Show(optionPopup));

            if (prevBranchButton != null) prevBranchButton.onClick.AddListener(() => ShowBranch(branchAt - 1));
            if (nextBranchButton != null) nextBranchButton.onClick.AddListener(() => ShowBranch(branchAt + 1));
            ShowBranch(Mathf.Max(0, System.Array.IndexOf(BranchOrder, firstBranch)));
            ShowStage();
            Wallet.Changed += ShowWallet;
            ShowWallet();
            if (blankArea != null) blankArea.onClick.AddListener(Deselect);
            if (skillInfo != null) skillInfo.Hide();
        }

        void ShowBranch(int at)
        {
            if (at < 0 || at >= BranchOrder.Length) return;
            Deselect();
            branchAt = at;
            if (treeView != null)
            {
                treeView.Build(BranchOrder[at]);
                foreach (var node in treeView.Nodes) node.Clicked += Select;
            }
            SetArrow(prevBranchButton, prevBranchLabel, at - 1);
            SetArrow(nextBranchButton, nextBranchLabel, at + 1);
        }

        // 넘어갈 가지가 없으면 숨긴다
        void SetArrow(Button button, TMP_Text label, int at)
        {
            if (button == null) return;
            bool exists = at >= 0 && at < BranchOrder.Length;
            button.gameObject.SetActive(exists);
            if (exists && label != null) label.text = BranchName(BranchOrder[at]);
        }

        static string BranchName(SkillBranch branch)
        {
            switch (branch)
            {
                case SkillBranch.Edible: return "식용";
                case SkillBranch.Medicinal: return "약용";
                case SkillBranch.Toxic: return "독";
                default: return "";
            }
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

        void OnDestroy() => Wallet.Changed -= ShowWallet;

        void ShowWallet()
        {
            if (goldText != null) goldText.text = Wallet.Format(Wallet.Gold);
            if (myceliteText != null) myceliteText.text = Wallet.Format(Wallet.Mycelite);
        }

        // 보스 스테이지면 "보스", 보스에게 져서 파밍 중이면 "파밍 n"
        void ShowStage()
        {
            if (stageName == null) return;
            var stage = StageProgress.CurrentData;
            if (stage == null) return;
            int farm = StageProgress.FarmLeft;
            stageName.text = stage.boss == 0 ? stage.name : farm > 0 ? $"{stage.name} 파밍 {farm}" : $"{stage.name} 보스";
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
            if (optionPopup != null) optionPopup.Close();
        }

        static void Go(string scene) => SceneLoader.Load(scene);
    }
}
