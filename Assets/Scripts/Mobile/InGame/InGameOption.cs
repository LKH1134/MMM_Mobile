using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // InGame 옵션 팝업 내용. 배경음·효과음 슬라이더(값만 저장한다 — 소리 연결은 추후), 로비(Main)로 돌아가기.
    // 닫기는 UiPopup의 CloseButton이 맡는다
    public class InGameOption : MonoBehaviour
    {
        public const string BgmKey = "Option.BgmVolume";
        public const string SfxKey = "Option.SfxVolume";

        [SerializeField] Slider bgmSlider;
        [SerializeField] Slider sfxSlider;
        [FormerlySerializedAs("worldTreeButton")]
        [SerializeField] Button lobbyButton;
        [SerializeField] string lobbyScene = MainNavigation.MainScene;

        void Awake()
        {
            Bind(bgmSlider, BgmKey);
            Bind(sfxSlider, SfxKey);
            if (lobbyButton != null) lobbyButton.onClick.AddListener(GoLobby);
        }

        static void Bind(Slider slider, string key)
        {
            if (slider == null) return;
            slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(key, 1f));
            slider.onValueChanged.AddListener(v => PlayerPrefs.SetFloat(key, v));
        }

        public void GoLobby()
        {
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            SceneLoader.Load(lobbyScene);
        }
    }
}
