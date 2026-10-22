using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // WorldTree 씬 화면. 재화·바구니(세계수 보상 누적치) 표시, 옵션 팝업, 성장·꼬마·꾸미기·제작소 시트, 스킬 트리로 돌아가기.
    // 값은 게임 쪽에서 SetGold/SetMycelite/SetBasket으로 넣는다.
    // 바구니를 누르면 쌓인 골드를 받았다는 문구만 띄운다 (실제로 받는 처리는 추후)
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
        [SerializeField] TMP_Text basketText;

        [SerializeField] Button optionButton;
        [SerializeField] PopupSlot optionPopup;   // 프리팹. 처음 열 때 이 캔버스 아래에 만든다
        [SerializeField] Menu[] menus;            // 성장·꼬마·꾸미기·제작소
        [SerializeField] string backScene = "SkillTree";

        [SerializeField] Button basketButton;
        [SerializeField] Toast toastPrefab;
        [SerializeField] float toastDuration = 3f;

        double basket;
        Toast toast;

        void Awake()
        {
            optionButton.onClick.AddListener(() => optionPopup.Get(transform).Toggle());
            foreach (var menu in menus)
            {
                var m = menu;
                if (m.button != null) m.button.onClick.AddListener(() => m.popup.Get(transform).Toggle());
            }
            if (basketButton != null) basketButton.onClick.AddListener(CollectBasket);
        }

        // Back 버튼 OnClick(인스펙터)에 연결돼 있다
        public void GoBack()
        {
            if (Application.CanStreamedLevelBeLoaded(backScene)) SceneManager.LoadScene(backScene);
            else Debug.LogWarning($"[WorldTreeScreen] 빌드 설정에 없는 씬: {backScene}");
        }

        public void SetGold(long value) => goldText.text = value.ToString("N0");
        public void SetMycelite(long value) => myceliteText.text = value.ToString("N0");

        public void SetBasket(double value)
        {
            basket = value;
            basketText.text = Short(value);
        }

        // 임시: 문구만 띄운다. 골드 지급·바구니 비우기는 바구니 기능을 만들 때 붙인다
        void CollectBasket() => ShowToast($"{System.Math.Floor(basket):N0}골드를 획득했습니다!");

        public void ShowToast(string message)
        {
            if (toastPrefab == null) return;
            if (toast == null) toast = Instantiate(toastPrefab, transform, false);
            toast.Show(message, toastDuration);
        }

        // 1234 → 1.2k, 1234567 → 1.2M (1000 미만은 그대로)
        static string Short(double v)
        {
            if (v < 1e3) return Mathf.FloorToInt((float)v).ToString();
            if (v < 1e6) return (v / 1e3).ToString("0.#") + "k";
            if (v < 1e9) return (v / 1e6).ToString("0.#") + "M";
            return (v / 1e9).ToString("0.#") + "B";
        }
    }
}
