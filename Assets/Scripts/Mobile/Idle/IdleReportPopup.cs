using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 방치 정산 팝업: 홈으로 돌아오면 비운 동안 번 골드·균사석·처치 수·넘긴 스테이지·앱 밖 보상을 보여 준다.
    // 다른 UI 위에 오도록 자기 오버레이 캔버스를 코드로 만든다 (씬에 둘 것 없음).
    // 왼쪽 "확인"은 닫는다. 오른쪽 "광고 보고 2배 보상"은 리워드 광고를 끝까지 보면 방금 받은 골드·균사석을 한 번 더 준다 (이미 지갑에 들어 있으므로 같은 양을 더 넣는다)
    public class IdleReportPopup : MonoBehaviour
    {
        const int SortingOrder = 30000;

        TMP_Text body, title;
        TMP_FontAsset font;
        Button doubleButton;
        IdleAway.Report report;     // 지금 보이는 정산
        bool doubled;               // 2배 보상을 이미 받았다

        public static IdleReportPopup Create(TMP_FontAsset font)
        {
            var go = new GameObject("IdleReportPopup", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(StageFrame.MinWidth, StageFrame.DesignHeight);   // 씬 캔버스와 같은 기준 (크기가 화면마다 같게)
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var popup = go.AddComponent<IdleReportPopup>();
            popup.font = font;
            // [UI 개편 2026-11-05] 화면은 프리팹(규격 틀·노란 탭·속지)으로. 없으면 예전처럼 코드로 만든다 (UI_REWORK_LOG.md)
            var prefab = Resources.Load<IdleReportView>(PrefabPath);
            if (prefab != null) popup.Bind(Instantiate(prefab, go.transform, false));
            else popup.Build();
            go.SetActive(false);
            return popup;
        }

        public void Show(IdleAway.Report r)
        {
            SetTitle("방치 정산");
            SetMessageMode(false);
            report = r;
            doubled = false;
            Render();
            if (doubleButton != null)
            {
                doubleButton.interactable = true;
                doubleButton.gameObject.SetActive(r.gold + r.offlineGold + r.mycelite > 0);
            }
            AdService.Initialize();
            AdService.Prepare();            // 누를 때 광고가 준비돼 있게
            PopupGate.Opened(this, Hide);
            gameObject.SetActive(true);
        }

        // 알림만 보여 준다 (방치 화면 던전 결과 등): 제목·본문 + 확인. 2배 버튼은 숨긴다
        public bool IsOpen => gameObject.activeSelf;
        string pendingTitle, pendingBody;   // 정산 창이 열려 있을 때 온 알림 — 닫은 뒤 보여 준다

        public void ShowMessage(string heading, string message)
        {
            if (IsOpen) { pendingTitle = heading; pendingBody = message; return; }
            report = null;
            SetTitle(heading);
            SetMessageMode(true);
            var text = view != null ? messageText : body;
            if (text != null) text.text = message;
            if (doubleButton != null) doubleButton.gameObject.SetActive(false);
            PopupGate.Opened(this, Hide);
            gameObject.SetActive(true);
        }

        void SetTitle(string heading)
        {
            if (title != null) title.text = heading;
        }

        // 프리팹 화면에서 알림일 때: 정산 줄(List)·시간 줄을 숨기고 그 자리에 여러 줄 본문을 보인다
        void SetMessageMode(bool on)
        {
            if (view == null || list == null) return;
            list.SetActive(!on);
            if (view.timeText != null) view.timeText.gameObject.SetActive(!on);
            if (on && messageText == null) messageText = CreateMessageText();
            if (messageText != null) messageText.gameObject.SetActive(on);
        }

        TMP_Text CreateMessageText()
        {
            var src = (RectTransform)list.transform;
            var t = NewText("Message", src.parent, 40f);
            var rt = t.rectTransform;
            rt.anchorMin = src.anchorMin; rt.anchorMax = src.anchorMax; rt.pivot = src.pivot;
            rt.anchoredPosition = src.anchoredPosition; rt.sizeDelta = src.sizeDelta;
            rt.SetSiblingIndex(src.GetSiblingIndex() + 1);
            if (view.timeText != null)
            {
                t.font = view.timeText.font;
                t.fontSharedMaterial = view.timeText.fontSharedMaterial;
                t.color = view.timeText.color;
            }
            t.enableAutoSizing = true;
            t.fontSizeMin = 24f;
            t.fontSizeMax = 40f;
            t.lineSpacing = 12f;
            return t;
        }

        const string PrefabPath = "IdleReportPopup";   // Assets/Prefabs/Mobile/UI/Main/Resources/IdleReportPopup.prefab
        IdleReportView view;
        GameObject list;            // 프리팹의 정산 줄 묶음
        TMP_Text messageText;       // 프리팹 화면에서 알림 본문 (처음 알림 때 만든다)

        void Bind(IdleReportView v)
        {
            view = v;
            title = v.transform.Find("Frame/Tab/Label")?.GetComponent<TMP_Text>();   // 제목 (알림 때 바꾼다)
            list = v.transform.Find("Content/List")?.gameObject;
            var rt = (RectTransform)v.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            if (v.okButton != null) v.okButton.onClick.AddListener(Hide);
            doubleButton = v.doubleButton;
            if (doubleButton != null)
            {
                UiSkin.GreyWhenDisabled(doubleButton);
                doubleButton.onClick.AddListener(WatchForDouble);
            }
        }

        void RenderView()
        {
            var r = report;
            long m = doubled ? 2 : 1;
            if (view.timeText != null) view.timeText.text = "자리를 비운 동안 " + FormatTime(r.seconds + r.offlineSeconds);
            if (view.goldText != null) view.goldText.text = "+" + Wallet.Format(Times(IdleProgress.SaturatingAdd(r.gold, r.offlineGold), m));
            if (view.goldSubText != null)
            {
                view.goldSubText.gameObject.SetActive(r.offlineGold > 0);
                view.goldSubText.text = $"(앱 밖 {FormatTime(r.offlineSeconds)} +{Wallet.Format(Times(r.offlineGold, m))})";
            }
            if (view.myceliteRow != null) view.myceliteRow.SetActive(r.mycelite > 0);
            if (view.myceliteText != null) view.myceliteText.text = "+" + Wallet.Format(Times(r.mycelite, m));
            var info = new System.Collections.Generic.List<string>();
            if (r.kills > 0) info.Add($"처치 {r.kills:N0}마리");
            if (r.stages > 0) info.Add($"스테이지 +{r.stages} → {IdleBattle.HudStageLabel(IdleProgress.Stage).Replace("스테이지 ", "")}");   // HUD와 같은 "7-7" 표기
            if (view.infoRow != null) view.infoRow.SetActive(info.Count > 0);
            if (view.infoText != null) view.infoText.text = string.Join("  ·  ", info);
            if (view.doubledRow != null) view.doubledRow.SetActive(doubled);
        }

        void Render()
        {
            if (view != null) { RenderView(); return; }
            var r = report;
            long m = doubled ? 2 : 1;
            var sb = new System.Text.StringBuilder();
            sb.Append("자리를 비운 동안 ").Append(FormatTime(r.seconds + r.offlineSeconds)).Append("\n\n");
            sb.Append("<color=#FFD54A>골드 +").Append(Wallet.Format(Times(IdleProgress.SaturatingAdd(r.gold, r.offlineGold), m))).Append("</color>\n");
            if (r.offlineGold > 0) sb.Append("<size=80%>(앱 밖 ").Append(FormatTime(r.offlineSeconds)).Append(" +").Append(Wallet.Format(Times(r.offlineGold, m))).Append(")</size>\n");
            if (r.mycelite > 0) sb.Append("<color=#8FE3FF>균사석 +").Append(Wallet.Format(Times(r.mycelite, m))).Append("</color>\n");
            if (r.kills > 0) sb.Append("처치 ").Append(r.kills.ToString("N0")).Append("마리\n");
            if (r.stages > 0) sb.Append("스테이지 +").Append(r.stages).Append(" → ").Append(IdleProgress.Stage).Append('\n');
            if (doubled) sb.Append("<color=#7FD37F>광고 보상 2배 적용!</color>");
            body.text = sb.ToString().TrimEnd('\n');
        }

        // 광고를 끝까지 보면 같은 양을 한 번 더 지갑에 넣고 화면도 2배로 바꾼다 (한 번만)
        void WatchForDouble()
        {
            if (doubled || report == null) return;
            doubleButton.interactable = false;      // 광고가 뜨기 전에 또 누르지 않게
            var watched = report;
            bool given = false;
            bool started = AdService.ShowRewarded(AdPlacement.OfflineDouble, () =>
            {
                if (given) return;
                given = true;
                // 보상부터 넣는다. 광고를 보는 사이 씬이 바뀌어 이 창이 사라졌어도 보상은 받는다
                Wallet.Add(Currency.Gold, IdleProgress.SaturatingAdd(watched.gold, watched.offlineGold));
                Wallet.Add(Currency.Mycelite, watched.mycelite);
                if (this == null || report != watched) return;
                Hide();     // 광고를 다 보고 보상을 받았으면 정산 창도 같이 닫는다
            });
            if (!started) doubleButton.interactable = true;
        }

        void Hide()
        {
            PopupGate.Closed(this);
            report = null;
            gameObject.SetActive(false);
            if (pendingBody == null) return;
            string t = pendingTitle, b = pendingBody;
            pendingTitle = pendingBody = null;
            ShowMessage(t, b);
        }

        // 표시용 곱하기 (아주 높은 스테이지에서 long이 넘쳐 음수로 보이지 않게)
        static long Times(long v, long m) => m == 2 ? IdleProgress.SaturatingAdd(v, v) : v;

        static string FormatTime(double seconds)
        {
            int s = Mathf.Max(0, (int)seconds);
            if (s >= 3600) return $"{s / 3600}시간 {s % 3600 / 60}분";
            if (s >= 60) return $"{s / 60}분 {s % 60}초";
            return $"{s}초";
        }

        void Build()
        {
            var dim = NewImage("Dim", transform, new Color(0f, 0f, 0f, 0.6f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = true;                       // 뒤를 누르지 못하게 막는다

            var panel = NewImage("Panel", transform, new Color(0.98f, 0.94f, 0.86f));
            var sprite = Resources.Load<Sprite>("UI/UI_PP_Out_Paper2");
            if (sprite != null) { panel.sprite = sprite; panel.type = Image.Type.Sliced; panel.color = Color.white; }
            panel.rectTransform.sizeDelta = new Vector2(820f, 760f);

            title = NewText("Title", panel.transform, 64f);
            title.text = "방치 정산";
            title.color = new Color(0.36f, 0.24f, 0.14f);
            Place(title.rectTransform, new Vector2(0f, -60f), new Vector2(760f, 90f));

            body = NewText("Body", panel.transform, 44f);
            body.color = new Color(0.3f, 0.22f, 0.15f);
            body.lineSpacing = 12f;
            Place(body.rectTransform, new Vector2(0f, -170f), new Vector2(740f, 420f));
            body.alignment = TextAlignmentOptions.Top;

            // 왼쪽 = 확인(초록), 오른쪽 = 광고 보고 2배 보상(청록)
            NewButton("Ok", panel.transform, "확인", 52f, "UI/UI_Btn_Green", -190f).onClick.AddListener(Hide);
            doubleButton = NewButton("DoubleAd", panel.transform, "광고 보고\n2배 보상", 40f, "UI/UI_Btn_Sky", 190f);   // 색은 그림으로 (물들이지 않는다)
            doubleButton.onClick.AddListener(WatchForDouble);
        }

        Button NewButton(string name, Transform parent, string label, float size, string spritePath, float x)
        {
            var img = NewImage(name, parent, new Color(0.45f, 0.75f, 0.35f));
            var sprite = Resources.Load<Sprite>(spritePath);
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; img.color = Color.white; }
            img.raycastTarget = true;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(x, 50f);
            rt.sizeDelta = new Vector2(350f, 130f);
            var button = img.gameObject.AddComponent<Button>();
            UiSkin.GreyWhenDisabled(button);   // [UI 개편] 누를 수 없으면 회색 버튼 그림
            button.targetGraphic = img;
            var text = NewText("Text", img.transform, size);
            text.text = label;
            text.color = Color.white;
            OutlineText.ApplyIfWhite(text);   // [UI 개편 11-05] 흰 글자는 외곽선 (프리팹이 없을 때의 예전 화면)
            Stretch(text.rectTransform);
            return button;
        }

        Image NewImage(string name, Transform parent, Color color)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        TMP_Text NewText(string name, Transform parent, float size)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
