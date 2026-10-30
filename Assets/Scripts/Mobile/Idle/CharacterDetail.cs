using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 꼬마 고르기 팝업 아래쪽 정보 칸 (장비 화면 위쪽 정보 칸과 같은 구성):
    // 그림 · [등급] 이름 · Lv · 다음 레벨 조각 막대 · 공격 배율(→ 다음 레벨) · 편성 / 합성 / 일괄 합성 버튼.
    // FormationPanel이 팝업 그리드 아래 빈 자리에 코드로 만들고, 꼬마를 누를 때마다 Show로 바꾼다
    public class CharacterDetail : MonoBehaviour
    {
        const string CreamSprite = "Art/Dream/UI/Window/btn_cream";
        const string GoSprite = "Art/Dream/UI/btn_go";
        static readonly Color Brown = new Color(0.36f, 0.24f, 0.14f);
        static readonly Color Green = new Color(0.45f, 0.78f, 0.38f);
        static readonly Color Locked = new Color(0.25f, 0.25f, 0.25f);

        TMP_FontAsset font;
        Image icon;
        TMP_Text nameText, levelText, barText, statText, placeLabel, message;
        RectTransform barFill;
        Button placeButton, mergeButton, mergeAllButton;
        string id;
        bool inTargetSlot;

        public event Action<string> PlaceRequested;     // "편성" — 지금 고르는 칸에 놓기

        public static CharacterDetail Create(RectTransform parent, TMP_FontAsset font, float height)
        {
            var root = NewImage("CharacterDetail", parent);
            Skin(root, CreamSprite, 2.5f);
            var rt = root.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(30f, 20f);
            rt.offsetMax = new Vector2(-30f, 20f + height);
            var d = root.gameObject.AddComponent<CharacterDetail>();
            d.font = font;
            d.Build();
            return d;
        }

        void OnEnable() => Formation.Changed += Refresh;
        void OnDisable() => Formation.Changed -= Refresh;

        void Build()
        {
            var rt = (RectTransform)transform;

            icon = NewImage("Icon", rt);
            Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(230f, 230f), new Vector2(0f, 1f));

            nameText = NewText("Name", rt, 44f);
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            Place(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(300f, -40f), new Vector2(560f, 60f), new Vector2(0f, 1f));
            levelText = NewText("Level", rt, 40f);
            levelText.alignment = TextAlignmentOptions.MidlineRight;
            Place(levelText.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(280f, 60f), new Vector2(1f, 1f));

            // 다음 레벨 조각 막대
            var bar = NewImage("Bar", rt);
            bar.color = new Color(0.85f, 0.82f, 0.78f);
            bar.preserveAspect = false;
            Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(300f, -116f), new Vector2(880f, 44f), new Vector2(0f, 1f));
            var fill = NewImage("Fill", bar.rectTransform);
            fill.color = Green;
            fill.preserveAspect = false;
            barFill = fill.rectTransform;
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = new Vector2(0f, 1f);
            barFill.offsetMin = barFill.offsetMax = Vector2.zero;
            barText = NewText("Text", bar.rectTransform, 28f);
            Stretch(barText.rectTransform);

            statText = NewText("Stat", rt, 34f);
            statText.alignment = TextAlignmentOptions.MidlineLeft;
            Place(statText.rectTransform, new Vector2(0f, 1f), new Vector2(300f, -168f), new Vector2(880f, 56f), new Vector2(0f, 1f));

            placeButton = NewButton("Place", rt, new Vector2(-330f, 30f), "편성", out placeLabel);
            mergeButton = NewButton("Merge", rt, new Vector2(0f, 30f), "합성", out _);
            mergeAllButton = NewButton("MergeAll", rt, new Vector2(330f, 30f), "일괄 합성", out _);
            placeButton.onClick.AddListener(() => { if (!string.IsNullOrEmpty(id)) PlaceRequested?.Invoke(id); });
            mergeButton.onClick.AddListener(MergeSelected);
            mergeAllButton.onClick.AddListener(MergeAll);

            // 합성 결과 한 줄 (버튼 위)
            message = NewText("Message", rt, 28f);
            message.color = new Color(0.23f, 0.56f, 0.18f);
            Place(message.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1000f, 40f), new Vector2(0.5f, 0f));
        }

        // id: 보여 줄 꼬마, inTargetSlot: 지금 고르는 칸에 이미 놓인 꼬마인가 (편성 버튼 글자)
        public void Show(string id, bool inTargetSlot)
        {
            this.id = id;
            this.inTargetSlot = inTargetSlot;
            message.text = "";
            Refresh();
        }

        void MergeSelected()
        {
            int up = Formation.Merge(id);
            message.text = up > 0 ? $"{Formation.Name(id)} Lv.{Formation.Level(id)} (+{up})" : "";
        }

        void MergeAll()
        {
            int up = Formation.MergeAll();
            message.text = up > 0 ? $"일괄 합성: 꼬마 레벨 +{up}" : "합성할 꼬마가 없습니다";
        }

        void Refresh()
        {
            if (string.IsNullOrEmpty(id)) return;
            int level = Formation.Level(id), copies = Formation.Copies(id), need = Formation.CopiesToNext(level);
            bool have = level > 0, max = level >= Formation.MaxLevel;

            icon.sprite = Formation.Icon(id);
            icon.color = have ? Color.white : Locked;
            nameText.text = $"[{GachaProgress.GradeNames[Mathf.Clamp(Formation.Grade(id), 0, GachaProgress.GradeCount - 1)]}] {Formation.Name(id)}";
            levelText.text = have ? (max ? $"Lv.{level} MAX" : $"Lv.{level}") : "미보유";
            barFill.anchorMax = new Vector2(max ? 1f : have ? Mathf.Clamp01(copies / (float)need) : 0f, 1f);
            barText.text = !have ? "꼬마 뽑기로 얻을 수 있습니다" : max ? "최고 레벨" : $"다음 레벨 조각 {copies}/{need}";

            int shown = Mathf.Max(1, level);
            string next = have && !max ? $"  <size=80%><color=#3A8F2E>→ ×{Formation.AttackMultiplierAt(id, level + 1):0.00}</color></size>" : "";
            statText.text = $"공격 배율  ×{Formation.AttackMultiplierAt(id, shown):0.00}" + next;

            placeButton.interactable = have;
            placeLabel.text = inTargetSlot ? "편성 해제" : "편성";
            mergeButton.interactable = Formation.CanMerge(id);
            bool anyMerge = false;
            foreach (var other in Formation.Ids) if (Formation.CanMerge(other)) { anyMerge = true; break; }
            mergeAllButton.interactable = anyMerge;
        }

        // ── 만들기 도우미 ──

        Button NewButton(string name, RectTransform parent, Vector2 pos, string label, out TMP_Text text)
        {
            var img = NewImage(name, parent);
            img.preserveAspect = false;
            img.raycastTarget = true;
            Skin(img, GoSprite, 1.4f);
            Place(img.rectTransform, new Vector2(0.5f, 0f), pos, new Vector2(300f, 110f), new Vector2(0.5f, 0f));
            var b = img.gameObject.AddComponent<Button>();
            text = NewText("Text", img.rectTransform, 36f);
            text.text = label;
            text.color = Color.white;
            Stretch(text.rectTransform);
            return b;
        }

        static Image NewImage(string name, Transform parent)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        TMP_Text NewText(string name, Transform parent, float size)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = Brown;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        static void Skin(Image img, string sprite, float pixelsPerUnit)
        {
            var sp = Resources.Load<Sprite>(sprite);
            if (sp == null) return;
            img.sprite = sp;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = pixelsPerUnit;
            img.color = Color.white;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
