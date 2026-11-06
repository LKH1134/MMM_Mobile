using TMPro;
using UnityEditor;

namespace MoreMush.Mobile.EditorTools
{
    // [UI 개편 2026-11-05] 동적 폰트(LINESeedKR-* SDF)는 실행할 때마다 아틀라스에 글자가 추가되고, 커밋 전에 git으로 되돌린다 (UI_REWORK_LOG.md 4-1).
    // 되돌린 뒤 다시 가져오면 글자 자리(아틀라스 좌표)가 바뀌는데, 에디터에 이미 그려 둔 글자는 예전 좌표를 써서
    // 한 단어가 조각나 2~3개씩 겹쳐 보인다. 폰트 에셋을 다시 가져오면 그 폰트를 쓰는 글자를 모두 다시 그리게 한다 (에디터 전용, 게임에는 영향 없음)
    class FontReimportRefresh : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (!path.EndsWith(".asset") || !path.Contains("SDF")) continue;
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null) continue;
                EditorApplication.delayCall += () =>
                {
                    if (font == null) return;
                    font.ReadFontAssetDefinition();
                    TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, font);
                };
            }
        }
    }
}
