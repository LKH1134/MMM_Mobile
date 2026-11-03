using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 일일 퀘스트 창. 코드로 만든 오버레이 캔버스 하나를 처음 열 때 만들어 두고 다시 쓴다 (씬에 둘 것 없음).
    // 퀘스트 한 줄 = 설명 · 진행 막대 · 보상 · "받기" 버튼. 열려 있는 동안 진행도와 00시까지 남은 시간이 바뀌는 대로 다시 그린다
    public class DailyQuestPopup : MonoBehaviour
    {
        const int SortingOrder = 10100;     // PopupLayer.Popup(Main 캔버스 10000 + 100)과 같은 층. 한 번에 하나만 뜨게 PopupGate에 알린다
        const string PanelSprite = "Art/Dream/UI/panel_paper";
        const string TitleSprite = "Art/Dream/UI/window_title";
        const string ButtonSprite = "Art/Dream/UI/btn_go";
        const float PanelWidth = 900f, RowHeight = 170f, RowStep = 180f;
        static readonly Color Ink = new Color(0.30f, 0.20f, 0.10f, 1f);

        static DailyQuestPopup instance;

        class Row
        {
            public TMP_Text label, progress, reward, buttonText;
            public RectTransform fill;
            public Button button;
            public Image buttonImage;
        }

        readonly System.Collections.Generic.List<Row> rows = new System.Collections.Generic.List<Row>();
        GameObject root;
        TMP_Text resetText;
        TMP_FontAsset font;
        int shownSecond = -1;

        public static void Open()
        {
            if (instance == null) instance = Build();
            instance.Show();
        }

        static DailyQuestPopup Build()
        {
            var go = new GameObject("DailyQuestPopup", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2340f);
            scaler.matchWidthOrHeight = 0.5f;
            var popup = go.AddComponent<DailyQuestPopup>();
            popup.font = BorrowFont(go.transform);
            popup.BuildUi();
            SceneManager.sceneLoaded += (s, m) => popup.Hide();     // 다른 씬으로 넘어가면 닫는다
            return popup;
        }

        // 한글 글꼴은 씬에 있는 글자에서 가져온다 (TMP 기본 글꼴은 한글이 없다. AdBox와 같은 방식)
        static TMP_FontAsset BorrowFont(Transform self)
        {
            foreach (var t in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (!t.transform.IsChildOf(self) && t.font != null) return t.font;
            return null;
        }

        void Show()
        {
            if (root.activeSelf) return;
            root.SetActive(true);
            shownSecond = -1;
            DailyQuests.Changed += Refresh;
            PopupGate.Opened(this, Hide);       // 뒤로가기·다른 팝업이 열리면 닫힌다
            Refresh();
        }

        void Hide()
        {
            if (root == null || !root.activeSelf) return;
            root.SetActive(false);
            DailyQuests.Changed -= Refresh;
            PopupGate.Closed(this);
        }

        void Update()
        {
            if (root == null || !root.activeSelf) return;
            int sec = Mathf.FloorToInt((float)DailyQuests.UntilReset.TotalSeconds);
            if (sec == shownSecond) return;
            shownSecond = sec;
            var t = System.TimeSpan.FromSeconds(sec);
            resetText.text = $"초기화까지 {(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }

        void Refresh()
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                int target = DailyQuests.Target(i), done = DailyQuests.Progress(i);
                bool claimed = DailyQuests.IsClaimed(i), canClaim = DailyQuests.CanClaim(i);
                row.label.text = DailyQuests.Describe(i);
                row.progress.text = $"{done:N0} / {target:N0}";
                row.fill.anchorMax = new Vector2(target <= 0 ? 0f : Mathf.Clamp01((float)done / target), 1f);
                row.reward.text = $"<color=#B07A00>골드</color> +{Wallet.Format(DailyQuests.RewardGold(i))}   <color=#2A7FB0>균사석</color> +{Wallet.Format(DailyQuests.RewardMycelite(i))}";
                row.buttonText.text = claimed ? "완료" : canClaim ? "받기" : "진행 중";
                row.button.interactable = canClaim;
                row.buttonImage.color = canClaim ? new Color(0.4f, 0.85f, 0.75f) : new Color(0.7f, 0.7f, 0.7f);
            }
        }

        // ── 화면 ──
        void BuildUi()
        {
            int n = DailyQuests.Count;
            float height = 640f + (n - 1) * RowStep;

            root = new GameObject("Root", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(transform, false);
            Stretch((RectTransform)root.transform);
            var dim = root.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.65f);       // 뒤를 어둡게 하고 누름을 막는다
            root.GetComponent<Button>().onClick.AddListener(Hide);      // 바깥을 누르면 닫는다

            var panel = NewImage("Panel", root.transform, Resources.Load<Sprite>(PanelSprite), Color.white, new Vector2(PanelWidth, height), Vector2.zero);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;     // 창 안을 눌러도 닫히지 않게
            var title = NewImage("Title", panel.transform, Resources.Load<Sprite>(TitleSprite), new Color(0.75f, 0.5f, 0.9f, 1f), new Vector2(560f, 120f), new Vector2(0f, height / 2f - 60f));
            title.type = Image.Type.Sliced;
            NewText("TitleText", title.transform, "일일 퀘스트", 54f, Color.white, new Vector2(520f, 100f), Vector2.zero);
            resetText = NewText("Reset", panel.transform, "", 32f, new Color(Ink.r, Ink.g, Ink.b, 0.8f), new Vector2(760f, 60f), new Vector2(0f, height / 2f - 190f));

            for (int i = 0; i < n; i++)
                rows.Add(BuildRow(panel.transform, i, new Vector2(0f, height / 2f - 330f - i * RowStep)));

            var close = NewButton("Close", panel.transform, "닫기", new Color(0.9f, 0.35f, 0.45f, 1f), new Vector2(340f, 130f), new Vector2(0f, -height / 2f + 110f), 48f, out _);
            close.onClick.AddListener(Hide);
            root.SetActive(false);
        }

        Row BuildRow(Transform parent, int index, Vector2 pos)
        {
            var row = new Row();
            var bg = NewImage("Row" + index, parent, null, new Color(1f, 1f, 1f, 0.35f), new Vector2(PanelWidth - 90f, RowHeight), pos);
            row.label = NewText("Label", bg.transform, "", 40f, Ink, new Vector2(520f, 56f), new Vector2(-95f, 48f));
            row.label.alignment = TextAlignmentOptions.Left;
            var track = NewImage("Bar", bg.transform, null, new Color(0f, 0f, 0f, 0.45f), new Vector2(520f, 28f), new Vector2(-95f, 0f));
            var fill = NewImage("Fill", track.transform, null, new Color(0.2f, 0.85f, 0.8f), Vector2.zero, Vector2.zero);
            row.fill = fill.rectTransform;
            row.fill.anchorMin = Vector2.zero;
            row.fill.anchorMax = Vector2.one;
            row.fill.offsetMin = row.fill.offsetMax = Vector2.zero;
            row.progress = NewText("Progress", track.transform, "", 24f, Color.white, new Vector2(520f, 28f), Vector2.zero);
            row.reward = NewText("Reward", bg.transform, "", 30f, Ink, new Vector2(520f, 44f), new Vector2(-95f, -46f));
            row.button = NewButton("Claim", bg.transform, "", Color.white, new Vector2(190f, 110f), new Vector2(280f, 0f), 42f, out var buttonText);
            row.buttonText = buttonText;
            row.buttonImage = row.button.targetGraphic as Image;
            int i = index;
            row.button.onClick.AddListener(() => DailyQuests.Claim(i));
            return row;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, Color color, Vector2 size, Vector2 pos)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        TMP_Text NewText(string name, Transform parent, string text, float size, Color color, Vector2 box, Vector2 pos)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = box;
            rt.anchoredPosition = pos;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            if (font != null) t.font = font;
            return t;
        }

        Button NewButton(string name, Transform parent, string label, Color tint, Vector2 size, Vector2 pos, float textSize, out TMP_Text text)
        {
            var img = NewImage(name, parent, Resources.Load<Sprite>(ButtonSprite), tint, size, pos);
            img.type = Image.Type.Sliced;
            img.raycastTarget = true;
            text = NewText("Label", img.transform, label, textSize, Color.white, size, Vector2.zero);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            return button;
        }
    }
}
