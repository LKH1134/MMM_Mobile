using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // InGame 옵션 팝업 내용. 배경음 슬라이더(BgmPlayer 음량)·효과음 슬라이더(값만 저장 — 효과음 연결은 추후), 로비(Main)로 돌아가기.
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
            Bind(bgmSlider, BgmKey, BgmPlayer.ApplyVolume);
            Bind(sfxSlider, SfxKey, null);
            if (lobbyButton != null) lobbyButton.onClick.AddListener(GoLobby);
        }

        static void Bind(Slider slider, string key, System.Action changed)
        {
            if (slider == null) return;
            slider.SetValueWithoutNotify(PlayerPrefs.GetFloat(key, 1f));
            slider.onValueChanged.AddListener(v => { PlayerPrefs.SetFloat(key, v); SaveScheduler.MarkDirty(); changed?.Invoke(); });
        }

        public void GoLobby()
        {
            PlayerPrefs.Save();
            // 배속은 여기서 풀지 않는다 — 풀면 읽는 동안 판이 계속 돌아 시간이 끝나거나 보스가 죽어 보상·진행이 처리된다.
            // 씬이 바뀌면 StageHud.OnDestroy와 AppSettings가 1로 되돌린다 (로딩 화면은 실제 시간으로 돈다)
            SceneLoader.Load(lobbyScene);
        }
    }
}
