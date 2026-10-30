using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // InGame 첫 입장 안내. "드래그하여 발사"를 첫 투척까지 띄우고, 던지면 "버섯을 캐십쇼"를 잠깐 띄운 뒤 끝난다.
    // 한 번 끝까지 보면 다시 나오지 않는다 (PlayerPrefs). 글자는 터치를 막지 않는다 — 안내를 띄운 채로 바로 끌어 던질 수 있다.
    // HarvestGame이 판 시작 때 Begin을 부른다 (측정기 판에서는 부르지 않는다)
    public class InGameTutorial : MonoBehaviour
    {
        const string DoneKey = "InGame.TutorialDone";
        const string DragText = "드래그하여 발사";
        const string HarvestText = "버섯을 캐십쇼";
        const float HarvestShow = 2f;       // "버섯을 캐십쇼"를 보여 주는 시간
        const float Fade = 0.25f;
        const float PulseSpeed = 3f;

        HarvestField field;
        CanvasGroup group;
        TMP_Text label;
        bool launched;

        public static bool Done => PlayerPrefs.GetInt(DoneKey, 0) == 1;

        // canvasRoot: 글자를 올릴 캔버스(HUD). font는 HUD 글자에서 가져온다
        public static void Begin(HarvestField field, Transform canvasRoot, TMP_FontAsset font)
        {
            if (Done || field == null || canvasRoot == null) return;
            var go = new GameObject("Tutorial", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(canvasRoot, false);
            var tutorial = go.AddComponent<InGameTutorial>();
            tutorial.field = field;
            tutorial.Build(font);
        }

        void Build(TMP_FontAsset font)
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(0f, 0.3f);
            rt.anchorMax = new Vector2(1f, 0.3f);
            rt.sizeDelta = new Vector2(0f, 170f);
            transform.SetAsLastSibling();
            group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var band = new GameObject("Band", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            band.transform.SetParent(rt, false);
            band.color = new Color(0f, 0f, 0f, 0.45f);
            band.raycastTarget = false;
            Stretch(band.rectTransform);

            label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(rt, false);
            if (font != null) label.font = font;
            label.fontSize = 72f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            Stretch(label.rectTransform);
            label.text = DragText;

            field.Launched += OnLaunched;
        }

        void OnDestroy()
        {
            if (field != null) field.Launched -= OnLaunched;
        }

        // 첫 투척 전에는 글자가 숨 쉬듯 커졌다 작아진다
        void Update()
        {
            if (launched) return;
            float s = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * PulseSpeed);
            label.transform.localScale = new Vector3(s, s, 1f);
        }

        void OnLaunched(Harvester h)
        {
            if (launched || h != field.Main) return;
            launched = true;
            field.Launched -= OnLaunched;
            label.transform.localScale = Vector3.one;
            label.text = HarvestText;
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
            StartCoroutine(HideAfter(HarvestShow));
        }

        IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            for (float t = 0f; t < Fade; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / Fade;
                yield return null;
            }
            Destroy(gameObject);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
