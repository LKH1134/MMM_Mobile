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
        const string PanelSprite = "Art/Dream/UI/panel_paper";
        const string TitleSprite = "Art/Dream/UI/window_title";
        const string ButtonSprite = "Art/Dream/UI/btn_go";
        const string GoldIcon = "Art/Dream/Icons/gold";
        const string MyceliteIcon = "Art/Dream/Icons/gem";
        static readonly Color Ink = new Color(0.30f, 0.20f, 0.10f, 1f);

        readonly GameObject root;
        readonly TMP_Text goldText, myceliteText, statusText;
        readonly Button watchButton;
        Action onWatch, onClose;

        public bool IsOpen => root != null && root.activeSelf;

        public AdBoxPopup(Transform parent, Sprite chest, TMP_FontAsset font)
        {
            root = new GameObject("AdPopup", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            Stretch((RectTransform)root.transform);
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);      // 뒤를 어둡게 하고 누름을 막는다
            var canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            root.AddComponent<GraphicRaycaster>();

            var panel = NewImage("Panel", root.transform, Resources.Load<Sprite>(PanelSprite), Color.white, new Vector2(860f, 1040f), Vector2.zero);
            panel.type = Image.Type.Sliced;
            var title = NewImage("Title", panel.transform, Resources.Load<Sprite>(TitleSprite), new Color(0.75f, 0.5f, 0.9f, 1f), new Vector2(560f, 120f), new Vector2(0f, 460f));
            title.type = Image.Type.Sliced;
            NewText("TitleText", title.transform, "광고 보기", 54f, Color.white, font, new Vector2(520f, 100f), Vector2.zero);
            NewText("Desc", panel.transform, "광고를 보고 골드와 균사석을 받으세요!", 44f, Ink, font, new Vector2(740f, 130f), new Vector2(0f, 330f));
            var icon = NewImage("Chest", panel.transform, chest, Color.white, new Vector2(340f, 340f), new Vector2(0f, 90f));
            icon.preserveAspect = true;

            NewImage("GoldIcon", panel.transform, Resources.Load<Sprite>(GoldIcon), Color.white, new Vector2(96f, 96f), new Vector2(-250f, -160f)).preserveAspect = true;
            goldText = NewText("Gold", panel.transform, "", 56f, Ink, font, new Vector2(260f, 100f), new Vector2(-90f, -160f));
            NewImage("MyceliteIcon", panel.transform, Resources.Load<Sprite>(MyceliteIcon), Color.white, new Vector2(96f, 96f), new Vector2(110f, -160f)).preserveAspect = true;
            myceliteText = NewText("Mycelite", panel.transform, "", 56f, Ink, font, new Vector2(260f, 100f), new Vector2(270f, -160f));
            statusText = NewText("Status", panel.transform, "", 34f, new Color(Ink.r, Ink.g, Ink.b, 0.8f), font, new Vector2(760f, 80f), new Vector2(0f, -270f));

            var cancel = NewButton("Cancel", panel.transform, "취소", new Color(0.9f, 0.35f, 0.45f, 1f), font, new Vector2(-200f, -400f));
            cancel.onClick.AddListener(Cancel);
            watchButton = NewButton("Watch", panel.transform, "광고 보기", new Color(0.4f, 0.85f, 0.75f, 1f), font, new Vector2(200f, -400f));
            watchButton.onClick.AddListener(() => onWatch?.Invoke());
            root.SetActive(false);
        }

        // gold·mycelite = 받을 양. watch = "광고 보기"를 눌렀을 때, close = 창이 닫힐 때(취소·뒤로가기·보상 뒤)
        public void Open(long gold, long mycelite, Action watch, Action close)
        {
            onWatch = watch;
            onClose = close;
            goldText.text = "+" + Wallet.Format(gold);
            myceliteText.text = "+" + Wallet.Format(mycelite);
            statusText.text = "";
            watchButton.interactable = true;
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
            onClose = onWatch = null;
            close?.Invoke();
        }

        void Cancel() => Close();

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
            return t;
        }

        static Button NewButton(string name, Transform parent, string label, Color tint, TMP_FontAsset font, Vector2 pos)
        {
            var img = NewImage(name, parent, Resources.Load<Sprite>(ButtonSprite), tint, new Vector2(340f, 150f), pos);
            img.type = Image.Type.Sliced;
            img.raycastTarget = true;
            NewText("Label", img.transform, label, 48f, Color.white, font, new Vector2(300f, 100f), Vector2.zero);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            return button;
        }
    }
}
