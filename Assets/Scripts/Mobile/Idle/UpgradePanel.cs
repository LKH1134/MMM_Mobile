using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 방치형 강화 목록 (레전드 오브 슬라임식): 줄마다 아이콘·레벨 / 이름·현재 값 / 강화 버튼(비용).
    // 잠긴 줄은 "OO Lv N 달성 시 잠금 해제"로 덮는다. 누르고 있으면 계속 강화된다(HoldRepeat). 줄이 판보다 길면 위아래로 스크롤한다.
    // 이 오브젝트는 늘 켜 두고, 목록(body)만 켜고 끈다 — toggleButton(하단바 업그레이드)으로 열고 닫는다
    public class UpgradePanel : MonoBehaviour
    {
        [SerializeField] Button toggleButton;
        [SerializeField] bool openOnStart = true;
        [SerializeField] TMP_FontAsset font;
        [SerializeField] float rowHeight = 170f;
        [SerializeField] float rowGap = 14f;
        [SerializeField] float padding = 24f;

        [Header("임시 리소스 (Resources 경로)")]
        [SerializeField] string panelSprite = "Art/Dream/UI/panel_paper";
        [SerializeField] string buttonSprite = "Art/Dream/UI/btn_go";
        [SerializeField] string goldIcon = "Art/Dream/Icons/gold";   // 능력치 아이콘은 IdleProgress.Def.icon

        class Row
        {
            public IdleStat stat;
            public TMP_Text level, value, cost, lockText;
            public Button button;
            public Image buttonImage;
            public GameObject lockCover, costRow;
            public TMP_Text buttonText;
        }

        readonly List<Row> rows = new List<Row>();
        RectTransform body, content;

        void Awake()
        {
            Build();
            if (toggleButton != null) toggleButton.onClick.AddListener(() => SetOpen(!body.gameObject.activeSelf));
            SetOpen(openOnStart);
        }

        void OnEnable()
        {
            Wallet.Changed += Refresh;
            IdleProgress.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            Wallet.Changed -= Refresh;
            IdleProgress.Changed -= Refresh;
        }

        public void SetOpen(bool open)
        {
            body.gameObject.SetActive(open);
            if (open) Refresh();
        }

        void Build()
        {
            body = new GameObject("Body", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            body.SetParent(transform, false);
            Stretch(body);
            var bg = body.GetComponent<Image>();
            bg.sprite = Resources.Load<Sprite>(panelSprite);
            bg.type = Image.Type.Sliced;
            bg.color = bg.sprite != null ? Color.white : new Color(0.96f, 0.92f, 0.84f);

            // 스크롤: body(ScrollRect) > Viewport(가림) > Content(줄들)
            var viewport = NewRect("Viewport", body);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();   // 줄 사이 빈 곳도 끌어서 스크롤되게
            hit.color = Color.clear;
            content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, padding * 2f + IdleProgress.Defs.Length * (rowHeight + rowGap) - rowGap);
            var scroll = body.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            float y = -padding;
            foreach (var d in IdleProgress.Defs)
            {
                rows.Add(BuildRow(d, y));
                y -= rowHeight + rowGap;
            }
        }

        Row BuildRow(IdleProgress.Def d, float y)
        {
            var r = new Row { stat = d.stat };
            var rowRt = NewRect("Row_" + d.stat, content);
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.offsetMin = new Vector2(padding, y - rowHeight);
            rowRt.offsetMax = new Vector2(-padding, y);
            var rowBg = rowRt.gameObject.AddComponent<Image>();
            rowBg.color = new Color(0.45f, 0.33f, 0.24f, 0.18f);

            // 왼쪽: 아이콘 + 레벨
            var iconBox = NewImage("IconBox", rowRt, null, new Color(0.36f, 0.26f, 0.2f, 0.85f));
            SetRect(iconBox.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f + 70f, 0f), new Vector2(140f, 140f));
            var icon = NewImage("Icon", iconBox.rectTransform, string.IsNullOrEmpty(d.icon) ? null : Resources.Load<Sprite>(d.icon), Color.white);
            icon.preserveAspect = true;
            SetRect(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(90f, 90f));
            r.level = NewText("Level", iconBox.rectTransform, 30f, TextAlignmentOptions.Center, Color.white);
            SetRect(r.level.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(140f, 40f));

            // 가운데: 이름 + 값
            var name = NewText("Name", rowRt, 36f, TextAlignmentOptions.Left, new Color(0.36f, 0.24f, 0.14f));
            name.text = d.name;
            SetRect(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(180f + 200f, 30f), new Vector2(400f, 50f));
            r.value = NewText("Value", rowRt, 48f, TextAlignmentOptions.Left, new Color(0.25f, 0.16f, 0.1f));
            SetRect(r.value.rectTransform, new Vector2(0f, 0.5f), new Vector2(180f + 200f, -30f), new Vector2(400f, 60f));

            // 오른쪽: 강화 버튼 (강화 / 금화 비용)
            r.buttonImage = NewImage("Upgrade", rowRt, Resources.Load<Sprite>(buttonSprite), Color.white);
            r.buttonImage.type = Image.Type.Sliced;
            r.buttonImage.raycastTarget = true;
            SetRect(r.buttonImage.rectTransform, new Vector2(1f, 0.5f), new Vector2(-14f - 150f, 0f), new Vector2(300f, 136f));
            r.button = r.buttonImage.gameObject.AddComponent<Button>();
            r.button.targetGraphic = r.buttonImage;
            r.button.onClick.AddListener(() => IdleProgress.TryUpgrade(d.stat));
            r.buttonImage.gameObject.AddComponent<HoldRepeat>();
            r.buttonText = NewText("Label", r.buttonImage.rectTransform, 36f, TextAlignmentOptions.Center, Color.white);
            r.buttonText.text = "강화";
            SetRect(r.buttonText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(280f, 50f));
            r.costRow = NewRect("Cost", r.buttonImage.rectTransform).gameObject;
            var costRt = (RectTransform)r.costRow.transform;
            SetRect(costRt, new Vector2(0.5f, 0.5f), new Vector2(0f, -26f), new Vector2(280f, 50f));
            var coin = NewImage("Gold", costRt, Resources.Load<Sprite>(goldIcon), Color.white);
            coin.preserveAspect = true;
            SetRect(coin.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-80f, 0f), new Vector2(42f, 42f));
            r.cost = NewText("Value", costRt, 34f, TextAlignmentOptions.Left, Color.white);
            SetRect(r.cost.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(40f, 0f), new Vector2(190f, 50f));

            // 잠금 덮개
            var cover = NewImage("Lock", rowRt, null, new Color(0.15f, 0.1f, 0.08f, 0.72f));
            cover.raycastTarget = true;   // 잠긴 줄 버튼을 못 누르게
            Stretch(cover.rectTransform);
            r.lockCover = cover.gameObject;
            r.lockText = NewText("Text", cover.rectTransform, 34f, TextAlignmentOptions.Center, Color.white);
            Stretch(r.lockText.rectTransform);
            if (d.unlockLv > 0) r.lockText.text = $"{IdleProgress.Get(d.unlockStat).name} Lv {d.unlockLv} 달성 시 잠금 해제";
            return r;
        }

        void Refresh()
        {
            if (body == null || !body.gameObject.activeInHierarchy) return;
            long gold = Wallet.Gold;
            foreach (var r in rows)
            {
                int lv = IdleProgress.Level(r.stat);
                bool unlocked = IdleProgress.Unlocked(r.stat);
                bool max = IdleProgress.IsMax(r.stat);
                bool capped = IdleProgress.IsCapped(r.stat);
                long cost = IdleProgress.Cost(r.stat);
                r.level.text = "LV " + lv;
                r.value.text = IdleProgress.FormatValue(r.stat, IdleProgress.Value(r.stat));
                r.lockCover.SetActive(!unlocked);
                r.costRow.SetActive(!max && !capped);
                // 단계 제한: 다음 단계를 여는 기준 강화 레벨을 버튼에 적는다
                var d = IdleProgress.Get(r.stat);
                r.buttonText.text = max ? "MAX" : capped ? $"{IdleProgress.Get(d.unlockStat).name}\nLv {IdleProgress.NextCapGateLevel(r.stat)} 필요" : "강화";
                r.buttonText.fontSize = capped ? 28f : 36f;
                r.buttonText.rectTransform.sizeDelta = new Vector2(280f, capped ? 100f : 50f);
                r.buttonText.rectTransform.anchoredPosition = new Vector2(0f, max || capped ? 0f : 24f);
                r.cost.text = Wallet.Format(cost);
                bool can = unlocked && !max && !capped && gold >= cost;
                r.button.interactable = can;
                r.buttonImage.color = can ? Color.white : new Color(0.6f, 0.6f, 0.6f);
                r.cost.color = gold >= cost ? Color.white : new Color(1f, 0.55f, 0.5f);
            }
        }

        // ── 만들기 도우미 ──

        static RectTransform NewRect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var img = NewRect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        TMP_Text NewText(string name, Transform parent, float size, TextAlignmentOptions align, Color color)
        {
            var t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
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
