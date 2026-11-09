using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// LINESeedKR-Bd SDF Wide 폰트 에셋을 Dynamic → Static 으로 바꾼다.
/// StaticFontChars.txt(프로젝트에서 쓰는 한글) + ASCII + 기본 기호를 아틀라스에 구운 뒤 Static 모드로 전환한다.
/// 사용: 메뉴 Tools/Font/Bake Static (LINESeedKR-Bd SDF Wide)
/// </summary>
public static class BakeStaticFont
{
    private const string FontAssetPath = "Assets/Resources/UI/Fonts/LINESeedKR-Bd SDF Wide.asset";
    private const string CharsFilePath = "Assets/Editor/StaticFontChars.txt";

    // 한글 외에 항상 포함할 글자: ASCII 32~126 + 자주 쓰는 기호
    private const string ExtraSymbols =
        "·…―–—‘’“”•※★☆♥♡▲▼△▽◆◇■□○●◎→←↑↓↔×÷±≤≥≈∞°℃％‰①②③④⑤⑥⑦⑧⑨⑩";

    [MenuItem("Tools/Font/Bake Static (LINESeedKR-Bd SDF Wide)")]
    public static void Bake()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (font == null)
        {
            Debug.LogError($"[BakeStaticFont] 폰트 에셋을 찾을 수 없음: {FontAssetPath}");
            return;
        }

        if (!File.Exists(CharsFilePath))
        {
            Debug.LogError($"[BakeStaticFont] 글자 목록 파일이 없음: {CharsFilePath}");
            return;
        }

        var sb = new StringBuilder();
        for (char c = (char)32; c <= (char)126; c++) sb.Append(c);
        sb.Append(ExtraSymbols);
        sb.Append(File.ReadAllText(CharsFilePath, Encoding.UTF8).Trim());
        string chars = sb.ToString();

        // 굽는 동안은 Dynamic 이어야 TryAddCharacters 가 동작한다.
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        bool ok = font.TryAddCharacters(chars, out string missing, true);

        font.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(font);
        foreach (var tex in font.atlasTextures)
        {
            if (tex != null) EditorUtility.SetDirty(tex);
        }
        if (font.material != null) EditorUtility.SetDirty(font.material);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[BakeStaticFont] 완료. 글리프 {font.glyphTable.Count}개 / 글자 {font.characterTable.Count}개 / " +
                  $"아틀라스 {font.atlasTextures.Length}장 / 모드 {font.atlasPopulationMode}" +
                  (ok ? "" : $"\n폰트에 없는 글자(□로 나옴): {missing}"));
    }
}
