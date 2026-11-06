using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 옵션 창의 ON/OFF 스위치 (배경음·효과음). 프리팹의 Toggle은 켜짐 그림(OnState)만 켜고 끌 뿐 값을 저장하지도 소리에 반영하지도 않았다 —
    // 이 컴포넌트가 저장된 값을 창이 열릴 때 보여 주고, 누르면 저장한 뒤 소리에 바로 반영한다.
    // 배경음 = BgmPlayer.SetEnabled(꺼도 슬라이더 음량은 그대로 남는다), 효과음 = 값만 저장 (효과음 연결은 추후)
    [RequireComponent(typeof(Toggle))]
    public class OptionSwitch : MonoBehaviour
    {
        public enum Kind { Bgm, Sfx }

        [SerializeField] Kind kind = Kind.Bgm;

        public const string BgmOnKey = "Option.BgmOn";
        public const string SfxOnKey = "Option.SfxOn";

        Toggle toggle;
        Transform onState;      // 켜짐 모습 (프리팹의 "OnState" 자식)

        public static bool IsOn(Kind kind) => PlayerPrefs.GetInt(KeyOf(kind), 1) == 1;

        static string KeyOf(Kind kind) => kind == Kind.Bgm ? BgmOnKey : SfxOnKey;

        void Awake()
        {
            toggle = GetComponent<Toggle>();
            onState = transform.Find("OnState");
            toggle.onValueChanged.AddListener(OnChanged);       // 프리팹의 기존 연결(OnState 켜고 끄기)은 그대로 둔다
        }

        // 창이 열릴 때마다 저장된 값을 보여 준다 (다른 씬의 옵션 창에서 바꾼 값도 따라온다)
        void OnEnable()
        {
            bool on = IsOn(kind);
            toggle.SetIsOnWithoutNotify(on);
            Show(on);
        }

        void OnChanged(bool on)
        {
            PlayerPrefs.SetInt(KeyOf(kind), on ? 1 : 0);
            SaveScheduler.MarkDirty();
            Show(on);
            if (kind == Kind.Bgm) BgmPlayer.SetEnabled(on);
        }

        void Show(bool on)
        {
            if (onState != null && onState.gameObject.activeSelf != on) onState.gameObject.SetActive(on);
        }
    }
}
