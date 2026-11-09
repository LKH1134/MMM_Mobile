using TMPro;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 프리팹에 고정으로 적힌 글자(버튼·탭·제목)를 문자열 표의 key 문구로 바꾼다. 언어를 바꾸면 바로 다시 쓴다.
    // 코드가 글자를 바꾸는 텍스트에 붙어 있어도 안전하다: 지금 글자가 이 key의 어느 언어 문구와도 다르면(코드가 바꿨으면) 건드리지 않는다.
    // 코드가 만드는 글자는 이 컴포넌트 대신 코드에서 Loc.T / Loc.F를 쓴다
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] string key;

        TMP_Text text;

        public string Key => key;

        void Awake() => text = GetComponent<TMP_Text>();

        void OnEnable()
        {
            Loc.Changed += Apply;
            Apply();
        }

        void OnDisable() => Loc.Changed -= Apply;

        void Apply()
        {
            if (text == null || string.IsNullOrEmpty(key) || !Loc.Has(key)) return;
            string current = text.text;
            if (!string.IsNullOrEmpty(current) && !Loc.IsAnyTranslation(key, current)) return;   // 코드가 바꾼 글자
            string next = Loc.T(key);
            if (current != next) text.text = next;
        }

#if UNITY_EDITOR
        // 이관 도구가 붙일 때 key를 넣는다
        public void EditorSetKey(string value) => key = value;
#endif
    }
}
