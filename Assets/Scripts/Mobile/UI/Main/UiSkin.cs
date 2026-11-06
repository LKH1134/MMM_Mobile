using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // UI 공통 규격 (UI 개편 2026-11-04). 숫자는 디자인 해상도(폭 1080) 기준. 자세한 표는 UI_REWORK_LOG.md "공통 규격"
    public static class UiSkin
    {
        public const string Grey = "UI/UI_Btn_Grey";
        public static readonly Color Ink = new Color(0.36f, 0.22f, 0.12f);   // 베이지 위 진한 글자

        public const float TitleSize = 56f, SectionSize = 36f, BodySize = 32f, SmallSize = 26f;
        public const float ButtonMain = 120f, ButtonSub = 96f;              // 버튼 높이
        public const float WindowInset = 22f, Gap = 16f, Margin = 30f;      // 창 틀 안쪽 · 칸 사이 · 화면 좌우

        static Sprite grey;

        // 누를 수 없을 때 색을 입히지 않고 회색 버튼 그림으로 바꿔 낀다 (Unity 기본 ColorTint 대신 SpriteSwap)
        public static void GreyWhenDisabled(Button b)
        {
            if (b == null) return;
            if (grey == null) grey = Resources.Load<Sprite>(Grey);
            b.transition = Selectable.Transition.SpriteSwap;
            var s = b.spriteState;
            s.disabledSprite = grey;
            s.highlightedSprite = s.pressedSprite = s.selectedSprite = null;
            b.spriteState = s;
        }
    }
}
