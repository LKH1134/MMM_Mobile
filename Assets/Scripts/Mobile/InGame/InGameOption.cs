using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // InGame 옵션 팝업 내용. 배경음·효과음 슬라이더(값만 저장한다 — 소리 연결은 추후), 세계수로 돌아가기.
    // 닫기는 UiPopup의 CloseButton이 맡는다
    public class InGameOption : MonoBehaviour
    {
        public const string BgmKey = "Option.BgmVolume";
        public const string SfxKey = "Option.SfxVolume";

        [SerializeField] Slider bgmSlider;
        [SerializeField] Slider sfxSlider;
        [SerializeField] Button worldTreeButton;
        [SerializeField] string worldTreeScene = "WorldTree";

        void Awake()
        {
            Bind(bgmSlider, BgmKey);
            Bind(sfxSlider, SfxKey);
            if (worldTreeButton != null) worldTreeButton.onClick.AddListener(GoWorldTree);
        }

        static void Bind(Slider slider, string key)
        {
            if (slider == null) return;
            slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(key, 1f));
            slider.onValueChanged.AddListener(v => PlayerPrefs.SetFloat(key, v));
        }

        public void GoWorldTree()
        {
            PlayerPrefs.Save();
            if (Application.CanStreamedLevelBeLoaded(worldTreeScene)) SceneManager.LoadScene(worldTreeScene);
            else Debug.LogWarning($"[InGameOption] 빌드 설정에 없는 씬: {worldTreeScene}");
        }
    }
}
