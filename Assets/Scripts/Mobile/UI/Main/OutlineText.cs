using TMPro;
using UnityEngine;

namespace MoreMush.Mobile
{
    // [UI 개편 2026-11-05] 흰 글자 + 갈색 바깥 외곽선 (UI_REWORK_LOG.md 0-1 글자 규격).
    // 외곽선은 글자 안으로 파고들지 않고 바깥으로만 붙는다 (재질의 Face Dilate = Outline Width).
    // 글자 크기로 굵기를 고른다: 작은 글자(32 미만) 2px · 보통(32~47) 4px · 큰 글자(48 이상) 6px — 폭 1080 기준.
    // 기본 폰트(LINESeedKR-Bd SDF)는 여백(padding 9)이 좁아 4px 이상을 못 그려서, 여백을 넓힌 Wide 폰트(padding 20)를 쓴다.
    // 프리팹 글자는 이 재질을 직접 끼워 두었고, 코드로 만드는 글자는 Apply를 부른다
    public static class OutlineText
    {
        const string FontPath = "UI/Fonts/LINESeedKR-Bd SDF Wide";
        const string MatPath = "UI/Fonts/LINESeedKR-Bd Wide Outline ";   // + S·M·L
        public const float MediumFrom = 32f, LargeFrom = 48f;

        static TMP_FontAsset font;
        static Material small, medium, large;

        public static void Apply(TMP_Text t)
        {
            if (t == null) return;
            if (font == null) font = Resources.Load<TMP_FontAsset>(FontPath);
            if (font == null) return;     // 리소스가 없으면 원래 글자 그대로
            var m = MaterialFor(t.enableAutoSizing ? t.fontSizeMax : t.fontSize);
            if (m == null) return;
            t.font = font;
            t.fontSharedMaterial = m;
        }

        // 흰 글자는 무조건 외곽선 (11-05 규칙). 흰색이 아니면 그대로 둔다. 글자 크기·색을 정한 뒤에 부른다
        public static void ApplyIfWhite(TMP_Text t)
        {
            if (t == null) return;
            var c = t.color;
            if (c.r > 0.85f && c.g > 0.85f && c.b > 0.85f && c.a > 0.3f) Apply(t);
        }

        static Material MaterialFor(float size)
        {
            if (size >= LargeFrom) return large != null ? large : large = Resources.Load<Material>(MatPath + "L");
            if (size >= MediumFrom) return medium != null ? medium : medium = Resources.Load<Material>(MatPath + "M");
            return small != null ? small : small = Resources.Load<Material>(MatPath + "S");
        }
    }
}
