using System.Collections;
using TMPro;
using UnityEngine;

namespace MoreMush.Mobile
{
    // InGame 첫 입장 안내. "드래그하여 발사"를 첫 투척까지 띄우고, 던지면 "버섯을 채집하세요!"를 잠깐 띄운 뒤 끝난다.
    // 한 번 끝까지 보면 다시 나오지 않는다 (PlayerPrefs). 글자는 터치를 막지 않는다 — 안내를 띄운 채로 바로 끌어 던질 수 있다.
    // InGame 씬 HUD에 꺼 둔 채 배치(띠 + 글자). HarvestGame이 판 시작 때 Begin을 부른다 (측정기 판에서는 부르지 않는다)
    public class InGameTutorial : MonoBehaviour
    {
        const string DoneKey = "InGame.TutorialDone";
        const string DragKey = "ingame.tutorial_drag";
        const string HarvestKey = "ingame.tutorial_harvest";
        const float HarvestShow = 2f;       // "버섯을 채집하세요!"를 보여 주는 시간
        const float Fade = 0.25f;
        const float PulseSpeed = 3f;

        [SerializeField] CanvasGroup group;     // 터치를 막지 않는다 (blocksRaycasts 끔)
        [SerializeField] TMP_Text label;

        HarvestField field;
        bool launched;

        public static bool Done => PlayerPrefs.GetInt(DoneKey, 0) == 1;

        public void Begin(HarvestField harvestField)
        {
            if (Done || harvestField == null) return;
            field = harvestField;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            group.alpha = 1f;
            label.text = Loc.T(DragKey);
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
            label.text = Loc.T(HarvestKey);
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
            gameObject.SetActive(false);
        }
    }
}
