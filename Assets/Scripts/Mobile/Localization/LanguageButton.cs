using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 옵션 창의 언어 버튼 (Language/Dropdown). 지금 언어를 그 언어 이름으로 보여 주고(한국어 / English), 누르면 다음 언어로 바꾼다.
    // 바꾸면 Loc.Changed로 화면 글자가 바로 다시 그려진다
    [RequireComponent(typeof(Button))]
    public class LanguageButton : MonoBehaviour
    {
        [SerializeField] TMP_Text label;

        void Awake()
        {
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
            GetComponent<Button>().onClick.AddListener(Next);
        }

        void OnEnable()
        {
            Loc.Changed += Show;
            Show();
        }

        void OnDisable() => Loc.Changed -= Show;

        void Next()
        {
            int count = System.Enum.GetValues(typeof(GameLanguage)).Length;
            Loc.Set((GameLanguage)(((int)Loc.Current + 1) % count));
        }

        void Show()
        {
            if (label != null) label.text = Loc.NativeName(Loc.Current);
        }
    }
}
