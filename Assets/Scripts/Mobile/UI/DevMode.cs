using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 테스트용 개발자 모드. 에디터와 Development Build에서만 생긴다 (출시 빌드에는 없음).
    // 키보드 ` 키로 팝업을 열고 닫는다. 씬이 바뀌어도 남는다 (씬에 둘 것 없음).
    // 재화·꾸미기·창고 버섯·세계수·방치 스테이지·던전 횟수·광고 건너뛰기·게임 속도·앱 밖 시간을 바로 바꾼다.
    // 위쪽 입력칸에 숫자(1.5c 같은 단위 가능)를 넣고 "=입력"을 누르면 그 값으로 정한다
    public class DevMode : MonoBehaviour
    {
        const int SortingOrder = 32500;                 // 씬 로딩(32000) 위
        const string SkipAdsKey = "Dev.SkipAds";
        const float LiveEvery = 0.3f;

        static DevMode instance;

        public static bool Enabled => Application.isEditor || Debug.isDebugBuild;
        // 광고를 띄우지 않고 끝까지 본 것으로 (AdService)
        public static bool SkipAds => Enabled && PlayerPrefs.GetInt(SkipAdsKey, 0) == 1;

        GameObject root;
        RectTransform list;
        TMP_InputField input;
        TMP_FontAsset font;
        TMP_Text status;
        readonly List<(TMP_Text text, Func<string> value)> live = new List<(TMP_Text, Func<string>)>();
        float liveIn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (!Enabled || instance != null) return;
            var go = new GameObject("DevMode");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DevMode>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.backquoteKey.wasPressedThisFrame) Toggle();
            if (root == null || !root.activeSelf) return;
            liveIn -= Time.unscaledDeltaTime;
            if (liveIn > 0f) return;
            liveIn = LiveEvery;
            foreach (var (text, value) in live) if (text != null) text.text = value();
        }

        void Toggle()
        {
            if (root != null && root.activeSelf) { root.SetActive(false); return; }
            if (root == null) Build();
            root.SetActive(true);
            liveIn = 0f;
        }

        // ── 화면 ──

        void Build()
        {
            font = BorrowFont();
            root = new GameObject("DevModeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2340f);
            scaler.matchWidthOrHeight = 0.5f;

            var dim = TreeUi.New("Dim", root.transform);
            TreeUi.Stretch(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);   // 뒤 화면 터치를 막는다

            var window = TreeUi.New("Window", root.transform);
            TreeUi.Place(window, Vector2.zero, Vector2.one, new Vector2(40f, 100f), new Vector2(-40f, -100f));
            var bg = window.gameObject.AddComponent<Image>();
            bg.color = new Color(0.97f, 0.93f, 0.84f, 1f);

            var title = TreeUi.Text(window, "개발자 모드  <size=26>(` 키로 닫기)</size>", 46f, TreeUi.Ink, font);
            title.fontStyle = FontStyles.Bold;
            TreeUi.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -100f), new Vector2(-200f, -10f));
            var close = SmallButton(window, "닫기", () => root.SetActive(false), TreeUi.Bad);
            var crt = (RectTransform)close.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(1f, 1f);
            crt.anchoredPosition = new Vector2(-24f, -14f);
            crt.sizeDelta = new Vector2(150f, 80f);

            // 입력칸 (목록을 다시 만들어도 남게 목록 밖에 둔다)
            var inputRow = TreeUi.New("Input", window);
            TreeUi.Place(inputRow, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -200f), new Vector2(-24f, -110f));
            input = MakeInput(inputRow);
            status = TreeUi.Text(window, "", 26f, TreeUi.Good, font);
            TreeUi.Place(status.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -250f), new Vector2(-32f, -205f));

            var body = TreeUi.New("Body", window);
            TreeUi.Place(body, Vector2.zero, Vector2.one, new Vector2(16f, 16f), new Vector2(-16f, -260f));
            list = TreeUi.ScrollList(body);
            BuildList();
        }

        void BuildList()
        {
            live.Clear();

            // 재화
            TreeUi.Header(list, "재화", font);
            CurrencyCard(Currency.Gold, "골드", new (string, Func<long, long>)[]
            {
                ("0", _ => 0), ("+1a", v => v + 1_000), ("+1b", v => v + 1_000_000), ("+1c", v => v + 1_000_000_000), ("×10", v => Mul(v, 10)),
            });
            CurrencyCard(Currency.Mycelite, "균사석", new (string, Func<long, long>)[]
            {
                ("0", _ => 0), ("+100", v => v + 100), ("+1천", v => v + 1_000), ("+1만", v => v + 10_000), ("×10", v => Mul(v, 10)),
            });

            // 꾸미기
            TreeUi.Header(list, "세계수 꾸미기", font);
            var deco = Row(150f);
            Live(TreeUi.Line(deco, "", 32f, TreeUi.Ink, font, 24f, 24f, 0.55f, 0.95f), () =>
            {
                int owned = 0;
                foreach (var d in GameBalance.Decos) if (WorldTree.OwnsDeco(d.key)) owned++;
                return $"장식 보유 {owned}/{GameBalance.Decos.Count}  ·  제작 중 {WorldTree.Crafts.Count}개";
            });
            Buttons(deco, ("모든 장식 해금", () => Done($"장식 {WorldTree.DevUnlockAllDecos()}개 해금")),
                          ("제작 즉시 완료", () => Done($"제작 {WorldTree.DevFinishCrafts()}개 완료 — 공방에서 받기")));

            // 세계수
            var tree = Row(150f);
            Live(TreeUi.Line(tree, "", 32f, TreeUi.Ink, font, 24f, 24f, 0.55f, 0.95f),
                () => $"세계수 {WorldTree.Stage}/{WorldTree.MaxStage}단계  ·  나이테 Lv {WorldTree.Ring}/{WorldTree.RingMax}");
            Buttons(tree, ("단계 −1", () => WorldTree.DevSetStage(WorldTree.Stage - 1)),
                          ("단계 +1", () => WorldTree.DevSetStage(WorldTree.Stage + 1)),
                          ("나이테 최대", () => WorldTree.DevSetRing(WorldTree.RingMax)),
                          ("=입력 단계", () => { if (TryInput(out long v)) WorldTree.DevSetStage((int)Math.Min(v, int.MaxValue)); }));

            // 창고 버섯
            TreeUi.Header(list, "창고 버섯", font);
            var all = Row(150f);
            Live(TreeUi.Line(all, "", 32f, TreeUi.Ink, font, 24f, 24f, 0.55f, 0.95f), () => $"창고 합계 {WorldTree.Count(WorldTree.StoreTotal)}마리 (전체 {GameBalance.Enemies.Count}종)");
            Buttons(all, ("전부 +100", () => AddAllStore(100)), ("전부 +1000", () => AddAllStore(1000)), ("전부 0", () => SetAllStore(_ => 0)),
                         ("전부 =입력", () => { if (TryInput(out long v)) SetAllStore(_ => v); }));
            foreach (var e in GameBalance.Enemies) StoreCard(e);

            // 방치 진행
            TreeUi.Header(list, "방치 전투", font);
            var stage = Row(150f);
            Live(TreeUi.Line(stage, "", 32f, TreeUi.Ink, font, 24f, 24f, 0.55f, 0.95f), () => $"방치 스테이지 {IdleProgress.Stage}");
            Buttons(stage, ("−10", () => IdleProgress.Stage -= 10), ("−1", () => IdleProgress.Stage -= 1), ("+1", () => IdleProgress.Stage += 1),
                           ("+10", () => IdleProgress.Stage += 10), ("+100", () => IdleProgress.Stage += 100),
                           ("=입력", () => { if (TryInput(out long v)) IdleProgress.Stage = (int)Math.Min(v, int.MaxValue); }));

            var away = Row(150f);
            TreeUi.Line(away, "앱 밖에 있던 것으로 (홈 화면에서 방치 정산이 뜬다)", 30f, TreeUi.Ink, font, 24f, 24f, 0.55f, 0.95f);
            Buttons(away, ("10분", () => Away(1.0 / 6)), ("1시간", () => Away(1)), ("8시간", () => Away(8)), ("24시간", () => Away(24)));

            // 던전·광고·속도
            TreeUi.Header(list, "던전 · 광고 · 속도", font);
            var dungeon = Row(150f);
            Live(TreeUi.Line(dungeon, "", 32f, TreeUi.Ink, font, 24f, 24f, 0.55f, 0.95f),
                () => $"던전 1 남은 입장 {DungeonTickets.Remaining(1)}/{DungeonTickets.PerDay}  ·  광고 입장 {DungeonTickets.AdRemaining(1)}/{DungeonTickets.AdPerDay}");
            Buttons(dungeon, ("오늘 입장 횟수 초기화", () => { DungeonTickets.DevResetToday(); Done("던전 입장 횟수 초기화"); }));

            var ads = Row(150f);
            Live(TreeUi.Line(ads, "", 32f, TreeUi.Ink, font, 24f, 24f, 0.55f, 0.95f), () => $"광고 건너뛰기: {(SkipAds ? "켬 (광고 없이 바로 보상)" : "끔")}");
            Buttons(ads, ("켜기", () => SetSkipAds(true)), ("끄기", () => SetSkipAds(false)));

            var speed = Row(150f);
            Live(TreeUi.Line(speed, "", 32f, TreeUi.Ink, font, 24f, 24f, 0.55f, 0.95f), () => $"게임 속도 ×{Time.timeScale:0.##}  (홈으로 오면 ×1로 돌아간다)");
            Buttons(speed, ("×1", () => Time.timeScale = 1f), ("×2", () => Time.timeScale = 2f), ("×5", () => Time.timeScale = 5f), ("×10", () => Time.timeScale = 10f));
        }

        void CurrencyCard(Currency currency, string label, (string, Func<long, long>)[] ops)
        {
            var card = Row(150f);
            Live(TreeUi.Line(card, "", 32f, TreeUi.Ink, font, 24f, 24f, 0.55f, 0.95f),
                () => $"{label} {Wallet.Format(Wallet.Get(currency))}  <size=24>({Wallet.Get(currency).ToString("#,0", CultureInfo.InvariantCulture)})</size>");
            var buttons = new List<(string, Action)>();
            foreach (var (name, op) in ops) buttons.Add((name, () => Wallet.DevSet(currency, op(Wallet.Get(currency)))));
            buttons.Add(("=입력", () => { if (TryInput(out long v)) Wallet.DevSet(currency, v); }));
            Buttons(card, buttons.ToArray());
        }

        void StoreCard(EnemyRow e)
        {
            var card = Row(130f);
            var icon = TreeUi.Icon(card, WorldTree.EnemySprite(e.index), 96f, 16f);
            icon.preserveAspect = true;
            Live(TreeUi.Line(card, "", 28f, TreeUi.Ink, font, 124f, 24f, 0.55f, 0.95f),
                () => $"{e.name} <size=22>#{e.index}</size>  보유 {WorldTree.Count(WorldTree.StoreCount(e.index))}");
            Buttons(card, 124f, ("+10", () => AddStore(e.index, 10)), ("+100", () => AddStore(e.index, 100)), ("+1000", () => AddStore(e.index, 1000)),
                                ("0", () => WorldTree.DevSetStore(e.index, 0)), ("=입력", () => { if (TryInput(out long v)) WorldTree.DevSetStore(e.index, v); }));
        }

        // ── 동작 ──

        static long Mul(long v, long m) => v > long.MaxValue / m ? long.MaxValue : v * m;

        static void AddStore(int enemy, long n)
        {
            long have = WorldTree.StoreCount(enemy);
            WorldTree.DevSetStore(enemy, have > long.MaxValue - n ? long.MaxValue : have + n);
        }

        static void AddAllStore(long n) { foreach (var e in GameBalance.Enemies) AddStore(e.index, n); }

        static void SetAllStore(Func<long, long> value) { foreach (var e in GameBalance.Enemies) WorldTree.DevSetStore(e.index, value(WorldTree.StoreCount(e.index))); }

        void SetSkipAds(bool on)
        {
            PlayerPrefs.SetInt(SkipAdsKey, on ? 1 : 0);
            PlayerPrefs.Save();
            Done(on ? "광고 건너뛰기 켬" : "광고 건너뛰기 끔");
        }

        // 마지막 접속을 앞당기고, 방치 전투가 있으면(홈 씬) 앱으로 돌아온 것처럼 정산을 돌린다
        void Away(double hours)
        {
            IdleAway.DevBackdate(hours);
            var battle = FindAnyObjectByType<IdleBattle>();
            if (battle == null) { Done("방치 전투가 없는 씬 — 홈으로 가면 정산된다"); return; }
            root.SetActive(false);
            battle.SendMessage("OnApplicationPause", false, SendMessageOptions.DontRequireReceiver);
        }

        void Done(string message)
        {
            if (status != null) status.text = message;
            liveIn = 0f;
        }

        // 입력칸 숫자. 1234 / 1,234 / 1.5c (방치형 단위 a=천, b=백만 …, aa 이후 두 글자) / 1e12
        bool TryInput(out long value)
        {
            value = 0;
            string s = input != null ? input.text.Trim().Replace(",", "").Replace(" ", "") : "";
            if (s.Length == 0) { Done("입력칸에 숫자를 넣어 주세요"); return false; }
            int cut = s.Length;
            while (cut > 0 && char.IsLetter(s[cut - 1]) && s[cut - 1] != 'e' && s[cut - 1] != 'E') cut--;
            string unit = s.Substring(cut).ToLowerInvariant();
            if (!double.TryParse(s.Substring(0, cut), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || v < 0)
            {
                Done($"숫자를 읽지 못했어요: {input.text}");
                return false;
            }
            int power = unit.Length == 0 ? 0 : unit.Length == 1 ? unit[0] - 'a' + 1 : unit.Length == 2 ? 27 + (unit[0] - 'a') * 26 + (unit[1] - 'a') : -1;
            if (power < 0) { Done($"단위를 읽지 못했어요: {unit}"); return false; }
            double total = v * Math.Pow(1000, power);
            value = total >= 9e18 ? long.MaxValue : (long)total;
            Done($"입력값 {value.ToString("#,0", CultureInfo.InvariantCulture)} 적용");
            return true;
        }

        // ── UI 조각 ──

        RectTransform Row(float height) => TreeUi.Card(list, height);

        void Live(TMP_Text text, Func<string> value)
        {
            text.richText = true;
            live.Add((text, value));
        }

        void Buttons(RectTransform card, params (string label, Action action)[] buttons) => Buttons(card, 24f, buttons);

        // 카드 아래쪽에 왼쪽부터 버튼을 놓는다. 누르면 값 글자를 바로 고친다
        void Buttons(RectTransform card, float left, params (string label, Action action)[] buttons)
        {
            float x = left;
            foreach (var (label, action) in buttons)
            {
                float w = Mathf.Max(110f, 26f * label.Length + 40f);
                var b = SmallButton(card, label, () =>
                {
                    try { action(); }
                    catch (Exception e) { Debug.LogException(e); Done("실패: " + e.Message); }
                    liveIn = 0f;
                }, TreeUi.ButtonOn);
                var rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(x, 12f);
                rt.sizeDelta = new Vector2(w, 64f);
                x += w + 10f;
            }
        }

        Button SmallButton(Transform parent, string label, Action onClick, Color tint)
        {
            var b = TreeUi.Button(parent, label, true, onClick, font, tint: tint);
            var t = b.GetComponentInChildren<TMP_Text>();
            t.fontSize = 26f;
            return b;
        }

        TMP_InputField MakeInput(RectTransform parent)
        {
            var bg = parent.gameObject.AddComponent<Image>();
            bg.color = Color.white;
            var field = parent.gameObject.AddComponent<TMP_InputField>();
            var area = TreeUi.New("TextArea", parent);
            TreeUi.Place(area, Vector2.zero, Vector2.one, new Vector2(20f, 6f), new Vector2(-20f, -6f));
            area.gameObject.AddComponent<RectMask2D>();
            var placeholder = TreeUi.Text(area, "숫자 입력 (예: 5000, 1.5c, 1e12) → 각 줄의 \"=입력\"", 30f, TreeUi.Faint, font);
            TreeUi.Stretch(placeholder.rectTransform);
            placeholder.textWrappingMode = TextWrappingModes.NoWrap;
            var text = TreeUi.Text(area, "", 34f, TreeUi.Ink, font);
            TreeUi.Stretch(text.rectTransform);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.richText = false;
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.fontAsset = font;
            field.pointSize = 34f;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.targetGraphic = bg;
            return field;
        }

        // 한글 글꼴은 씬에 있는 글자에서 가져온다 (TMP 기본 글꼴은 한글이 없다. PowerSaveMode·AdBox와 같은 방식)
        TMP_FontAsset BorrowFont()
        {
            foreach (var t in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (!t.transform.IsChildOf(transform) && t.font != null) return t.font;
            return TMP_Settings.defaultFontAsset;
        }
    }
}
