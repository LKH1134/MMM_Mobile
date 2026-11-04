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
        [SerializeField] float rowGap = 22f;
        [SerializeField] float padding = 30f;

        // 노트 모양: 갈색 표지(coverSprite) 위에 펼친 속지(pageSprite). 속지 오른쪽 면은 9-slice 오른쪽 테두리라 폭이 고정이고,
        // 그 경계(책 접힌 선)에 스프링을 걸고, 왼쪽 면에는 능력치 카드, 오른쪽 면에는 강화 버튼을 둔다
        [Header("노트 리소스 (Resources 경로)")]
        [SerializeField] string coverSprite = "UI/UI_PP_Under_Base02";
        [SerializeField] string pageSprite = "UI/UI_PP_Under_Base01";
        [SerializeField] string tabSprite = "UI/UI_PP_Under_Tag_Yellow";
        [SerializeField] string cardSprite = "UI/UI_PP_In_Color_Beige1";
        [SerializeField] string iconBoxSprite = "UI/UI_PP_In_Color_Beige2";
        [SerializeField] string springSprite = "UI/UI_PP_In_Spring";
        [SerializeField] string upgradeButtonSprite = "UI/UI_Btn_Yellow";
        [SerializeField] string lockSprite = "UI/UI_PP_In_Color_Brown";
        [SerializeField] string goldIcon = "UI/Icon/gold";   // 능력치 아이콘은 IdleProgress.Def.icon
        [SerializeField] float pageScale = 1.5f;             // 속지 9-slice 테두리 축소 배율 (오른쪽 면 453px → 302)
        [SerializeField] float coverTop = 16f;               // 속지 위로 드러나는 표지 두께

        class Row
        {
            public IdleStat stat;
            public TMP_Text level, value, cost, lockText;
            public Button button;
            public Image buttonImage;
            public GameObject lockCover, costRow;
            public TMP_Text buttonText;
            public long nextCost;       // Refresh 때 셈해 둔 다음 레벨 비용
            public bool buyable;        // 열렸고 최대·단계 제한이 아님
        }

        readonly List<Row> rows = new List<Row>();
        RectTransform body, content;
        float rightPage;   // 속지 오른쪽 면 폭 (책 접힌 선 ~ 오른쪽 끝)

        void Awake()
        {
            Build();
            if (toggleButton != null) toggleButton.onClick.AddListener(() => SetOpen(!body.gameObject.activeSelf));
            SetOpen(openOnStart);
        }

        void OnEnable()
        {
            Wallet.Changed += RefreshAfford;    // 전투 골드로 1초마다 온다 — 살 수 있는지만 다시 본다
            IdleProgress.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            Wallet.Changed -= RefreshAfford;
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
            bg.sprite = Resources.Load<Sprite>(coverSprite);
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = pageScale;
            bg.color = bg.sprite != null ? Color.white : new Color(0.59f, 0.37f, 0.2f);

            // 위 가운데 노란 탭 (표지 위로 튀어나온다)
            var tab = NewImage("Tab", body, Resources.Load<Sprite>(tabSprite), Color.white);
            tab.type = Image.Type.Sliced;
            tab.pixelsPerUnitMultiplier = pageScale;
            var tabRt = tab.rectTransform;
            tabRt.anchorMin = tabRt.anchorMax = new Vector2(0.5f, 1f);
            tabRt.pivot = new Vector2(0.5f, 0f);
            tabRt.anchoredPosition = new Vector2(0f, -coverTop);
            tabRt.sizeDelta = new Vector2(420f, 80f);

            var page = NewImage("Page", body, Resources.Load<Sprite>(pageSprite), Color.white);
            page.type = Image.Type.Sliced;
            page.pixelsPerUnitMultiplier = pageScale;
            if (page.sprite == null) page.color = new Color(0.96f, 0.87f, 0.73f);
            Stretch(page.rectTransform);
            page.rectTransform.offsetMax = new Vector2(0f, -coverTop);
            rightPage = page.sprite != null ? page.sprite.border.z / pageScale : 300f;

            // 스크롤: body(ScrollRect) > Viewport(가림) > Content(줄들)
            var viewport = NewRect("Viewport", body);
            Stretch(viewport);
            viewport.offsetMax = new Vector2(0f, -coverTop - 8f);
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

        const float LockTextLeft = 330f;   // 잠금 문구 왼쪽 끝 (이름·값 글자 오른쪽)
        const float CardSpineGap = 20f;    // 카드 오른쪽 끝 ~ 접힌 선
        const float ButtonSpineInset = 34f; // 접힌 선 ~ 버튼 왼쪽 끝 (스프링 오른쪽 반이 버튼에 살짝 걸친다)
        const float ButtonWidth = 230f;
        const string LockIcon = "UI/Icon/lock";
        static readonly Color TextDark = new Color(0.36f, 0.22f, 0.12f);
        static readonly Color LockTint = new Color(1f, 1f, 1f, 0.82f);

        Row BuildRow(IdleProgress.Def d, float y)
        {
            var r = new Row { stat = d.stat };
            var rowRt = NewRect("Row_" + d.stat, content);
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.offsetMin = new Vector2(padding, y - rowHeight);
            rowRt.offsetMax = new Vector2(-padding, y);

            // 줄 좌표에서 책 접힌 선까지 (줄 오른쪽 끝 기준)
            float spine = rightPage - padding;

            // 왼쪽 면: 능력치 카드 (접힌 선 조금 앞에서 끝나고, 그 위를 스프링이 덮는다)
            var card = NewImage("Card", rowRt, Resources.Load<Sprite>(cardSprite), Color.white);
            card.type = Image.Type.Sliced;
            var cardRt = card.rectTransform;
            Stretch(cardRt);
            cardRt.offsetMax = new Vector2(-spine - CardSpineGap, 0f);

            // 카드 안 왼쪽: 아이콘 + 레벨
            var iconBox = NewImage("IconBox", cardRt, Resources.Load<Sprite>(iconBoxSprite), Color.white);
            iconBox.type = Image.Type.Sliced;
            SetRect(iconBox.rectTransform, new Vector2(0f, 0.5f), new Vector2(16f + 68f, 0f), new Vector2(136f, 136f));
            var icon = NewImage("Icon", iconBox.rectTransform, string.IsNullOrEmpty(d.icon) ? null : Resources.Load<Sprite>(d.icon), Color.white);
            icon.preserveAspect = true;
            SetRect(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(88f, 88f));
            r.level = NewText("Level", iconBox.rectTransform, 28f, TextAlignmentOptions.Center, TextDark);
            SetRect(r.level.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(136f, 40f));

            // 카드 안 가운데: 이름 + 값
            var name = NewText("Name", cardRt, 36f, TextAlignmentOptions.Left, new Color(0.45f, 0.3f, 0.18f));
            name.text = d.name;
            SetRect(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(180f + 200f, 30f), new Vector2(400f, 50f));
            r.value = NewText("Value", cardRt, 48f, TextAlignmentOptions.Left, TextDark);
            SetRect(r.value.rectTransform, new Vector2(0f, 0.5f), new Vector2(180f + 200f, -30f), new Vector2(400f, 60f));

            // 오른쪽 면: 강화 버튼 (강화 / 금화 비용), 오른쪽 면 가운데
            r.buttonImage = NewImage("Upgrade", rowRt, Resources.Load<Sprite>(upgradeButtonSprite), Color.white);
            r.buttonImage.type = Image.Type.Sliced;
            r.buttonImage.raycastTarget = true;
            var buttonRt = r.buttonImage.rectTransform;
            SetRect(buttonRt, new Vector2(1f, 0.5f), new Vector2(-(spine - ButtonSpineInset - ButtonWidth * 0.5f), 0f), new Vector2(ButtonWidth, rowHeight - 10f));
            r.button = r.buttonImage.gameObject.AddComponent<Button>();
            r.button.targetGraphic = r.buttonImage;
            r.button.onClick.AddListener(() => IdleProgress.TryUpgrade(d.stat));
            r.buttonImage.gameObject.AddComponent<HoldRepeat>();
            r.buttonText = NewText("Label", buttonRt, 36f, TextAlignmentOptions.Center, TextDark);
            r.buttonText.text = "강화";
            SetRect(r.buttonText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(ButtonWidth - 20f, 50f));
            r.costRow = NewRect("Cost", buttonRt).gameObject;
            var costRt = (RectTransform)r.costRow.transform;
            SetRect(costRt, new Vector2(0.5f, 0.5f), new Vector2(0f, -26f), new Vector2(ButtonWidth - 20f, 50f));
            var coin = NewImage("Gold", costRt, Resources.Load<Sprite>(goldIcon), Color.white);
            coin.preserveAspect = true;
            SetRect(coin.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-72f, 0f), new Vector2(42f, 42f));
            r.cost = NewText("Value", costRt, 34f, TextAlignmentOptions.Left, TextDark);
            SetRect(r.cost.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(28f, 0f), new Vector2(150f, 50f));
            r.cost.enableAutoSizing = true;
            r.cost.fontSizeMin = 22f;
            r.cost.fontSizeMax = 34f;

            // 접힌 선 위 스프링 두 개 (카드와 버튼 위에 그린다)
            var springSp = Resources.Load<Sprite>(springSprite);
            if (springSp != null)
                for (int i = 0; i < 2; i++)
                {
                    var spring = NewImage("Spring", rowRt, springSp, Color.white);
                    spring.preserveAspect = true;
                    SetRect(spring.rectTransform, new Vector2(1f, 0.5f), new Vector2(-spine, i == 0 ? 36f : -36f), new Vector2(104f, 50f));
                }

            // 잠금 덮개: 카드·버튼을 각각 덮는다. 줄 전체를 받아서 잠긴 줄 버튼을 못 누르게
            var cover = NewRect("Lock", rowRt);
            Stretch(cover);
            cover.gameObject.AddComponent<Image>().color = Color.clear;
            r.lockCover = cover.gameObject;
            var lockSp = Resources.Load<Sprite>(lockSprite);
            var cardCover = NewImage("Card", cover, lockSp, LockTint);
            cardCover.type = Image.Type.Sliced;
            Stretch(cardCover.rectTransform);
            cardCover.rectTransform.offsetMax = cardRt.offsetMax;
            var buttonCover = NewImage("Button", cover, lockSp, LockTint);
            buttonCover.type = Image.Type.Sliced;
            SetRect(buttonCover.rectTransform, buttonRt.anchorMin, buttonRt.anchoredPosition, buttonRt.sizeDelta);
            var lockIcon = NewImage("Icon", buttonCover.rectTransform, Resources.Load<Sprite>(LockIcon), Color.white);
            lockIcon.preserveAspect = true;
            SetRect(lockIcon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
            r.lockText = NewText("Text", cardCover.rectTransform, 34f, TextAlignmentOptions.Center, Color.white);
            // 왼쪽 이름·값(x 180~)과 겹치지 않게 그 오른쪽 자리에만 쓴다. 폭이 좁은 화면에서는 글자를 줄인다
            Stretch(r.lockText.rectTransform);
            r.lockText.rectTransform.offsetMin = new Vector2(LockTextLeft, 0f);
            r.lockText.rectTransform.offsetMax = new Vector2(-20f, 0f);
            r.lockText.enableAutoSizing = true;
            r.lockText.fontSizeMin = 22f;
            r.lockText.fontSizeMax = 34f;
            if (d.unlockLv > 0) r.lockText.text = $"{IdleProgress.Get(d.unlockStat).name} Lv {d.unlockLv} 달성 시 잠금 해제";
            return r;
        }

        void Refresh()
        {
            if (body == null || !body.gameObject.activeInHierarchy) return;
            foreach (var r in rows)
            {
                int lv = IdleProgress.Level(r.stat);
                bool unlocked = IdleProgress.Unlocked(r.stat);
                bool max = IdleProgress.IsMax(r.stat);
                bool capped = IdleProgress.IsCapped(r.stat);
                long cost = IdleProgress.Cost(r.stat);
                r.nextCost = cost;
                r.buyable = unlocked && !max && !capped;
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
            }
            RefreshAfford();
        }

        // 잔액만 바뀌었을 때: 버튼을 누를 수 있는지·비용 색만 (글자·배치는 그대로)
        void RefreshAfford()
        {
            if (body == null || !body.gameObject.activeInHierarchy) return;
            long gold = Wallet.Gold;
            foreach (var r in rows)
            {
                bool enough = gold >= r.nextCost;
                bool can = r.buyable && enough;
                if (r.button.interactable != can)
                {
                    r.button.interactable = can;
                    r.buttonImage.color = can ? Color.white : DisabledButton;
                }
                var costColor = enough ? TextDark : ShortCost;
                if (r.cost.color != costColor) r.cost.color = costColor;
            }
        }

        static readonly Color DisabledButton = new Color(0.6f, 0.6f, 0.6f);
        static readonly Color ShortCost = new Color(0.85f, 0.25f, 0.2f);

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
