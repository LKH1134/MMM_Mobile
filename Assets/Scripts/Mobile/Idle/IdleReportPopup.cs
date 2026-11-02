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

        TMP_Text body;
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
            popup.Build();
            go.SetActive(false);
            return popup;
        }

        public void Show(IdleAway.Report r)
        {
            report = r;
            doubled = false;
            Render();
            doubleButton.interactable = true;
            doubleButton.gameObject.SetActive(r.gold + r.offlineGold + r.mycelite > 0);
            AdService.Initialize();
            AdService.Prepare();            // 누를 때 광고가 준비돼 있게
            PopupGate.Opened(this, Hide);
            gameObject.SetActive(true);
        }

        void Render()
        {
            var r = report;
            long m = doubled ? 2 : 1;
            var sb = new System.Text.StringBuilder();
            sb.Append("자리를 비운 동안 ").Append(FormatTime(r.seconds + r.offlineSeconds)).Append("\n\n");
            sb.Append("<color=#FFD54A>골드 +").Append(Wallet.Format((r.gold + r.offlineGold) * m)).Append("</color>\n");
            if (r.offlineGold > 0) sb.Append("<size=80%>(앱 밖 ").Append(FormatTime(r.offlineSeconds)).Append(" +").Append(Wallet.Format(r.offlineGold * m)).Append(")</size>\n");
            if (r.mycelite > 0) sb.Append("<color=#8FE3FF>균사석 +").Append(Wallet.Format(r.mycelite * m)).Append("</color>\n");
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
            bool started = AdService.ShowRewarded(() =>
            {
                if (doubled || report == null) return;
                doubled = true;
                Wallet.Add(Currency.Gold, report.gold + report.offlineGold);
                Wallet.Add(Currency.Mycelite, report.mycelite);
                Render();
                doubleButton.gameObject.SetActive(false);
            });
            if (!started) doubleButton.interactable = true;
        }

        void Hide()
        {
            PopupGate.Closed(this);
            report = null;
            gameObject.SetActive(false);
        }

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
            var sprite = Resources.Load<Sprite>("Art/Dream/UI/panel_paper");
            if (sprite != null) { panel.sprite = sprite; panel.type = Image.Type.Sliced; panel.color = Color.white; }
            panel.rectTransform.sizeDelta = new Vector2(820f, 760f);

            var title = NewText("Title", panel.transform, 64f);
            title.text = "방치 정산";
            title.color = new Color(0.36f, 0.24f, 0.14f);
            Place(title.rectTransform, new Vector2(0f, -60f), new Vector2(760f, 90f));

            body = NewText("Body", panel.transform, 44f);
            body.color = new Color(0.3f, 0.22f, 0.15f);
            body.lineSpacing = 12f;
            Place(body.rectTransform, new Vector2(0f, -170f), new Vector2(740f, 420f));
            body.alignment = TextAlignmentOptions.Top;

            // 왼쪽 = 확인(초록), 오른쪽 = 광고 보고 2배 보상(청록)
            NewButton("Ok", panel.transform, "확인", 52f, Color.white, -190f).onClick.AddListener(Hide);
            doubleButton = NewButton("DoubleAd", panel.transform, "광고 보고\n2배 보상", 40f, new Color(0.45f, 0.85f, 0.8f, 1f), 190f);
            doubleButton.onClick.AddListener(WatchForDouble);
        }

        Button NewButton(string name, Transform parent, string label, float size, Color tint, float x)
        {
            var img = NewImage(name, parent, new Color(0.45f, 0.75f, 0.35f));
            var sprite = Resources.Load<Sprite>("Art/Dream/UI/btn_go");
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; img.color = tint; }
            img.raycastTarget = true;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(x, 50f);
            rt.sizeDelta = new Vector2(350f, 130f);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var text = NewText("Text", img.transform, size);
            text.text = label;
            text.color = Color.white;
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
