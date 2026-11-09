using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 뽑기 카드 오른쪽 열 꾸미기 (참고: 레전드 오브 슬라임 장비 소환 칸).
    //   맨 위 = 뽑기 레벨 "LV n" + 경험치 막대 "exp/다음",
    //   그 아래 = 무료 소환(초록, 넓게, 하루 한 번) · 재화 소환 11회/35회(보라, 반씩) — 버튼마다 "n회 소환" 한 줄 + 아래 줄(무료/재화 아이콘·가격).
    // GachaPanel이 Awake에서 부른다. 프리팹(Gacha_Panel1~4)의 버튼을 그대로 쓰고 자리·색·글자만 코드로 바꾼다
    public static class GachaCardView
    {
        public class ExpRow { public TMP_Text level, value; public RectTransform fill; }
        public class ButtonText { public TMP_Text title, sub; }

        const string BarBack = "UI/UI_PP_Bar_Gauge_Under";
        const string BarFill = "UI/UI_PP_Bar_Gauge_Yellow";
        // 버튼 그림: 무료 = 초록 버튼, 재화 = 하늘 버튼 (색은 그림으로 정하고 물들이지 않는다)
        public const string FreeSprite = "UI/UI_Btn_Green";
        public const string CoinSprite = "UI/UI_Btn_Sky";
        public static readonly Color FreeColor = Color.white;
        public static readonly Color CoinColor = Color.white;
        static readonly Color32 TextOutline = new Color32(40, 28, 20, 255);

        // 오른쪽 열 (카드 오른쪽 끝 기준, 버튼 앵커는 프리팹 그대로 오른쪽 가운데)
        const float ColumnX = -276f, ColumnWidth = 474f;
        // [UI 개편 2026-11-05] 경험치 줄을 제목 띠(Name, 카드 가운데 위 162)와 같은 줄로 올리고, 광고 버튼도 그만큼(60) 위로,
        //   남은 자리만큼 균사석 소환 버튼을 위로 늘림 (예전: ExpY 102 · FreeY 22 · CoinY -110 · CoinHeight 150)
        const float ExpY = 162f, ExpHeight = 46f;
        const float FreeY = 82f, FreeHeight = 92f;
        const float CoinY = -80f, CoinHeight = 210f, CoinWidth = 232f, CoinGap = 10f;

        public static void Layout(Button free, Button coin11, Button coin35)
        {
            Place(free, new Vector2(ColumnX, FreeY), new Vector2(ColumnWidth, FreeHeight));
            float half = (CoinWidth + CoinGap) * 0.5f;
            Place(coin11, new Vector2(ColumnX - half, CoinY), new Vector2(CoinWidth, CoinHeight));
            Place(coin35, new Vector2(ColumnX + half, CoinY), new Vector2(CoinWidth, CoinHeight));
        }

        static void Place(Button b, Vector2 pos, Vector2 size)
        {
            if (b == null) return;
            var rt = (RectTransform)b.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static ExpRow BuildExp(RectTransform card, TMP_FontAsset font)
        {
            var root = new GameObject("Exp", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(card, false);
            root.anchorMin = root.anchorMax = new Vector2(1f, 0.5f);
            root.anchoredPosition = new Vector2(ColumnX, ExpY);
            root.sizeDelta = new Vector2(ColumnWidth, ExpHeight);

            var row = new ExpRow();
            row.level = NewText("Level", root, font, 32f, Color.white);
            row.level.fontStyle = FontStyles.Bold;
            row.level.alignment = TextAlignmentOptions.MidlineLeft;
            var lrt = row.level.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.anchoredPosition = new Vector2(4f, 0f);
            lrt.sizeDelta = new Vector2(110f, 0f);

            var back = NewImage("Bar", root, BarBack, Color.white);
            var brt = back.rectTransform;
            brt.anchorMin = new Vector2(0f, 0.5f);
            brt.anchorMax = new Vector2(1f, 0.5f);
            brt.offsetMin = new Vector2(118f, -15f);
            brt.offsetMax = new Vector2(-4f, 15f);
            var fill = NewImage("Fill", brt, BarFill, Color.white);
            row.fill = fill.rectTransform;
            row.fill.anchorMin = Vector2.zero;
            row.fill.anchorMax = new Vector2(0f, 1f);
            row.fill.offsetMin = new Vector2(3f, 3f);
            row.fill.offsetMax = new Vector2(-3f, -3f);
            row.value = NewText("Value", brt, font, 24f, Color.white);
            Stretch(row.value.rectTransform);
            row.value.outlineWidth = 0.2f;
            row.value.outlineColor = new Color32(30, 20, 40, 255);
            return row;
        }

        public static void SetExp(ExpRow row, int level, int exp, int need)
        {
            row.level.text = $"LV {level}";
            bool max = need <= 0;
            float k = max ? 1f : Mathf.Clamp01(exp / (float)need);
            row.fill.anchorMax = new Vector2(k, 1f);
            row.fill.gameObject.SetActive(k > 0f);
            row.value.text = max ? Loc.T("upgrade.max") : $"{exp}/{need}";
        }

        // 버튼 색과 두 줄 글자. costIcon이 있으면 아래 줄 앞에 재화 아이콘을 붙인다.
        // 프리팹 버튼의 원래 글자(label)는 위 줄로 쓴다
        public static ButtonText Style(Button b, TMP_Text label, string sprite, Color color, Sprite costIcon, TMP_FontAsset font)
        {
            var text = new ButtonText();
            if (b == null) return text;
            if (b.image != null)
            {
                var sp = Resources.Load<Sprite>(sprite);
                if (sp != null)
                {
                    b.image.sprite = sp;
                    b.image.type = Image.Type.Sliced;
                    b.image.pixelsPerUnitMultiplier = 1.6f;     // 버튼이 작아 테두리를 줄인다
                }
                b.image.color = color;
            }
            UiSkin.GreyWhenDisabled(b);   // [UI 개편] 누를 수 없으면 회색 버튼 그림
            var rt = (RectTransform)b.transform;

            text.title = label != null ? label : NewText("Title", rt, font, 32f, Color.white);
            if (font != null) text.title.font = font;
            text.title.color = Color.white;
            // [UI 개편] 버튼 글자·아이콘·가격을 크게 (넘치면 자동 축소)
            text.title.fontSize = 40f;
            text.title.fontStyle = FontStyles.Bold;
            text.title.enableAutoSizing = true;
            text.title.fontSizeMax = 40f;
            text.title.fontSizeMin = 24f;
            text.title.alignment = TextAlignmentOptions.Center;
            Outline(text.title);
            var trt = text.title.rectTransform;
            trt.anchorMin = new Vector2(0f, 0.48f);
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10f, 0f);
            trt.offsetMax = new Vector2(-10f, -6f);

            // 아래 줄: [아이콘] 가격 — 가운데 정렬로 묶는다
            var sub = new GameObject("Sub", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            sub.SetParent(rt, false);
            sub.anchorMin = sub.anchorMax = new Vector2(0.5f, 0.26f);
            sub.sizeDelta = new Vector2(0f, 60f);
            var layout = sub.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            sub.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (costIcon != null)
            {
                var icon = NewImage("Icon", sub, null, Color.white);
                icon.sprite = costIcon;
                icon.preserveAspect = true;
                var le = icon.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.preferredHeight = CostIconSize;   // [UI 개편 2026-11-05] 56 → 80 (균사석 소환 버튼이 높아져서)
            }
            text.sub = NewText("Value", sub, font, 38f, new Color(1f, 0.95f, 0.75f));
            text.sub.fontStyle = FontStyles.Bold;
            Outline(text.sub);
            // [UI 개편 2026-11-05] 재화 아이콘이 없는 버튼(광고·무료)은 버튼 좌우 끝, 세로 가운데에 잎
            if (costIcon == null) { Leaf(rt, "UI/UI_Sticker_Leaf_L_Beige", 0f); Leaf(rt, "UI/UI_Sticker_Leaf_R_Beige", 1f); }
            return text;
        }

        const float LeafSize = 56f, LeafInset = 26f, LeafTextGap = 4f;
        const float CostIconSize = 80f;

        // [UI 개편 2026-11-05] 광고 소환 버튼: 아래 줄 없이 "광고 보고 11회 소환" 한 줄을 버튼 가운데에 (잎 사이)
        public static void OneLine(ButtonText text)
        {
            if (text == null || text.title == null) return;
            if (text.sub != null) text.sub.transform.parent.gameObject.SetActive(false);
            text.title.textWrappingMode = TextWrappingModes.NoWrap;   // 넘치면 줄바꿈 대신 자동 축소 (잘리지 않게 최소 16까지)
            text.title.overflowMode = TextOverflowModes.Overflow;
            text.title.fontSizeMin = 16f;
            var trt = text.title.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            // 잎 바깥부터 글자 자리 (예전 -6은 잎과 6px 겹쳐 긴 문구·외곽선이 잎을 덮었다). 넘치면 자동 축소
            trt.offsetMin = new Vector2(LeafInset + LeafSize + LeafTextGap, 0f);
            trt.offsetMax = new Vector2(-(LeafInset + LeafSize + LeafTextGap), -4f);
        }

        static void Leaf(RectTransform button, string sprite, float side)
        {
            var leaf = NewImage(side < 0.5f ? "Leaf_L" : "Leaf_R", button, null, Color.white);
            leaf.sprite = Resources.Load<Sprite>(sprite);
            leaf.preserveAspect = true;
            var r = leaf.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(side, 0.5f);
            r.anchoredPosition = new Vector2(side < 0.5f ? LeafInset : -LeafInset, 0f);
            r.sizeDelta = new Vector2(LeafSize, LeafSize);
        }

        // 밝은 버튼 그림 위에서도 흰 글자가 읽히게 진한 갈색 외곽선
        static void Outline(TMP_Text t)
        {
            // [UI 개편 2026-11-05] 공용 바깥 외곽선 재질(크기별 2·4·6px)로. 재질 리소스가 없으면 예전처럼 글자마다 외곽선
            OutlineText.Apply(t);
            if (t.fontSharedMaterial != null && t.fontSharedMaterial.name.StartsWith("LINESeedKR-Bd Wide Outline")) return;
            t.outlineWidth = 0.2f;
            t.outlineColor = TextOutline;
        }

        static Image NewImage(string name, Transform parent, string sprite, Color color)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.raycastTarget = false;
            img.color = color;
            var sp = sprite != null ? Resources.Load<Sprite>(sprite) : null;
            if (sp != null)
            {
                img.sprite = sp;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            OutlineText.ApplyIfWhite(t);   // [UI 개편 11-05] 흰 글자는 외곽선 (레벨·막대 숫자)
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
