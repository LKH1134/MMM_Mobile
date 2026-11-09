using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    public enum GameLanguage { Korean = 0, English = 1 }   // 순서 = StringTable.LanguageColumns

    // 게임 언어와 문구 찾기. 문구는 문자열 표(String.xlsx)에서 key로 꺼낸다.
    // - 처음 실행할 때 휴대폰 언어를 읽어 정한다: 한국어 기기는 한국어, 나머지는 영어. 정한 값은 저장하고 이후엔 옵션에서 바꾼 값을 쓴다
    //   (플레이스토어 언어는 따로 읽을 수 없다 — 스토어도 휴대폰 언어를 따른다)
    // - 번역이 비었으면 한국어, 표에 없는 key면 넘겨준 기본 문구(없으면 key)를 보여 준다 → 옮기는 중에도 화면이 비지 않는다
    public static class Loc
    {
        public const string LanguageKey = "Option.Language";

        static Dictionary<string, string[]> table;
        static bool hasLanguage;
        static GameLanguage current;

        // 언어가 바뀌면 부른다 (LocalizedText·화면이 다시 그린다)
        public static event Action Changed;

        public static GameLanguage Current
        {
            get
            {
                if (!hasLanguage) LoadLanguage();
                return current;
            }
        }

        public static void Set(GameLanguage language)
        {
            if (hasLanguage && current == language) return;
            current = language;
            hasLanguage = true;
            PlayerPrefs.SetInt(LanguageKey, (int)language);
            SaveScheduler.MarkDirty();
            Changed?.Invoke();
        }

        // 표의 문구. 표에 없으면 fallback (null이면 key 그대로)
        public static string T(string key, string fallback = null)
        {
            var row = Row(key);
            if (row == null) return fallback ?? key;
            return row[(int)Current] ?? row[0] ?? fallback ?? key;
        }

        // {0} {1} 자리에 값을 넣은 문구 (숫자 형식은 기기 언어와 상관없이 같다 — AppSettings가 문화권을 Invariant로 고정).
        // 기본 문구를 받는 겹친 함수는 두지 않는다 — F(key, "문자열 값")이 기본 문구로 잘못 묶인다
        public static string F(string key, params object[] args) => Format(T(key), key, args);

        public static bool Has(string key) => Row(key) != null;

        // 모든 언어 문구 중 하나와 같은지 (LocalizedText가 "아직 코드가 바꾸지 않은 글자"인지 볼 때)
        public static bool IsAnyTranslation(string key, string text)
        {
            var row = Row(key);
            if (row == null) return false;
            foreach (var t in row) if (t != null && t == text) return true;
            return false;
        }

        public static GameLanguage DetectDeviceLanguage() =>
            Application.systemLanguage == SystemLanguage.Korean ? GameLanguage.Korean : GameLanguage.English;

        public static string NativeName(GameLanguage language) => language == GameLanguage.Korean ? "한국어" : "English";

        static string Format(string text, string key, object[] args)
        {
            if (args == null || args.Length == 0) return text;
            try { return string.Format(System.Globalization.CultureInfo.InvariantCulture, text, args); }
            catch (FormatException)
            {
                Debug.LogError($"[Loc] {key}: 문구의 {{번호}} 자리가 넘긴 값과 맞지 않다 — \"{text}\"");
                return text;
            }
        }

        static void LoadLanguage()
        {
            hasLanguage = true;
            if (PlayerPrefs.HasKey(LanguageKey))
            {
                int saved = PlayerPrefs.GetInt(LanguageKey);
                current = Enum.IsDefined(typeof(GameLanguage), saved) ? (GameLanguage)saved : DetectDeviceLanguage();
                return;
            }
            // 처음 실행: 휴대폰 언어로 정하고 저장한다 (나중에 휴대폰 언어를 바꿔도 게임 언어는 옵션에서만 바뀐다)
            current = DetectDeviceLanguage();
            PlayerPrefs.SetInt(LanguageKey, (int)current);
            SaveScheduler.MarkDirty();
        }

        static string[] Row(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (table == null) LoadTable();
            return table.TryGetValue(key, out var row) ? row : null;
        }

        static void LoadTable()
        {
            var asset = Resources.Load<TextAsset>(StringTable.ResourceName);
            if (asset == null)
            {
                Debug.LogError($"[Loc] Resources/{StringTable.ResourceName}.bytes가 없다 — 메뉴 MoreMush/Data/Reimport Tables로 만든다");
                table = new Dictionary<string, string[]>();
                return;
            }
            var issues = new List<string>();
            table = StringTable.Parse(asset.bytes, issues);
            Resources.UnloadAsset(asset);
            if (issues.Count > 0) Debug.LogError("[Loc] 문자열 표 문제:\n" + string.Join("\n", issues));
        }

        // 표를 고친 뒤 에디터에서 다시 읽는다
        public static void Reload()
        {
            table = null;
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            table = null;
            hasLanguage = false;
            Changed = null;
        }
    }
}
