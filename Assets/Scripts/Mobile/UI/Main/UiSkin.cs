using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // UI 공통 규격 (UI 개편 2026-11-04). 숫자는 디자인 해상도(폭 1080) 기준. 자세한 표는 UI_REWORK_LOG.md "공통 규격"
    public static class UiSkin
    {
        // 공용 UI 그림 (Resources 경로). 코드가 바꿔 끼우는 버튼·카드·막대·재화 아이콘은 여기 하나로 쓴다
        public const string BtnGrey = "UI/UI_Btn_Grey", BtnGreen = "UI/UI_Btn_Green", BtnYellow = "UI/UI_Btn_Yellow",
            BtnRed = "UI/UI_Btn_Red", BtnSky = "UI/UI_Btn_Sky";
        public const string CardBeige = "UI/UI_PP_In_Color_Beige1", CardGreen = "UI/UI_PP_In_Color_Green", CardGrey = "UI/UI_PP_In_Color_Grey",
            CardSelected = "UI/UI_PP_In_InnerLine1", CardPaper = "UI/UI_PP_Out_Paper2";
        public const string BarBack = "UI/UI_PP_Bar_Gauge_Under", BarGreen = "UI/UI_PP_Bar_Gauge_Green", BarYellow = "UI/UI_PP_Bar_Gauge_Yellow";
        public const string IconGold = "UI/Icon/gold", IconMycelite = "UI/Icon/mycelium_stone";
        public static readonly Color Ink = new Color(0.36f, 0.22f, 0.12f);   // 베이지 위 진한 글자

        public const float Gap = 16f;      // 칸 사이 (글자 크기·버튼 높이 등 나머지 규격은 UI_REWORK_LOG.md "공통 규격" — 프리팹에 직접 들어 있다)

        static Sprite grey;

        // 누를 수 없을 때 색을 입히지 않고 회색 버튼 그림으로 바꿔 낀다 (Unity 기본 ColorTint 대신 SpriteSwap)
        public static void GreyWhenDisabled(Button b)
        {
            if (b == null) return;
            if (grey == null) grey = Resources.Load<Sprite>(BtnGrey);
            b.transition = Selectable.Transition.SpriteSwap;
            var s = b.spriteState;
            s.disabledSprite = grey;
            s.highlightedSprite = s.pressedSprite = s.selectedSprite = null;
            b.spriteState = s;
        }
    }
}
