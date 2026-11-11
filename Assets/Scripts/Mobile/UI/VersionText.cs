using TMPro;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 시작 화면 버전 글자: 문자열 표 start.version("버전 {0}")에 앱 버전(Player Settings Version)을 넣는다. 언어를 바꾸면 다시 쓴다
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public class VersionText : MonoBehaviour
    {
        TMP_Text text;

        void Awake() => text = GetComponent<TMP_Text>();

        void OnEnable()
        {
            Loc.Changed += Apply;
            Apply();
        }

        void OnDisable() => Loc.Changed -= Apply;

        void Apply()
        {
            if (text != null) text.text = Loc.F("start.version", Application.version);
        }
    }
}
