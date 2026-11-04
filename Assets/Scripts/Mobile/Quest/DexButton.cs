using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 전투 화면 왼쪽 아이콘 열의 도감 버튼 (일일 퀘스트 버튼 아래). 도감 창이 아직 없어 누르면 Opened 이벤트만 알린다.
    // IdleBattle의 HUD에 Create로 만들어 붙인다 (홈 화면을 따라 보이고 숨는다)
    public class DexButton : MonoBehaviour
    {
        const string IconSprite = "UI/Icon/collection_book";      // 임시 그림
        static readonly Color PlateColor = new Color(0.1f, 0.08f, 0.14f, 0.85f);

        public static event System.Action Opened;   // 도감 창이 생기면 여기에 연결한다

        public static DexButton Create(Transform parent, Vector2 pos, float size, TMP_FontAsset font)
        {
            var plate = new GameObject("DexButton", typeof(RectTransform), typeof(Image), typeof(Button));
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

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(plate.transform, false);
            if (font != null) label.font = font;
            label.text = "도감";
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

            var button = plate.GetComponent<DexButton>() ?? plate.AddComponent<DexButton>();
            var hint = label;
            plate.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (Opened != null) { Opened(); return; }
                button.StopAllCoroutines();
                button.StartCoroutine(button.Flash(hint));   // 창이 없으면 눌렸다는 걸 라벨로 알린다
            });
            return button;
        }

        System.Collections.IEnumerator Flash(TextMeshProUGUI label)
        {
            label.text = "준비 중";
            yield return new WaitForSecondsRealtime(1.2f);
            label.text = "도감";
        }
    }
}
