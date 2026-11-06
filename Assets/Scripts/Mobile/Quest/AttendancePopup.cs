using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 출석 체크 창 (Attendance.Days일, 5칸씩 줄 — 마지막 줄은 가운데). DailyQuestPopup처럼 코드로 만든 오버레이 캔버스 하나를 처음 열 때 만들어 다시 쓴다 (씬에 둘 것 없음).
    // 칸 = "N일째" · 보상 그림 · 개수. 받은 날은 체크 표시, 오늘 받은 칸은 노란 칸으로 튀어 오르고, 다음 날 칸에 NEXT.
    // 아래에 다음 출석(한국 00시)까지 남은 시간
    public class AttendancePopup : MonoBehaviour
    {
        const int SortingOrder = 10100;     // PopupLayer.Popup과 같은 층 (DailyQuestPopup과 같다)
        const string PanelSprite = "UI/UI_PP_Out_Paper2";
        const string TitleSprite = "UI/UI_PP_Under_Tag_Yellow";
        const string CellSprite = "UI/UI_PP_In_Color_Beige1";
        const string ClaimedSprite = "UI/UI_PP_In_Color_Beige2";
        const string TodaySprite = "UI/UI_PP_In_Color_Green";
        const string FinalSprite = "UI/UI_PP_In_Color_Yellow";    // 마지막 날 (전설 무기)
        const string GoldIcon = "UI/Icon/gold";
        const string MyceliteIcon = "UI/Icon/mycelium_stone";
        const string CheckIcon = "UI/Icon/check";

        const int Columns = 5;
        const float PanelWidth = 960f;
        const float PanelBottom = 134f;    // 마지막 줄 아래 ~ 패널 아래 끝 (남은 시간 글자 자리)
        const float CellWidth = 165f, CellHeight = 220f, CellGap = 16f;
        const float GridTop = 330f;         // 패널 위 끝에서 첫 줄 가운데까지
        static readonly Color Ink = new Color(0.30f, 0.20f, 0.10f, 1f);
        static readonly Color NextColor = new Color(0.95f, 0.30f, 0.35f, 1f);

        static AttendancePopup instance;

        class Cell
        {
            public RectTransform rect;
            public Image bg, icon, check;
            public TMP_Text amount, next;
        }

        readonly List<Cell> cells = new List<Cell>();
        GameObject root;
        TMP_Text messageText, timerText;
        TMP_FontAsset font;
        int shownSecond = -1;
        Coroutine pop;

        // justClaimed = 방금 받은 날 (0 = 그냥 열기)
        public static void Open(int justClaimed = 0)
        {
            if (instance == null) instance = Build();
            instance.Show(justClaimed);
        }

        static AttendancePopup Build()
        {
            var go = new GameObject("AttendancePopup", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2340f);
            scaler.matchWidthOrHeight = 0.5f;
            var popup = go.AddComponent<AttendancePopup>();
            popup.font = BorrowFont(go.transform);
            popup.BuildUi();
            SceneManager.sceneLoaded += (s, m) => popup.Hide();     // 다른 씬으로 넘어가면 닫는다
            return popup;
        }

        // 한글 글꼴은 씬에 있는 글자에서 가져온다 (TMP 기본 글꼴은 한글이 없다)
        static TMP_FontAsset BorrowFont(Transform self)
        {
            foreach (var t in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (!t.transform.IsChildOf(self) && t.font != null) return t.font;
            return null;
        }

        void Show(int justClaimed)
        {
            if (!root.activeSelf)
            {
                root.SetActive(true);
                Attendance.Changed += RefreshCurrent;
                PopupGate.Opened(this, Hide);       // 뒤로가기·다른 팝업이 열리면 닫힌다
            }
            shownSecond = -1;
            Refresh(justClaimed);
        }

        void Hide()
        {
            if (root == null || !root.activeSelf) return;
            if (pop != null) StopCoroutine(pop);
            pop = null;
            root.SetActive(false);
            Attendance.Changed -= RefreshCurrent;
            PopupGate.Closed(this);
        }

        void Update()
        {
            if (root == null || !root.activeSelf) return;
            if (Attendance.Finished || !Attendance.ClaimedToday)
            {
                if (shownSecond == -2) return;
                shownSecond = -2;
                timerText.text = Attendance.Finished ? $"{Attendance.Days}일 출석을 모두 마쳤어요!" : "";
                return;
            }
            int sec = Mathf.Max(0, Mathf.FloorToInt((float)Attendance.UntilNextDay.TotalSeconds));
            if (sec == shownSecond) return;
            shownSecond = sec;
            var t = System.TimeSpan.FromSeconds(sec);
            timerText.text = $"다음 출석까지 {(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }

        void RefreshCurrent() => Refresh(0);

        void Refresh(int justClaimed)
        {
            int claimed = Attendance.Claimed;
            int nextDay = claimed < Attendance.Days ? claimed + 1 : 0;
            for (int i = 0; i < cells.Count; i++)
            {
                int day = i + 1;
                var cell = cells[i];
                var reward = Attendance.RewardOf(day);
                bool got = day <= claimed;
                cell.bg.sprite = Resources.Load<Sprite>(day == justClaimed ? TodaySprite : got ? ClaimedSprite : day == Attendance.Days ? FinalSprite : CellSprite);
                cell.icon.sprite = IconOf(reward);
                cell.icon.color = got ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
                cell.check.gameObject.SetActive(got);
                cell.amount.text = AmountOf(reward);
                cell.next.gameObject.SetActive(day == nextDay);
                cell.rect.localScale = Vector3.one;
            }
            messageText.text = justClaimed > 0 ? $"{justClaimed}일째 출석 보상을 받았어요!" : $"{Attendance.Days}일째에는 전설 무기 <color=#E08A00>{Attendance.WeaponTitle}</color>!";
            if (justClaimed > 0 && justClaimed <= cells.Count)
            {
                if (pop != null) StopCoroutine(pop);
                pop = StartCoroutine(Pop(cells[justClaimed - 1].rect));
            }
        }

        static Sprite IconOf(Attendance.Reward r)
        {
            switch (r.kind)
            {
                case AttendanceRewardKind.Gold: return Resources.Load<Sprite>(GoldIcon);
                case AttendanceRewardKind.Mycelite: return Resources.Load<Sprite>(MyceliteIcon);
                default: return Equipment.Weapon.Sprite(Attendance.WeaponItem);
            }
        }

        static string AmountOf(Attendance.Reward r)
        {
            switch (r.kind)
            {
                case AttendanceRewardKind.Gold: return Wallet.Format(Attendance.GoldOf(r));
                case AttendanceRewardKind.Mycelite: return "x" + r.amount.ToString("N0");
                default: return Attendance.WeaponTitle;
            }
        }

        // 오늘 받은 칸이 도장 찍히듯 커졌다 돌아온다
        IEnumerator Pop(RectTransform rt)
        {
            const float Duration = 0.35f;
            for (float t = 0f; t < Duration; t += Time.unscaledDeltaTime)
            {
                float k = t / Duration;
                rt.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            rt.localScale = Vector3.one;
            pop = null;
        }

        // ── 화면 ──
        void BuildUi()
        {
            root = new GameObject("Root", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(transform, false);
            Stretch((RectTransform)root.transform);
            var dim = root.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.65f);       // 뒤를 어둡게 하고 누름을 막는다
            root.GetComponent<Button>().onClick.AddListener(Hide);      // 바깥을 누르면 닫는다

            int rows = (Attendance.Days + Columns - 1) / Columns;
            float panelHeight = GridTop + rows * (CellHeight + CellGap * 2f) + PanelBottom;     // 줄 수에 맞춘 높이
            var panel = NewImage("Panel", root.transform, Resources.Load<Sprite>(PanelSprite), new Vector2(PanelWidth, panelHeight), new Vector2(0f, 60f));
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;     // 창 안을 눌러도 닫히지 않게
            float top = panelHeight / 2f;
            var title = NewImage("Title", panel.transform, Resources.Load<Sprite>(TitleSprite), new Vector2(560f, 120f), new Vector2(0f, top - 60f));
            title.type = Image.Type.Sliced;
            NewText("TitleText", title.transform, "출석 체크", 54f, Color.white, new Vector2(520f, 100f), Vector2.zero);
            messageText = NewText("Message", panel.transform, "", 38f, Ink, new Vector2(860f, 60f), new Vector2(0f, top - 190f));

            for (int i = 0; i < Attendance.Days; i++) cells.Add(BuildCell(panel.transform, i, CellPosition(i, top)));

            timerText = NewText("Timer", panel.transform, "", 34f, new Color(Ink.r, Ink.g, Ink.b, 0.8f), new Vector2(860f, 56f), new Vector2(0f, -top + 90f));
            PopupBottomBand.Attach(root.transform, Hide);
            root.SetActive(false);
        }

        // 5칸씩 줄을 채우고, 마지막 줄은 가운데 맞춤
        static Vector2 CellPosition(int index, float top)
        {
            int row = index / Columns, col = index % Columns;
            int inRow = Mathf.Min(Columns, Attendance.Days - row * Columns);
            float rowWidth = inRow * CellWidth + (inRow - 1) * CellGap;
            float x = -rowWidth / 2f + CellWidth / 2f + col * (CellWidth + CellGap);
            float y = top - GridTop - CellHeight / 2f - row * (CellHeight + CellGap * 2f);
            return new Vector2(x, y);
        }

        Cell BuildCell(Transform parent, int index, Vector2 pos)
        {
            var cell = new Cell();
            cell.bg = NewImage("Day" + (index + 1), parent, Resources.Load<Sprite>(CellSprite), new Vector2(CellWidth, CellHeight), pos);
            cell.bg.type = Image.Type.Sliced;
            cell.rect = cell.bg.rectTransform;
            NewText("Day", cell.bg.transform, $"{index + 1}일째", 32f, Ink, new Vector2(CellWidth, 44f), new Vector2(0f, CellHeight / 2f - 30f));
            cell.icon = NewImage("Icon", cell.bg.transform, null, new Vector2(96f, 96f), new Vector2(0f, 4f));
            cell.icon.preserveAspect = true;
            cell.check = NewImage("Check", cell.bg.transform, Resources.Load<Sprite>(CheckIcon), new Vector2(90f, 90f), new Vector2(0f, 4f));
            cell.check.preserveAspect = true;
            cell.amount = NewText("Amount", cell.bg.transform, "", 28f, Ink, new Vector2(CellWidth - 12f, 40f), new Vector2(0f, -CellHeight / 2f + 30f));
            cell.amount.enableAutoSizing = true;     // 무기 이름·큰 골드가 칸보다 길 수 있다
            cell.amount.fontSizeMin = 18f;
            cell.amount.fontSizeMax = 28f;
            cell.next = NewText("Next", cell.bg.transform, "NEXT", 30f, NextColor, new Vector2(CellWidth, 40f), new Vector2(0f, CellHeight / 2f + 14f));
            return cell;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 pos)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            img.sprite = sprite;
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
            OutlineText.ApplyIfWhite(t);   // 흰 글자는 외곽선
            return t;
        }
    }
}
