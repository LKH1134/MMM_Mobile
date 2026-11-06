using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 광고 상자를 누르면 뜨는 "광고 보기" 창 (골드·균사석 보상 안내 + 취소 / 광고 보기).
    // 코드로 만들어 AdBox의 캔버스 아래에 둔다. 자기 캔버스로 다른 UI 위에 올라와 뒤의 버튼을 막는다
    public class AdBoxPopup
    {
        const int SortingOrder = 10100;     // PopupLayer.Popup(Main 캔버스 10000 + 100)과 같은 층. 한 번에 하나만 뜨게 PopupGate에 알린다
        const string FrameSprite = "UI/UI_PP_Out_Wood";
        const string PaperSprite = "UI/UI_PP_Out_Paper1";
        const string AdIcon = "UI/Icon/advertisement";     // 상자 그림 대신 광고 아이콘 (없으면 상자)
        const string TitleSprite = "UI/UI_PP_Under_Tag_Yellow";
        const string CancelSprite = "UI/UI_Btn_Red";     // 색은 그림으로 정한다 (코드에서 물들이지 않는다)
        const string WatchSprite = "UI/UI_Btn_Sky";
        const string GoldIcon = "UI/Icon/gold";
        const string MyceliteIcon = "UI/Icon/mycelium_stone";
        static readonly Color Ink = new Color(0.30f, 0.20f, 0.10f, 1f);

        readonly GameObject root;
        readonly TMP_Text goldText, myceliteText, statusText;
        readonly Button watchButton;
        Action onWatch, onClose, onCancel;

        public bool IsOpen => root != null && root.activeSelf;

        const string PrefabPath = "AdBoxPopup";   // Prefabs/Mobile/UI/Main/Resources/AdBoxPopup.prefab — 모양은 프리팹에서 고친다

        public AdBoxPopup(Transform parent, Sprite chest, TMP_FontAsset font)
        {
            // [UI 개편 2026-11-05] 프리팹이 있으면 그것을 띄운다 (없으면 아래 코드로 만든다)
            var prefab = Resources.Load<AdBoxView>(PrefabPath);
            if (prefab != null)
            {
                var view = UnityEngine.Object.Instantiate(prefab, parent, false);
                root = view.gameObject;
                Stretch((RectTransform)root.transform);
                if (!root.TryGetComponent<Canvas>(out var c)) c = root.AddComponent<Canvas>();
                c.overrideSorting = true;
                c.sortingOrder = SortingOrder;
                if (root.GetComponent<GraphicRaycaster>() == null) root.AddComponent<GraphicRaycaster>();
                goldText = view.goldText;
                myceliteText = view.myceliteText;
                statusText = view.statusText;
                watchButton = view.watchButton;
                if (watchButton != null) { UiSkin.GreyWhenDisabled(watchButton); watchButton.onClick.AddListener(() => onWatch?.Invoke()); }
                if (view.cancelButton != null) view.cancelButton.onClick.AddListener(Cancel);
                root.SetActive(false);
                return;
            }

            root = new GameObject("AdPopup", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            Stretch((RectTransform)root.transform);
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);      // 뒤를 어둡게 하고 누름을 막는다
            var canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            root.AddComponent<GraphicRaycaster>();

            // [UI 개편 2026-11-05] 다른 팝업과 같은 틀: 갈색 우드 틀 + 위로 튀어나온 노란 제목 탭 + 속지 (UiSkin 규격)
            var panel = NewImage("Frame", root.transform, Resources.Load<Sprite>(FrameSprite), Color.white, new Vector2(880f, 1060f), Vector2.zero);
            panel.type = Image.Type.Sliced;
            var title = NewImage("Tab", panel.transform, Resources.Load<Sprite>(TitleSprite), Color.white, Vector2.zero, Vector2.zero);
            title.type = Image.Type.Sliced;
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0.2f, 1f); trt.anchorMax = new Vector2(0.8f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(0f, -130f); trt.offsetMax = new Vector2(0f, 40f);
            var titleText = NewText("Label", title.transform, "광고 보기", 54f, Ink, font, new Vector2(400f, 140f), Vector2.zero);
            titleText.fontStyle = FontStyles.Bold;
            var ttr = titleText.rectTransform; ttr.anchorMin = ttr.anchorMax = new Vector2(0.5f, 1f); ttr.anchoredPosition = new Vector2(0f, -76f);
            var paper = NewImage("Paper", panel.transform, Resources.Load<Sprite>(PaperSprite), Color.white, Vector2.zero, Vector2.zero);
            paper.type = Image.Type.Sliced;
            var prt = paper.rectTransform; prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = new Vector2(34f, 50f); prt.offsetMax = new Vector2(-34f, -112f);
            var body = paper.transform;   // 속지 가운데 기준 (속지 높이 898)

            NewText("Desc", body, "광고를 보고 골드와 균사석을 받으세요!", 40f, Ink, font, new Vector2(740f, 110f), new Vector2(0f, 340f)).fontStyle = FontStyles.Bold;
            var icon = NewImage("AdIcon", body, Resources.Load<Sprite>(AdIcon) ?? chest, Color.white, new Vector2(300f, 300f), new Vector2(0f, 130f));
            icon.preserveAspect = true;

            // 골드 · 균사석: 글자 길이와 상관없이 가운데에 모은다
            var rewards = new GameObject("Rewards", typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
            rewards.SetParent(body, false);
            rewards.anchorMin = rewards.anchorMax = new Vector2(0.5f, 0.5f);
            rewards.sizeDelta = new Vector2(760f, 100f);
            rewards.anchoredPosition = new Vector2(0f, -110f);
            var rl = rewards.GetComponent<HorizontalLayoutGroup>();
            rl.childAlignment = TextAnchor.MiddleCenter; rl.spacing = 56f;
            rl.childControlWidth = rl.childControlHeight = false; rl.childForceExpandWidth = rl.childForceExpandHeight = false;
            goldText = RewardPair("Gold", rewards, Resources.Load<Sprite>(GoldIcon), font);
            myceliteText = RewardPair("Mycelite", rewards, Resources.Load<Sprite>(MyceliteIcon), font);
            statusText = NewText("Status", body, "", 32f, new Color(Ink.r, Ink.g, Ink.b, 0.8f), font, new Vector2(740f, 70f), new Vector2(0f, -210f));

            var cancel = NewButton("Cancel", body, "취소", CancelSprite, font, new Vector2(-190f, -340f));
            cancel.onClick.AddListener(Cancel);
            watchButton = NewButton("Watch", body, "광고 보기", WatchSprite, font, new Vector2(190f, -340f));
            watchButton.onClick.AddListener(() => onWatch?.Invoke());
            root.SetActive(false);
        }

        // gold·mycelite = 받을 양. watch = "광고 보기"를 눌렀을 때, close = 창이 닫힐 때(취소·뒤로가기·보상 뒤),
        // cancel = "취소" 버튼을 눌렀을 때만 (닫히기 전에 불린다 — 상자를 치우는 데 쓴다)
        public void Open(long gold, long mycelite, Action watch, Action close, Action cancel = null)
        {
            onWatch = watch;
            onClose = close;
            onCancel = cancel;
            if (goldText != null) goldText.text = "+" + Wallet.Format(gold);
            if (myceliteText != null) myceliteText.text = "+" + Wallet.Format(mycelite);
            if (statusText != null) statusText.text = "";
            if (watchButton != null) watchButton.interactable = true;
            root.SetActive(true);
        }

        public void SetWatchEnabled(bool enabled)
        {
            if (watchButton != null) watchButton.interactable = enabled;
        }

        public void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        public void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            var close = onClose;
            onClose = onWatch = onCancel = null;
            close?.Invoke();
        }

        void Cancel()
        {
            var cancel = onCancel;
            Close();
            cancel?.Invoke();
        }

        // ── UI 조각 ──
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

        static TMP_Text NewText(string name, Transform parent, string text, float size, Color color, TMP_FontAsset font, Vector2 box, Vector2 pos)
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
            t.raycastTarget = false;
            if (font != null) t.font = font;
            OutlineText.ApplyIfWhite(t);   // [UI 개편 11-05] 흰 글자는 외곽선
            return t;
        }

        // 아이콘 + 값 한 쌍 (가로 묶음, 값 길이만큼 폭)
        static TMP_Text RewardPair(string name, Transform parent, Sprite sprite, TMP_FontAsset font)
        {
            var pair = new GameObject(name + "Pair", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            pair.SetParent(parent, false);
            pair.sizeDelta = new Vector2(0f, 100f);
            var hl = pair.GetComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleCenter; hl.spacing = 14f;
            hl.childControlWidth = hl.childControlHeight = false; hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            pair.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var icon = NewImage(name + "Icon", pair, sprite, Color.white, new Vector2(90f, 90f), Vector2.zero);
            icon.preserveAspect = true;
            var t = NewText(name, pair, "", 54f, Ink, font, new Vector2(100f, 90f), Vector2.zero);
            t.fontStyle = FontStyles.Bold;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            return t;
        }

        static Button NewButton(string name, Transform parent, string label, string sprite, TMP_FontAsset font, Vector2 pos)
        {
            var img = NewImage(name, parent, Resources.Load<Sprite>(sprite), Color.white, new Vector2(340f, 150f), pos);
            img.type = Image.Type.Sliced;
            img.raycastTarget = true;
            NewText("Label", img.transform, label, 48f, Color.white, font, new Vector2(300f, 100f), Vector2.zero);
            var button = img.gameObject.AddComponent<Button>();
            UiSkin.GreyWhenDisabled(button);   // [UI 개편] 누를 수 없으면 회색 버튼 그림
            button.targetGraphic = img;
            return button;
        }
    }
}
