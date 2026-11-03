using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml;

namespace MoreMush.Mobile
{
    // .xlsx 읽기 전용 최소 구현. xlsx는 zip 안에 든 XML 묶음이라 외부 라이브러리 없이 시트별 셀 문자열만 뽑는다.
    // 서식·병합은 무시하고 저장된 값만 본다 (수식 칸은 엑셀이 마지막으로 계산해 둔 값)
    public static class XlsxReader
    {
        const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

        // 시트 이름 → 행 목록 (시트에 놓인 순서). 각 행은 A열부터의 셀 문자열이고 빈 칸은 ""다. 빈 행도 자리를 지킨다
        public static Dictionary<string, List<string[]>> Read(byte[] bytes)
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            var shared = ReadSharedStrings(zip);
            var result = new Dictionary<string, List<string[]>>();
            foreach (var sheet in ReadSheetPaths(zip))
                result[sheet.Key] = ReadSheet(zip, sheet.Value, shared);
            return result;
        }

        static XmlDocument Load(ZipArchive zip, string path)
        {
            var entry = zip.GetEntry(path);
            if (entry == null) return null;
            var doc = new XmlDocument();
            using (var s = entry.Open()) doc.Load(s);
            return doc;
        }

        // workbook.xml의 시트 이름 → 실제 시트 XML 경로
        static List<KeyValuePair<string, string>> ReadSheetPaths(ZipArchive zip)
        {
            var targets = new Dictionary<string, string>();
            var rels = Load(zip, "xl/_rels/workbook.xml.rels");
            if (rels != null)
                foreach (XmlElement r in rels.GetElementsByTagName("Relationship", PackageRelNs))
                {
                    string target = r.GetAttribute("Target");
                    targets[r.GetAttribute("Id")] = target.StartsWith("/") ? target.Substring(1) : "xl/" + target;
                }

            var list = new List<KeyValuePair<string, string>>();
            var workbook = Load(zip, "xl/workbook.xml");
            if (workbook == null) throw new InvalidDataException("xlsx가 아니다 (xl/workbook.xml 없음)");
            foreach (XmlElement s in workbook.GetElementsByTagName("sheet", MainNs))
            {
                if (targets.TryGetValue(s.GetAttribute("id", RelNs), out var path))
                    list.Add(new KeyValuePair<string, string>(s.GetAttribute("name"), path));
            }
            return list;
        }

        static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var list = new List<string>();
            var doc = Load(zip, "xl/sharedStrings.xml");
            if (doc == null) return list;
            foreach (XmlElement si in doc.GetElementsByTagName("si", MainNs)) list.Add(Text(si));
            return list;
        }

        // 서식이 섞인 문자열은 <r><t>로 쪼개져 있다. 후리가나(<rPh>)는 뺀다.
        // GetElementsByTagName은 칸마다 문서 변경 알림을 거는 목록을 만들어 칸이 많을수록 느려진다 — 자식을 직접 돈다
        static readonly System.Text.StringBuilder textBuilder = new System.Text.StringBuilder();

        static string Text(XmlElement e)
        {
            textBuilder.Clear();
            AppendText(e, textBuilder);
            return textBuilder.ToString();
        }

        static void AppendText(XmlNode node, System.Text.StringBuilder sb)
        {
            for (var child = node.FirstChild; child != null; child = child.NextSibling)
            {
                if (!(child is XmlElement el) || el.LocalName == "rPh") continue;
                if (el.LocalName == "t") sb.Append(el.InnerText);
                else AppendText(el, sb);
            }
        }

        static XmlElement Child(XmlElement parent, string localName)
        {
            for (var child = parent.FirstChild; child != null; child = child.NextSibling)
                if (child is XmlElement el && el.LocalName == localName) return el;
            return null;
        }

        const int MaxRows = 65536;      // 아래쪽 먼 칸에 서식만 남아 있어도 빈 행을 수십만 개 만들지 않게

        static List<string[]> ReadSheet(ZipArchive zip, string path, List<string> shared)
        {
            var rows = new List<string[]>();
            var doc = Load(zip, path);
            if (doc == null) return rows;

            foreach (XmlElement row in doc.GetElementsByTagName("row", MainNs))
            {
                // r 속성은 1부터. 비어 있는 행은 파일에 없을 수 있어 그만큼 빈 행으로 채운다
                if (int.TryParse(row.GetAttribute("r"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int r))
                {
                    if (r > MaxRows) break;
                    while (rows.Count < r - 1) rows.Add(System.Array.Empty<string>());
                }

                var cells = new List<string>();
                int next = 0;
                foreach (XmlNode node in row.ChildNodes)
                {
                    if (!(node is XmlElement c) || c.LocalName != "c") continue;
                    int col = Column(c.GetAttribute("r"), next);
                    while (cells.Count < col) cells.Add("");
                    cells.Add(Value(c, shared));
                    next = col + 1;
                }
                rows.Add(cells.ToArray());
            }
            return rows;
        }

        static string Value(XmlElement c, List<string> shared)
        {
            string type = c.GetAttribute("t");
            if (type == "inlineStr")
            {
                var inline = Child(c, "is");
                return inline != null ? Text(inline) : "";
            }

            var v = Child(c, "v");
            string raw = v != null ? v.InnerText : "";
            if (type == "s") return int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int i) && i >= 0 && i < shared.Count ? shared[i] : "";
            if (type == "b") return raw == "1" ? "TRUE" : "FALSE";
            return raw;
        }

        // "AB12" → 27 (A = 0). 주소가 없으면 앞 칸 다음
        static int Column(string address, int fallback)
        {
            if (string.IsNullOrEmpty(address)) return fallback;
            int col = 0, i = 0;
            for (; i < address.Length && char.IsLetter(address[i]); i++)
                col = col * 26 + (char.ToUpperInvariant(address[i]) - 'A' + 1);
            return i == 0 ? fallback : col - 1;
        }
    }
}
