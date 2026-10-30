using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 뽑기 카드 오른쪽 열 꾸미기 (참고: 레전드 오브 슬라임 장비 소환 칸).
    //   맨 위 = 뽑기 레벨 "LV n" + 경험치 막대 "exp/다음",
    //   그 아래 = 광고 소환(초록, 넓게) · 재화 소환 11회/35회(보라, 반씩) — 버튼마다 "n회 소환" 한 줄 + 아래 줄(무료/재화 아이콘·가격).
    // GachaPanel이 Awake에서 부른다. 프리팹(Gacha_Panel1~4)의 버튼을 그대로 쓰고 자리·색·글자만 코드로 바꾼다
    public static class GachaCardView
    {
        public class ExpRow { public TMP_Text level, value; public RectTransform fill; }
        public class ButtonText { public TMP_Text title, sub; }

        const string BarBack = "Art/Dream/UI/progress_back";
        const string BarFill = "Art/Dream/UI/progress_fill";
        public static readonly Color AdColor = new Color(0.33f, 0.66f, 0.32f);
        public static readonly Color CoinColor = new Color(0.47f, 0.32f, 0.68f);

        // 오른쪽 열 (카드 오른쪽 끝 기준, 버튼 앵커는 프리팹 그대로 오른쪽 가운데)
        const float ColumnX = -276f, ColumnWidth = 474f;
        const float ExpY = 102f, ExpHeight = 46f;
        const float AdY = 22f, AdHeight = 92f;
        const float CoinY = -110f, CoinHeight = 150f, CoinWidth = 232f, CoinGap = 10f;

        public static void Layout(Button ad, Button coin11, Button coin35)
        {
            Place(ad, new Vector2(ColumnX, AdY), new Vector2(ColumnWidth, AdHeight));
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

            var back = NewImage("Bar", root, BarBack, new Color(0.2f, 0.16f, 0.22f, 0.85f));
            var brt = back.rectTransform;
            brt.anchorMin = new Vector2(0f, 0.5f);
            brt.anchorMax = new Vector2(1f, 0.5f);
            brt.offsetMin = new Vector2(118f, -15f);
            brt.offsetMax = new Vector2(-4f, 15f);
            var fill = NewImage("Fill", brt, BarFill, new Color(0.45f, 0.7f, 1f));
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
            row.value.text = max ? "MAX" : $"{exp}/{need}";
        }

        // 버튼 색과 두 줄 글자. costIcon이 있으면 아래 줄 앞에 재화 아이콘을 붙인다.
        // 프리팹 버튼의 원래 글자(label)는 위 줄로 쓴다
        public static ButtonText Style(Button b, TMP_Text label, Color color, Sprite costIcon, TMP_FontAsset font)
        {
            var text = new ButtonText();
            if (b == null) return text;
            if (b.image != null) b.image.color = color;
            var rt = (RectTransform)b.transform;

            text.title = label != null ? label : NewText("Title", rt, font, 32f, Color.white);
            if (font != null) text.title.font = font;
            text.title.color = Color.white;
            text.title.fontSize = 32f;
            text.title.fontStyle = FontStyles.Bold;
            text.title.enableAutoSizing = false;
            text.title.alignment = TextAlignmentOptions.Center;
            var trt = text.title.rectTransform;
            trt.anchorMin = new Vector2(0f, 0.5f);
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(6f, 0f);
            trt.offsetMax = new Vector2(-6f, -4f);

            // 아래 줄: [아이콘] 가격 — 가운데 정렬로 묶는다
            var sub = new GameObject("Sub", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            sub.SetParent(rt, false);
            sub.anchorMin = sub.anchorMax = new Vector2(0.5f, 0.25f);
            sub.sizeDelta = new Vector2(0f, 44f);
            var layout = sub.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
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
                le.preferredWidth = le.preferredHeight = 40f;
            }
            text.sub = NewText("Value", sub, font, 30f, new Color(1f, 0.95f, 0.75f));
            return text;
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
