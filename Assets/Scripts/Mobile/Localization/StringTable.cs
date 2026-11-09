using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MoreMush.Mobile
{
    // 문자열 표 String.xlsx(→ Resources/Tables/String.bytes) 읽기·검사. 게임(Loc)과 표 가져오기(TableImporter)가 같이 쓴다.
    // 시트 "String": 1행은 열 이름 key | ko | en | memo (memo는 번역 참고용, 게임은 안 읽는다). 2행부터 한 줄 = 문구 하나.
    // 문구 안의 {0} {1}은 코드가 넣는 값 자리 — 언어마다 같은 번호가 있어야 한다. 줄바꿈은 셀 안 줄바꿈 또는 \n
    public static class StringTable
    {
        public const string SheetName = "String";
        public const string KeyColumn = "key";
        public const string ResourceName = "Tables/String";

        // 열 이름 = 언어 코드. GameLanguage 순서와 같다
        public static readonly string[] LanguageColumns = { "ko", "en" };

        static readonly Regex Placeholder = new Regex(@"\{(\d+)(?::[^}]*)?\}");

        // key → 언어별 문구 (LanguageColumns 순서). 빈 칸은 null
        public static Dictionary<string, string[]> Parse(byte[] xlsx, List<string> issues)
        {
            var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
            Dictionary<string, List<string[]>> sheets;
            try { sheets = XlsxReader.Read(xlsx); }
            catch (Exception e)
            {
                issues.Add("xlsx를 읽지 못했다: " + e.Message);
                return result;
            }
            if (!sheets.TryGetValue(SheetName, out var rows) || rows.Count == 0)
            {
                issues.Add($"\"{SheetName}\" 시트가 없다");
                return result;
            }

            var header = rows[0];
            int keyCol = IndexOf(header, KeyColumn);
            var langCols = new int[LanguageColumns.Length];
            for (int i = 0; i < langCols.Length; i++) langCols[i] = IndexOf(header, LanguageColumns[i]);
            if (keyCol < 0) issues.Add($"1행에 \"{KeyColumn}\" 열이 없다");
            for (int i = 0; i < langCols.Length; i++)
                if (langCols[i] < 0) issues.Add($"1행에 \"{LanguageColumns[i]}\" 열이 없다");
            if (keyCol < 0 || Array.IndexOf(langCols, -1) >= 0) return result;

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                string key = Cell(row, keyCol).Trim();
                if (key.Length == 0) continue;   // 빈 줄·구분 줄
                var texts = new string[langCols.Length];
                for (int i = 0; i < langCols.Length; i++)
                {
                    string t = Cell(row, langCols[i]).Replace("\\n", "\n").Replace("\r\n", "\n");
                    texts[i] = t.Length == 0 ? null : t;
                }
                if (result.ContainsKey(key)) { issues.Add($"{r + 1}행: key \"{key}\"가 겹친다"); continue; }
                if (texts[0] == null) issues.Add($"{r + 1}행 {key}: 기본 언어({LanguageColumns[0]}) 문구가 비었다");
                CheckPlaceholders(r + 1, key, texts, issues);
                result[key] = texts;
            }
            return result;
        }

        // 표 가져오기용: 문제는 issues에, 통과한 문구 수를 돌려준다
        public static int Validate(byte[] xlsx, List<string> issues) => Parse(xlsx, issues).Count;

        // 번역이 빠진 칸 (경고만 — 게임은 기본 언어로 보여 준다)
        public static List<string> MissingTranslations(Dictionary<string, string[]> table)
        {
            var list = new List<string>();
            foreach (var pair in table)
                for (int i = 1; i < pair.Value.Length; i++)
                    if (pair.Value[i] == null) list.Add($"{pair.Key} ({LanguageColumns[i]})");
            return list;
        }

        static void CheckPlaceholders(int line, string key, string[] texts, List<string> issues)
        {
            if (texts[0] == null) return;
            var expected = Numbers(texts[0]);
            for (int i = 1; i < texts.Length; i++)
            {
                if (texts[i] == null) continue;
                var got = Numbers(texts[i]);
                if (!expected.SetEquals(got))
                    issues.Add($"{line}행 {key}: {LanguageColumns[i]} 문구의 {{번호}} 자리가 {LanguageColumns[0]}와 다르다");
            }
        }

        static HashSet<int> Numbers(string text)
        {
            var set = new HashSet<int>();
            foreach (Match m in Placeholder.Matches(text)) set.Add(int.Parse(m.Groups[1].Value));
            return set;
        }

        static int IndexOf(string[] header, string name)
        {
            for (int i = 0; i < header.Length; i++)
                if (string.Equals(header[i]?.Trim(), name, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        static string Cell(string[] row, int col) => col < row.Length && row[col] != null ? row[col] : "";
    }
}
