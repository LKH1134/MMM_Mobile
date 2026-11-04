using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 꼬마 고르기 팝업 아래쪽 정보 칸 (장비 화면 위쪽 정보 칸과 같은 구성):
    // 그림 · [진화 단계] 이름 · Lv · 다음 레벨 조각 막대 · 공격 배율(→ 다음 레벨) · 스킬 3칸(해금 전은 잠금) · 편성 / 합성 / 일괄 합성 버튼.
    // FormationPanel이 팝업 그리드 아래 빈 자리에 코드로 만들고, 꼬마를 누를 때마다 Show로 바꾼다
    public class CharacterDetail : MonoBehaviour
    {
        const string CreamSprite = "UI/UI_Btn_Beige";
        const string GoSprite = "UI/UI_Btn_Green";
        static readonly Color Brown = new Color(0.36f, 0.24f, 0.14f);
        // 색은 그림으로 정한다 (물들이지 않는다)
        const string BarBackSprite = "UI/UI_PP_Bar_Gauge_Under";
        const string BarFillSprite = "UI/UI_PP_Bar_Gauge_Green";
        const string SkillOnSprite = "UI/UI_PP_In_Color_Green";     // 해금된 스킬 칸
        const string SkillOffSprite = "UI/UI_PP_In_Color_Grey";     // 잠긴 스킬 칸 (회색)
        const int SkillSlots = 3;   // 진화 단계마다 하나

        TMP_FontAsset font;
        Image icon;
        TMP_Text nameText, levelText, barText, statText, placeLabel, message;
        RectTransform barFill;
        readonly Image[] skillBg = new Image[SkillSlots];
        readonly TMP_Text[] skillText = new TMP_Text[SkillSlots];
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
            Skin(bar, BarBackSprite, 1f);
            bar.preserveAspect = false;
            Place(bar.rectTransform, new Vector2(0f, 1f), new Vector2(300f, -106f), new Vector2(880f, 38f), new Vector2(0f, 1f));
            var fill = NewImage("Fill", bar.rectTransform);
            Skin(fill, BarFillSprite, 1f);
            fill.preserveAspect = false;
            barFill = fill.rectTransform;
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = new Vector2(0f, 1f);
            barFill.offsetMin = barFill.offsetMax = Vector2.zero;
            barText = NewText("Text", bar.rectTransform, 28f);
            Stretch(barText.rectTransform);

            statText = NewText("Stat", rt, 34f);
            statText.alignment = TextAlignmentOptions.MidlineLeft;
            Place(statText.rectTransform, new Vector2(0f, 1f), new Vector2(300f, -148f), new Vector2(880f, 46f), new Vector2(0f, 1f));

            // 스킬 3칸 (1·2·3단계). 해금 전에도 무슨 스킬인지 보이게 잠금으로 그린다
            const float skillW = 286f, skillGap = 11f;
            for (int k = 0; k < SkillSlots; k++)
            {
                var bg = NewImage("Skill" + (k + 1), rt);
                bg.preserveAspect = false;
                Skin(bg, CreamSprite, 3f);
                Place(bg.rectTransform, new Vector2(0f, 1f), new Vector2(300f + k * (skillW + skillGap), -200f), new Vector2(skillW, 58f), new Vector2(0f, 1f));
                var t = NewText("Text", bg.rectTransform, 21f);
                t.lineSpacing = -18f;
                Stretch(t.rectTransform);
                skillBg[k] = bg;
                skillText[k] = t;
            }

            placeButton = NewButton("Place", rt, new Vector2(-330f, 30f), "편성", out placeLabel);
            mergeButton = NewButton("Merge", rt, new Vector2(0f, 30f), "합성", out _);
            mergeAllButton = NewButton("MergeAll", rt, new Vector2(330f, 30f), "일괄 합성", out _);
            placeButton.onClick.AddListener(() => { if (!string.IsNullOrEmpty(id)) PlaceRequested?.Invoke(id); });
            mergeButton.onClick.AddListener(MergeSelected);
            mergeAllButton.onClick.AddListener(MergeAll);

            // 합성 결과 한 줄 (버튼 위)
            message = NewText("Message", rt, 28f);
            message.color = new Color(0.23f, 0.56f, 0.18f);
            Place(message.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(1000f, 34f), new Vector2(0.5f, 0f));
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
            icon.color = Color.white;     // 못 얻은 꼬마는 "미보유" 글자로 알린다
            int stage = Formation.EvoStage(id);
            nameText.text = $"[{stage}단계] {Formation.Name(id)}";
            levelText.text = have ? (max ? $"Lv.{level} MAX" : $"Lv.{level}") : "미보유";
            barFill.anchorMax = new Vector2(max ? 1f : have ? Mathf.Clamp01(copies / (float)need) : 0f, 1f);
            barText.text = !have ? "꼬마 뽑기로 얻을 수 있습니다" : max ? "최고 레벨" : $"다음 레벨 조각 {copies}/{need}";

            int shown = Mathf.Max(1, level);
            string next = have && !max ? $"  <size=80%><color=#3A8F2E>→ ×{Formation.AttackMultiplierAt(id, level + 1):0.00}</color></size>" : "";
            // 다음 진화 안내 (한 줄 칸이라 같은 줄 뒤에 작게)
            string evo = stage < Formation.EvoStageCount
                ? $"   <size=70%>Lv.{Formation.EvoLevel(stage + 1)} {stage + 1}단계 진화</size>"
                : "   <size=70%>최종 진화</size>";
            statText.text = $"공격 배율  ×{Formation.AttackMultiplierAt(id, shown):0.00}" + next + evo;
            RefreshSkills(have);

            placeButton.interactable = have;
            placeLabel.text = inTargetSlot ? "편성 해제" : "편성";
            mergeButton.interactable = Formation.CanMerge(id);
            bool anyMerge = false;
            foreach (var other in Formation.Ids) if (Formation.CanMerge(other)) { anyMerge = true; break; }
            mergeAllButton.interactable = anyMerge;
        }

        // 스킬 칸: 해금 = 연두 칸에 이름·효과, 잠금 = 회색 칸에 "잠금 Lv.N" + 이름·효과 (무슨 스킬인지는 늘 보인다)
        void RefreshSkills(bool have)
        {
            var skills = GgomaSkills.Of(id);
            for (int k = 0; k < SkillSlots; k++)
            {
                var r = k < skills.Count ? skills[k] : null;
                skillBg[k].gameObject.SetActive(r != null);
                if (r == null) continue;
                bool on = have && GgomaSkills.IsUnlocked(r);
                Skin(skillBg[k], on ? SkillOnSprite : SkillOffSprite, 3f);
                string head = on ? $"<b>{r.name}</b>" : $"<color=#7A6E64>잠금 Lv.{Formation.EvoLevel(r.stage)}</color> <b>{r.name}</b>";
                skillText[k].text = head + "\n<size=90%>" + GgomaSkills.EffectText(r) + "</size>";
                skillText[k].color = on ? Brown : new Color(0.45f, 0.4f, 0.36f);
            }
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
