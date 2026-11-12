using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace MoreMush.Mobile
{
    /// <summary>
    /// LINESeedKR-Bd SDF Wide 폰트 에셋을 Static 으로 다시 굽는다 (아틀라스를 비우고 처음부터).
    /// 글자: 지금 구워진 글자 전부 + FontBaker.CollectCharacters(표·코드·씬 글자, Bd·Rg와 같은 목록) + ASCII + 기본 기호 → 한 글자도 빠지지 않는다.
    /// 사용: 메뉴 MoreMush/Fonts/Bake Wide Outline Font (Bd·Rg는 MoreMush/Fonts/Bake Static Atlases)
    /// </summary>
    public static class BakeStaticFont
    {
        private const string FontAssetPath = FontBaker.WideFontPath;

        // [폰트 최적화 2026-11-09] 64pt·여백 20 → 49pt·여백 15. 2048 아틀라스 3장(약 25MB) → 줄임.
        // "Wide"는 두꺼운 외곽선 재질(Outline S/M/L/Red)용 넓은 여백이다. 외곽선 두께는 (여백+1)/샘플 크기 비율로 정해지므로
        // 두 값을 같은 비율로 줄여 외곽선 두께를 그대로 지킨다 (64:21 ≈ 49:16). 여백만 줄이면 외곽선이 얇아진다
        private const int SamplingPointSize = 49;
        private const int AtlasPadding = 15;

        // 한글 외에 항상 포함할 글자: ASCII 32~126 + 자주 쓰는 기호
        private const string ExtraSymbols =
            "·…―–—‘’“”•※★☆♥♡▲▼△▽◆◇■□○●◎→←↑↓↔×÷±≤≥≈∞°℃％‰①②③④⑤⑥⑦⑧⑨⑩";

        [MenuItem("MoreMush/Fonts/Bake Wide Outline Font")]
        public static void Bake()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null)
            {
                Debug.LogError($"[BakeStaticFont] 폰트 에셋을 찾을 수 없음: {FontAssetPath}");
                return;
            }
            var sourceFont = LoadSourceFont(font);
            if (sourceFont == null)
            {
                Debug.LogError("[BakeStaticFont] 원본 폰트 파일(.ttf/.otf)을 찾을 수 없음 — 폰트 에셋의 Source Font File을 확인한다");
                return;
            }

            string chars = CollectCharacters(font);
            var materials = FindMaterialsUsing(font);

            // 비우기 전에 새 크기·여백을 넣는다 (굽는 동안은 Dynamic 이어야 TryAddCharacters 가 동작한다)
            var so = new SerializedObject(font);
            so.FindProperty("m_SourceFontFile").objectReferenceValue = sourceFont;
            so.FindProperty("m_AtlasPadding").intValue = AtlasPadding;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (FontEngine.LoadFontFace(sourceFont, SamplingPointSize) != FontEngineError.Success)
            {
                Debug.LogError("[BakeStaticFont] 원본 폰트를 열 수 없음");
                return;
            }
            font.faceInfo = FontEngine.GetFaceInfo();

            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.ClearFontAssetData(false);
            bool ok = font.TryAddCharacters(chars, out string missing, true);
            font.atlasPopulationMode = AtlasPopulationMode.Static;

            UpdateMaterials(font, materials);
            EditorUtility.SetDirty(font);
            foreach (var tex in font.atlasTextures)
            {
                if (tex != null) EditorUtility.SetDirty(tex);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BakeStaticFont] 완료. {SamplingPointSize}pt·여백 {AtlasPadding} / 글리프 {font.glyphTable.Count}개 / 글자 {font.characterTable.Count}개 / " +
                      $"아틀라스 {font.atlasTextures.Length}장 / 재질 {materials.Count}개 / 모드 {font.atlasPopulationMode}" +
                      (ok ? "" : $"\n폰트에 없는 글자(□로 나옴): {missing}"));
        }

        static Font LoadSourceFont(TMP_FontAsset font)
        {
            if (font.sourceFontFile != null) return font.sourceFontFile;
            var guid = new SerializedObject(font).FindProperty("m_SourceFontFileGUID").stringValue;
            return string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(guid));
        }

        // 지금 구워진 글자 + 게임이 쓰는 글자(FontBaker) + ASCII + 기호 (중복 없이)
        static string CollectCharacters(TMP_FontAsset font)
        {
            var sb = new StringBuilder();
            for (char c = (char)32; c <= (char)126; c++) sb.Append(c);
            sb.Append(ExtraSymbols);
            sb.Append(FontBaker.CollectCharacters());
            foreach (var ch in font.characterTable)
            {
                if (ch.unicode <= 0xFFFF) sb.Append((char)ch.unicode);
                else sb.Append(char.ConvertFromUtf32((int)ch.unicode));
            }
            var seen = new HashSet<int>();
            var result = new StringBuilder();
            string all = sb.ToString();
            for (int i = 0; i < all.Length; i++)
            {
                int cp = char.ConvertToUtf32(all, i);
                if (char.IsHighSurrogate(all[i])) i++;
                if (seen.Add(cp)) result.Append(char.ConvertFromUtf32(cp));
            }
            return result.ToString();
        }

        // 이 폰트의 아틀라스를 쓰는 재질 (폰트 기본 재질 + Outline 프리셋들)
        static List<Material> FindMaterialsUsing(TMP_FontAsset font)
        {
            var list = new List<Material>();
            var atlas = font.atlasTexture;
            foreach (var guid in AssetDatabase.FindAssets("t:Material"))
            {
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    if (obj is Material m && m.HasProperty(ShaderUtilities.ID_MainTex) && m.GetTexture(ShaderUtilities.ID_MainTex) == atlas && !list.Contains(m))
                        list.Add(m);
                }
            }
            if (font.material != null && !list.Contains(font.material)) list.Add(font.material);
            return list;
        }

        // 여백이 바뀌면 재질의 Gradient Scale(여백+1)·아틀라스 크기·비율도 맞춰야 외곽선이 그대로 나온다
        static void UpdateMaterials(TMP_FontAsset font, List<Material> materials)
        {
            foreach (var m in materials)
            {
                m.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
                m.SetFloat(ShaderUtilities.ID_GradientScale, font.atlasPadding + 1);
                m.SetFloat(ShaderUtilities.ID_TextureWidth, font.atlasWidth);
                m.SetFloat(ShaderUtilities.ID_TextureHeight, font.atlasHeight);
                ShaderUtilities.UpdateShaderRatios(m);
                EditorUtility.SetDirty(m);
            }
        }
    }
}
