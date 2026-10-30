using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 방치 정산 팝업: 홈으로 돌아오면 비운 동안 번 골드·균사석·처치 수·넘긴 스테이지·앱 밖 보상을 보여 준다.
    // 다른 UI 위에 오도록 자기 오버레이 캔버스를 코드로 만든다 (씬에 둘 것 없음). 확인을 누르면 닫힌다
    public class IdleReportPopup : MonoBehaviour
    {
        const int SortingOrder = 30000;

        TMP_Text body;
        TMP_FontAsset font;

        public static IdleReportPopup Create(TMP_FontAsset font)
        {
            var go = new GameObject("IdleReportPopup", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var popup = go.AddComponent<IdleReportPopup>();
            popup.font = font;
            popup.Build();
            go.SetActive(false);
            return popup;
        }

        public void Show(IdleAway.Report r)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("자리를 비운 동안 ").Append(FormatTime(r.seconds + r.offlineSeconds)).Append("\n\n");
            sb.Append("<color=#FFD54A>골드 +").Append(Wallet.Format(r.gold + r.offlineGold)).Append("</color>\n");
            if (r.offlineGold > 0) sb.Append("<size=80%>(앱 밖 ").Append(FormatTime(r.offlineSeconds)).Append(" +").Append(Wallet.Format(r.offlineGold)).Append(")</size>\n");
            if (r.mycelite > 0) sb.Append("<color=#8FE3FF>균사석 +").Append(Wallet.Format(r.mycelite)).Append("</color>\n");
            if (r.kills > 0) sb.Append("처치 ").Append(r.kills.ToString("N0")).Append("마리\n");
            if (r.stages > 0) sb.Append("스테이지 +").Append(r.stages).Append(" → ").Append(IdleProgress.Stage);
            body.text = sb.ToString().TrimEnd('\n');
            gameObject.SetActive(true);
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

            var ok = NewImage("Ok", panel.transform, new Color(0.45f, 0.75f, 0.35f));
            var okSprite = Resources.Load<Sprite>("Art/Dream/UI/btn_go");
            if (okSprite != null) { ok.sprite = okSprite; ok.type = Image.Type.Sliced; ok.color = Color.white; }
            ok.raycastTarget = true;
            var okRt = ok.rectTransform;
            okRt.anchorMin = okRt.anchorMax = new Vector2(0.5f, 0f);
            okRt.pivot = new Vector2(0.5f, 0f);
            okRt.anchoredPosition = new Vector2(0f, 50f);
            okRt.sizeDelta = new Vector2(360f, 130f);
            ok.gameObject.AddComponent<Button>().onClick.AddListener(() => gameObject.SetActive(false));
            var okText = NewText("Text", ok.transform, 52f);
            okText.text = "확인";
            Stretch(okText.rectTransform);
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
