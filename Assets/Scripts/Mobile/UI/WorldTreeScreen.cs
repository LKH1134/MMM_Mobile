using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // WorldTree 씬 화면. 재화 표시, 옵션 팝업, 성장·꼬마·꾸미기·공방·의뢰 시트, Main으로 돌아가기.
    // 골드·균사석은 지갑(Wallet)을 보여 준다. 시트 내용은 각 팝업 Content의 TreePanel이 맡는다
    public class WorldTreeScreen : MonoBehaviour
    {
        // 버튼 하나 = 팝업 하나. 시트 팝업은 모두 처음 열 때 이 캔버스 아래에 만든다
        [System.Serializable]
        public class Menu
        {
            public Button button;
            public PopupSlot popup;
        }

        [SerializeField] TMP_Text goldText;
        [SerializeField] TMP_Text myceliteText;

        [SerializeField] Button optionButton;
        [SerializeField] PopupSlot optionPopup;   // 프리팹. 처음 열 때 이 캔버스 아래에 만든다
        [SerializeField] Menu[] menus;            // 성장·꼬마·꾸미기·공방·의뢰. 하단바의 홈·던전 버튼은 MainTabButton으로 씬 이동만 한다 (여기 넣지 않는다)
        [SerializeField] string backScene = "Main";
        [SerializeField] RectTransform popupRoot;   // 팝업을 만들 부모 = 디자인 영역(SafeArea/Stage). 비우면 이 캔버스

        [SerializeField] Toast toast;               // 씬에 배치한 안내 문구 (꺼 둔다)
        [SerializeField] float toastDuration = 3f;

        void Awake()
        {
            // 팝업은 디자인 영역 안에 만든다 → 노치·홈바에 걸리지 않고 화면 비율이 달라도 같은 크기
            Transform root = popupRoot != null ? popupRoot : transform;
            optionButton.onClick.AddListener(() => optionPopup.Get(root).Toggle());
            foreach (var menu in menus)
            {
                var m = menu;
                if (m.button != null) m.button.onClick.AddListener(() => m.popup.Get(root).Toggle());
            }
            Wallet.Changed += ShowWallet;
            ShowWallet();
        }

        void OnDestroy() => Wallet.Changed -= ShowWallet;

        // 뒤로가기(BackButton): 열린 팝업이 없으면 Main으로
        void OnEnable() => BackButton.SceneHandler = GoBack;

        void OnDisable()
        {
            if (BackButton.SceneHandler == (System.Action)GoBack) BackButton.SceneHandler = null;
        }

        // 지갑(Wallet)의 골드·균사석
        void ShowWallet()
        {
            SetGold(Wallet.Gold);
            SetMycelite(Wallet.Mycelite);
        }

        // Back 버튼 OnClick(인스펙터)에 연결돼 있다
        public void GoBack() => SceneLoader.Load(backScene);

        public void SetGold(long value) => goldText.text = Wallet.Format(value);
        public void SetMycelite(long value) => myceliteText.text = Wallet.Format(value);

        public void ShowToast(string message)
        {
            if (toast == null) return;
            toast.Show(message, toastDuration);
        }
    }
}
