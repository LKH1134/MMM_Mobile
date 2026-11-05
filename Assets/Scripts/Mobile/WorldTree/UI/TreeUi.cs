using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 세계수 시트 내용(목록·카드·버튼)을 코드로 만드는 조각들. 팝업 껍데기(시트·탭·닫기)는 프리팹 그대로 쓰고 Content 안만 채운다.
    // 크기는 디자인 해상도(가로 1080) 기준 픽셀
    public static class TreeUi
    {
        public static readonly Color Ink = new Color(0.30f, 0.20f, 0.10f, 1f);
        public static readonly Color Faint = new Color(0.30f, 0.20f, 0.10f, 0.5f);
        public static readonly Color Good = new Color(0.22f, 0.55f, 0.25f, 1f);
        public static readonly Color Bad = new Color(0.80f, 0.28f, 0.22f, 1f);
        public static readonly Color ButtonOn = new Color(0.40f, 0.80f, 0.55f, 1f);
        public static readonly Color ButtonAlt = new Color(0.95f, 0.65f, 0.30f, 1f);
        public static readonly Color ButtonOff = new Color(0.72f, 0.70f, 0.68f, 1f);
        public static readonly Color CardOn = new Color(1f, 0.97f, 0.88f, 1f);
        public static readonly Color CardOff = new Color(0.86f, 0.84f, 0.80f, 1f);

        // 색은 그림으로 정한다 (코드에서 물들이지 않는다). 위의 Button·Card 색 값은 어떤 그림을 쓸지 고르는 데만 쓴다
        const string CardSprite = "UI/UI_PP_Out_Paper2";            // 켜진 카드
        const string CardOffSprite = "UI/UI_PP_In_Color_Grey";      // 꺼진 카드 (회색)
        const string ButtonSprite = "UI/UI_Btn_Green";              // ButtonOn
        const string ButtonAltSprite = "UI/UI_Btn_Yellow";          // ButtonAlt
        const string ButtonOffSprite = "UI/UI_Btn_Grey";            // ButtonOff · 누를 수 없음 (회색)
        const string BarBackSprite = "UI/UI_PP_Bar_Gauge_Under";
        const string BarGreenSprite = "UI/UI_PP_Bar_Gauge_Green";
        const string BarYellowSprite = "UI/UI_PP_Bar_Gauge_Yellow";
        static Sprite white;

        static Sprite Load(string path) => Resources.Load<Sprite>(path);

        // 부모를 가득 채우는 세로 스크롤 목록. 돌려주는 것은 행을 넣을 content
        public static RectTransform ScrollList(Transform parent)
        {
            var view = New("Scroll", parent);
            Stretch(view);
            view.gameObject.AddComponent<RectMask2D>();
            var hit = view.gameObject.AddComponent<Image>();
            hit.color = Color.clear;                     // 빈 곳을 끌어도 스크롤되게
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var content = New("List", view);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = UiSkin.Gap;   // [UI 개편] 공통 간격
            layout.padding = new RectOffset(0, 0, 8, 24);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = view;
            scroll.content = content;
            return content;
        }

        // 목록 한 줄 (종이 카드). 안의 조각은 Place로 자리를 잡는다
        public static RectTransform Card(Transform list, float height, bool active = true)
        {
            var rt = New("Card", list);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Load(active ? CardSprite : CardOffSprite);
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = false;
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return rt;
        }

        // 카드 없는 제목 줄
        public static TMP_Text Header(Transform list, string text, TMP_FontAsset font)
        {
            var rt = New("Header", list);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;
            var t = Text(rt, text, 44f, Ink, font, TextAlignmentOptions.BottomLeft);
            t.fontStyle = FontStyles.Bold;
            Place(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-12f, 0f));
            return t;
        }

        public static TMP_Text Text(Transform parent, string text, float size, Color color, TMP_FontAsset font,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var t = New("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            // [UI 개편] 세계수 카드 글자: 1.15배·굵게, 넘치면 자동 축소 (UiSkin 글자 규격)
            t.fontStyle = FontStyles.Bold;
            t.enableAutoSizing = true;
            t.fontSizeMax = size * 1.15f;
            t.fontSizeMin = size * 0.7f;
            t.fontSize = size * 1.15f;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        // 카드 오른쪽 끝에서 right만큼 떨어진 버튼
        public static Button Button(Transform parent, string label, bool interactable, Action onClick, TMP_FontAsset font,
            float right = 24f, float width = 220f, float height = 110f, Color? tint = null)
        {
            var rt = New("Button", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(-right, 0f);
            var img = rt.gameObject.AddComponent<Image>();
            var want = interactable ? (tint ?? ButtonOn) : ButtonOff;
            img.sprite = Load(want == ButtonAlt ? ButtonAltSprite : want == ButtonOff ? ButtonOffSprite : ButtonSprite);
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            var b = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            UiSkin.GreyWhenDisabled(b);   // [UI 개편] 누를 수 없으면 회색 버튼 그림
            b.targetGraphic = img;
            b.interactable = interactable;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var t = Text(rt, label, 34f, Color.white, font, TextAlignmentOptions.Center);
            t.fontStyle = FontStyles.Bold;
            t.overflowMode = TextOverflowModes.Overflow;
            Place(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            return b;
        }

        // 카드 왼쪽 아이콘
        public static Image Icon(Transform parent, Sprite sprite, float size = 140f, float left = 24f)
        {
            var rt = New("Icon", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(left, 0f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (sprite == null) img.color = Color.clear;
            return img;
        }

        // 진행 막대 (0~1). 부모 기준 offset 자리
        public static void Bar(Transform parent, float value, Color fill, Vector2 offsetMin, Vector2 offsetMax, float yAnchor)
        {
            var back = New("Bar", parent);
            Place(back, new Vector2(0f, yAnchor), new Vector2(1f, yAnchor), offsetMin, offsetMax);
            var bg = back.gameObject.AddComponent<Image>();
            bg.sprite = Load(BarBackSprite);
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;
            var front = New("Fill", back);
            Place(front, Vector2.zero, new Vector2(Mathf.Clamp01(value), 1f), Vector2.zero, Vector2.zero);
            var fg = front.gameObject.AddComponent<Image>();
            fg.sprite = Load(fill.g >= fill.r ? BarGreenSprite : BarYellowSprite);   // 초록 계열이면 초록 막대, 아니면 노란 막대
            fg.type = Image.Type.Sliced;
            fg.raycastTarget = false;
        }

        static Sprite White
        {
            get
            {
                if (white == null) white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
                return white;
            }
        }

        public static RectTransform New(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt) => Place(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        public static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        // 카드 안 글자 영역: 왼쪽 left ~ 오른쪽 right, 세로 위치 yMin~yMax (0 아래 ~ 1 위)
        public static TMP_Text Line(Transform card, string text, float size, Color color, TMP_FontAsset font, float left, float right, float yMin, float yMax)
        {
            var t = Text(card, text, size, color, font);
            Place(t.rectTransform, new Vector2(0f, yMin), new Vector2(1f, yMax), new Vector2(left, 0f), new Vector2(-right, 0f));
            return t;
        }

        public static string Duration(double seconds)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling(seconds)));
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";
        }

        public static string Colored(string text, Color c) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{text}</color>";
        public static string Check(bool ok, string text) => Colored(text, ok ? Good : Bad);
        public static string Stars(int star, int max) => new string('★', star) + new string('☆', Math.Max(0, max - star));
    }
}
