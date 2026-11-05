using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 배경음 음량 슬라이더. OptionPopup 프리팹의 SoundBgm/Slider에 붙어 있어 Start·Main·InGame 옵션 창이 모두 같은 값을 쓴다.
    // 창이 열릴 때 저장된 값을 보여 주고, 움직이면 바로 BgmPlayer 음량에 반영하고 저장한다
    [RequireComponent(typeof(Slider))]
    public class BgmVolumeSlider : MonoBehaviour
    {
        Slider slider;

        void Awake()
        {
            slider = GetComponent<Slider>();
            slider.onValueChanged.AddListener(BgmPlayer.SetVolume);
        }

        void OnEnable() => slider.SetValueWithoutNotify(BgmPlayer.Volume);
    }
}
