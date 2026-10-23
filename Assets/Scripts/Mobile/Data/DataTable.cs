using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace MoreMush.Mobile
{
    // 모든 데이터 표의 행이 가지는 공통 열
    public abstract class TableRow
    {
        public int index;          // 표마다 정해진 범위의 고유 번호 (버섯 1001~, 수확기 2001~ …)
        public string key;         // 코드·리소스에서 부를 영문 식별자. 표 안에서 겹치면 안 된다
        public string name;        // 인게임 이름
        public string desc;        // 인게임 설명
        public string icon;        // 아이콘 스프라이트 경로 (Resources 기준)
    }

    // 엑셀 시트 한 장을 읽은 표. 시트 형식:
    //   1행 = 열 이름 (행 클래스의 필드 이름과 같아야 한다)
    //   2행 = 자료형 (int, long, float, string, bool, int[], 열거형 이름) — 사람이 보는 용도, 필드와 다르면 경고
    //   3행 = 설명
    //   4행부터 = 데이터. index 칸이 빈 행은 건너뛴다
    // 열 이름이 '#'으로 시작하면 메모 열이라 읽지 않는다
    public class DataTable<T> where T : TableRow, new()
    {
        public const int HeaderRow = 0, TypeRow = 1, DescRow = 2, FirstDataRow = 3;

        readonly List<T> rows = new List<T>();
        readonly Dictionary<int, T> byIndex = new Dictionary<int, T>();
        readonly Dictionary<string, T> byKey = new Dictionary<string, T>();

        public string Name { get; }
        public int IndexMin { get; }
        public int IndexMax { get; }
        public IReadOnlyList<T> All => rows;
        public int Count => rows.Count;

        public DataTable(string name, int indexMin, int indexMax)
        {
            Name = name;
            IndexMin = indexMin;
            IndexMax = indexMax;
        }

        public T Get(int index) => byIndex.TryGetValue(index, out var row) ? row : null;
        public bool TryGet(int index, out T row) => byIndex.TryGetValue(index, out row);
        public T GetByKey(string key) => key != null && byKey.TryGetValue(key, out var row) ? row : null;

        // 시트를 읽어 채운다. 문제는 issues에 쌓고, 고칠 수 없는 행만 버린다
        public void Load(List<string[]> sheet, List<string> issues)
        {
            rows.Clear();
            byIndex.Clear();
            byKey.Clear();
            if (sheet == null || sheet.Count <= HeaderRow)
            {
                issues.Add($"[{Name}] 시트가 비어 있다");
                return;
            }

            var columns = MapColumns(sheet, issues);
            for (int r = FirstDataRow; r < sheet.Count; r++)
            {
                var cells = sheet[r];
                if (string.IsNullOrWhiteSpace(Cell(cells, columns.indexColumn))) continue;

                var row = new T();
                bool ok = true;
                foreach (var col in columns.fields)
                {
                    string raw = Cell(cells, col.column);
                    if (TryConvert(raw, col.field.FieldType, out object value)) col.field.SetValue(row, value);
                    else
                    {
                        issues.Add($"[{Name}] {r + 1}행 {col.field.Name}: '{raw}'을(를) {TypeName(col.field.FieldType)}(으)로 읽을 수 없다");
                        if (col.field.Name == nameof(TableRow.index)) ok = false;
                    }
                }
                if (ok) Add(row, r, issues);
            }
        }

        void Add(T row, int r, List<string> issues)
        {
            if (row.index < IndexMin || row.index > IndexMax)
            {
                issues.Add($"[{Name}] {r + 1}행 index {row.index}: 범위({IndexMin}~{IndexMax}) 밖이라 뺐다");
                return;
            }
            if (byIndex.ContainsKey(row.index))
            {
                issues.Add($"[{Name}] {r + 1}행 index {row.index}: 겹쳐서 뺐다");
                return;
            }
            if (!string.IsNullOrEmpty(row.key))
            {
                if (byKey.ContainsKey(row.key)) issues.Add($"[{Name}] {r + 1}행 key '{row.key}': 겹친다 (GetByKey는 앞의 것)");
                else byKey.Add(row.key, row);
            }
            rows.Add(row);
            byIndex.Add(row.index, row);
        }

        struct FieldColumn
        {
            public FieldInfo field;
            public int column;
        }

        (List<FieldColumn> fields, int indexColumn) MapColumns(List<string[]> sheet, List<string> issues)
        {
            var header = sheet[HeaderRow];
            var types = sheet.Count > TypeRow ? sheet[TypeRow] : new string[0];
            var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
            var byName = new Dictionary<string, FieldInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in fields) byName[f.Name] = f;

            var list = new List<FieldColumn>();
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int indexColumn = -1;
            for (int c = 0; c < header.Length; c++)
            {
                string name = header[c].Trim();
                if (name.Length == 0 || name.StartsWith("#")) continue;
                if (!byName.TryGetValue(name, out var field))
                {
                    issues.Add($"[{Name}] 모르는 열 '{name}' — 코드에 필드가 없어 읽지 않는다 (메모라면 '#'을 붙인다)");
                    continue;
                }
                if (!found.Add(name))
                {
                    issues.Add($"[{Name}] 열 '{name}'이(가) 두 번 있다 — 앞의 것만 읽는다");
                    continue;
                }

                string declared = c < types.Length ? types[c].Trim() : "";
                if (declared.Length > 0 && !string.Equals(declared, TypeName(field.FieldType), StringComparison.OrdinalIgnoreCase))
                    issues.Add($"[{Name}] 열 '{name}' 자료형 칸이 '{declared}'인데 코드는 {TypeName(field.FieldType)}");

                list.Add(new FieldColumn { field = field, column = c });
                if (field.Name == nameof(TableRow.index)) indexColumn = c;
            }

            foreach (var f in fields)
                if (!found.Contains(f.Name)) issues.Add($"[{Name}] 열 '{f.Name}'이(가) 없다 — 기본값으로 둔다");
            return (list, indexColumn);
        }

        static string Cell(string[] cells, int column) => column >= 0 && column < cells.Length ? cells[column] : "";

        // 빈 칸은 자료형의 기본값 (숫자 0, 문자열 null, 배열 빈 배열)
        static bool TryConvert(string raw, Type type, out object value)
        {
            raw = raw?.Trim() ?? "";
            var inv = CultureInfo.InvariantCulture;
            const NumberStyles num = NumberStyles.Float;

            if (type == typeof(string)) { value = raw.Length == 0 ? null : raw; return true; }
            if (type == typeof(int[])) return TryIntArray(raw, out value);
            if (raw.Length == 0)
            {
                value = type.IsValueType ? Activator.CreateInstance(type) : null;
                return true;
            }

            value = null;
            // 엑셀은 정수도 "1001.0"이나 "1E+3"으로 저장할 수 있어 double로 읽은 뒤 정수인지 본다
            if (type == typeof(int) || type == typeof(long))
            {
                if (!double.TryParse(raw, num, inv, out double d) || d != Math.Floor(d)) return false;
                if (type == typeof(int))
                {
                    if (d < int.MinValue || d > int.MaxValue) return false;
                    value = (int)d;
                }
                else value = (long)d;
                return true;
            }
            if (type == typeof(float)) { if (!float.TryParse(raw, num, inv, out float f)) return false; value = f; return true; }
            if (type == typeof(double)) { if (!double.TryParse(raw, num, inv, out double d)) return false; value = d; return true; }
            if (type == typeof(bool))
            {
                switch (raw.ToUpperInvariant())
                {
                    case "TRUE": case "1": case "Y": case "O": value = true; return true;
                    case "FALSE": case "0": case "N": case "X": value = false; return true;
                    default: return false;
                }
            }
            if (type.IsEnum)
            {
                try { value = Enum.Parse(type, raw, true); return Enum.IsDefined(type, value); }
                catch (ArgumentException) { return false; }
            }
            return false;
        }

        // "3001, 3002" 또는 "3001;3002"
        static bool TryIntArray(string raw, out object value)
        {
            var list = new List<int>();
            foreach (var part in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                {
                    value = null;
                    return false;
                }
                list.Add(n);
            }
            value = list.ToArray();
            return true;
        }

        public static string TypeName(Type type)
        {
            if (type == typeof(int)) return "int";
            if (type == typeof(long)) return "long";
            if (type == typeof(float)) return "float";
            if (type == typeof(double)) return "double";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(string)) return "string";
            if (type == typeof(int[])) return "int[]";
            return type.Name;
        }
    }
}
