using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 전투 화면 왼쪽 아이콘 열의 일일 퀘스트 버튼. 누르면 퀘스트 창을 열고, 받을 보상이 있으면 빨간 점을 보인다.
    // IdleBattle의 HUD에 Create로 만들어 붙인다 (홈 화면을 따라 보이고 숨는다)
    public class QuestButton : MonoBehaviour
    {
        const string IconSprite = "Art/Dream/Icons/quest";      // 임시 그림
        static readonly Color PlateColor = new Color(0.1f, 0.08f, 0.14f, 0.85f);

        GameObject dot;

        public static QuestButton Create(Transform parent, Vector2 pos, float size, TMP_FontAsset font)
        {
            var plate = new GameObject("QuestButton", typeof(RectTransform), typeof(Image), typeof(Button));
            plate.transform.SetParent(parent, false);
            var rt = (RectTransform)plate.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            var image = plate.GetComponent<Image>();
            image.color = PlateColor;
            image.raycastTarget = true;

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(plate.transform, false);
            icon.sprite = Resources.Load<Sprite>(IconSprite);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var irt = icon.rectTransform;
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(10f, 10f);
            irt.offsetMax = new Vector2(-10f, -10f);

            var dotImage = new GameObject("Dot", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            dotImage.transform.SetParent(plate.transform, false);
            dotImage.color = new Color(0.95f, 0.2f, 0.2f);
            dotImage.raycastTarget = false;
            var drt = dotImage.rectTransform;
            drt.anchorMin = drt.anchorMax = Vector2.one;
            drt.sizeDelta = new Vector2(30f, 30f);
            drt.anchoredPosition = new Vector2(-6f, -6f);

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(plate.transform, false);
            if (font != null) label.font = font;
            label.text = "퀘스트";
            label.fontSize = 22f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.outlineWidth = 0.25f;
            label.outlineColor = new Color32(40, 25, 15, 255);
            var lrt = label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(0f, -2f);
            lrt.sizeDelta = new Vector2(size + 20f, 30f);

            var button = plate.GetComponent<QuestButton>() ?? plate.AddComponent<QuestButton>();
            button.dot = dotImage.gameObject;
            plate.GetComponent<Button>().onClick.AddListener(DailyQuestPopup.Open);
            button.Refresh();
            return button;
        }

        void OnEnable()
        {
            DailyQuests.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => DailyQuests.Changed -= Refresh;

        void Refresh()
        {
            if (dot != null) dot.SetActive(DailyQuests.ClaimableCount > 0);
        }
    }
}
