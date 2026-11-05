using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace MoreMush.Mobile
{
    // 폰트 굽기: 게임이 화면에 쓸 수 있는 글자(표 전체·코드 문자열·씬/프리팹 글자·ASCII·자주 쓰는 기호)를 모아
    // LINESeedKR SDF 두 개를 정적(Static) 아틀라스로 다시 만든다. 실행 중엔 아틀라스가 바뀌지 않는다.
    // 동적 아틀라스는 플레이 중 이미 떠 있던 전투 HUD 글자가 사라지는 문제가 있었다 (2026-11-05).
    // 표·코드·씬에 새 글자를 넣었으면 이 메뉴를 다시 돌린다 — Reimport Tables가 폰트에 없는 표 글자를 경고한다
    public static class FontBaker
    {
        const int SamplingPointSize = 60;   // 화면 글자는 최대 60px 안팎. SDF라 크게 그려도 깨끗하다
        const int Padding = 6;              // 샘플 크기의 10% (외곽선 0.25까지 여유)
        const int AtlasSize = 1024;         // 페이지 하나 1MB(Alpha8). 모자라면 페이지가 늘어난다

        public static readonly string[] FontPaths = { "Assets/Fonts/LINESeedKR-Bd SDF.asset", "Assets/Fonts/LINESeedKR-Rg SDF.asset" };

        const string ExtraSymbols = "·…×÷−–—→←↑↓★☆♥%‰°™©®“”‘’「」『』【】《》〈〉~!?！？•●○■□▲△▼▽◆◇♪♬∞≈≤≥";

        [MenuItem("MoreMush/Fonts/Bake Static Atlases")]
        static void BakeMenu()
        {
            string chars = CollectCharacters();
            foreach (var path in FontPaths)
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null) { Debug.LogError($"[FontBaker] 폰트 없음: {path}"); continue; }
                var missing = Bake(font, chars);
                Debug.Log($"[FontBaker] {font.name}: 글자 {font.characterTable.Count}개 · 아틀라스 {font.atlasTextures.Length}장" + (missing.Length > 0 ? $" · 폰트에 없는 글자 {missing.Length}개: {missing}" : ""));
            }
            AssetDatabase.SaveAssets();
        }

        // 게임이 화면에 쓸 수 있는 글자 모음 (중복 없음, 정렬)
        public static string CollectCharacters()
        {
            var set = new HashSet<char>();
            void Add(string s) { if (s == null) return; foreach (var c in s) if (c >= ' ' && !char.IsSurrogate(c)) set.Add(c); }

            for (int c = 0x20; c < 0x7f; c++) set.Add((char)c);
            Add(ExtraSymbols);

            foreach (var xlsx in Directory.GetFiles("Assets/Data/Tables", "*.xlsx", SearchOption.AllDirectories))
                foreach (var sheet in XlsxReader.Read(File.ReadAllBytes(xlsx)))
                {
                    Add(sheet.Key);
                    foreach (var row in sheet.Value) foreach (var cell in row) Add(cell);
                }

            var literal = new Regex(@"""((?:[^""\\\n]|\\.)*)""");
            var charLiteral = new Regex(@"'((?:[^'\\\n]|\\.))'");
            foreach (var cs in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                if (cs.Replace('\\', '/').Contains("/Editor/")) continue;
                string t = File.ReadAllText(cs);
                t = Regex.Replace(t, @"/\*.*?\*/", "", RegexOptions.Singleline);
                t = Regex.Replace(t, @"//[^\n]*", "");
                foreach (Match m in literal.Matches(t)) Add(m.Groups[1].Value);
                foreach (Match m in charLiteral.Matches(t)) Add(m.Groups[1].Value);
            }

            var yamlText = new Regex(@"m_text: (?:""((?:[^""\\]|\\.)*)""|'((?:[^']|'')*)'|([^\n]*))");
            foreach (var root in new[] { "Assets/Scenes", "Assets/Prefabs" })
                foreach (var file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
                {
                    if (!file.EndsWith(".unity") && !file.EndsWith(".prefab")) continue;
                    foreach (Match m in yamlText.Matches(File.ReadAllText(file)))
                        Add(Regex.Replace(m.Groups[1].Value + m.Groups[2].Value + m.Groups[3].Value, @"\\u([0-9a-fA-F]{4})", u => ((char)System.Convert.ToInt32(u.Groups[1].Value, 16)).ToString()));
                }

            var list = set.ToList();
            list.Sort();
            return new string(list.ToArray());
        }

        // 정적 아틀라스로 다시 굽는다. 돌려주는 값 = 원본 글꼴(ttf)에 아예 없는 글자
        static string Bake(TMP_FontAsset font, string chars)
        {
            var so = new SerializedObject(font);
            // 정적 폰트는 원본 글꼴 연결(m_SourceFontFile)이 비어 있다 — 생성 설정의 GUID로 ttf를 다시 연결한다.
            // 비어 있으면 TMP가 생성 설정(옛 90pt·여백 9)으로 글꼴을 다시 읽어 크기가 어긋난다
            var source = so.FindProperty("m_SourceFontFile");
            if (source.objectReferenceValue == null)
            {
                string guid = so.FindProperty("m_CreationSettings.sourceFontFileGUID").stringValue;
                source.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
            }
            so.FindProperty("m_CreationSettings.pointSize").intValue = SamplingPointSize;
            so.FindProperty("m_CreationSettings.padding").intValue = Padding;
            so.FindProperty("m_CreationSettings.atlasWidth").intValue = AtlasSize;
            so.FindProperty("m_CreationSettings.atlasHeight").intValue = AtlasSize;
            so.FindProperty("m_AtlasPopulationMode").intValue = (int)AtlasPopulationMode.Dynamic;   // 굽는 동안만 동적
            so.FindProperty("m_AtlasWidth").intValue = AtlasSize;
            so.FindProperty("m_AtlasHeight").intValue = AtlasSize;
            so.FindProperty("m_AtlasPadding").intValue = Padding;
            so.FindProperty("m_IsMultiAtlasTexturesEnabled").boolValue = true;
            so.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (font.sourceFontFile == null) throw new System.InvalidOperationException($"[FontBaker] {font.name}: 원본 글꼴(ttf)을 찾지 못했다");

            FontEngine.InitializeFontEngine();
            FontEngine.LoadFontFace(font.sourceFontFile, SamplingPointSize);
            font.faceInfo = FontEngine.GetFaceInfo();

            font.ClearFontAssetData();
            font.TryAddCharacters(chars, out string missing);

            // SDF 머티리얼은 아틀라스 크기·여백을 알아야 한다
            var mat = font.material;
            mat.SetFloat(ShaderUtilities.ID_TextureWidth, AtlasSize);
            mat.SetFloat(ShaderUtilities.ID_TextureHeight, AtlasSize);
            mat.SetFloat(ShaderUtilities.ID_GradientScale, Padding + 1);
            mat.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);

            font.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(mat);
            EditorUtility.SetDirty(font);
            foreach (var tex in font.atlasTextures) if (tex != null) EditorUtility.SetDirty(tex);

            // 예전 굽기에서 남은 아틀라스 텍스처(지금 페이지가 아닌 것)를 파일에서 뺀다 (한 장 1MB)
            var used = new HashSet<Texture2D>(font.atlasTextures.Where(t => t != null));
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(font)))
                if (sub is Texture2D tex && !used.Contains(tex)) AssetDatabase.RemoveObjectFromAsset(tex);
            return missing ?? "";
        }

        // 폰트에 없는 글자 (표 검사용). 두 폰트 중 하나라도 없으면 넣는다
        public static string MissingCharacters(string text)
        {
            var missing = new HashSet<char>();
            foreach (var path in FontPaths)
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Static) continue;
                foreach (var c in text)
                    if (c >= ' ' && !char.IsSurrogate(c) && !font.characterLookupTable.ContainsKey(c)) missing.Add(c);
            }
            return new string(missing.OrderBy(c => c).ToArray());
        }
    }
}
