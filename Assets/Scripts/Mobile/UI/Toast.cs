using System.Collections;
using TMPro;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 화면 가운데에 잠깐 떴다 사라지는 안내 문구. 터치를 막지 않는다.
    // 프리팹 하나를 처음 쓸 때 만들어 두고 계속 다시 쓴다 — 보이는 중에 또 부르면 문구를 바꾸고 시간을 처음부터 센다
    [RequireComponent(typeof(CanvasGroup))]
    public class Toast : MonoBehaviour
    {
        [SerializeField] TMP_Text messageText;
        [SerializeField] float fade = 0.2f;           // 나타날 때·사라질 때 걸리는 시간

        CanvasGroup group;
        Coroutine play;

        void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        // duration = 다 보이는 상태로 머무는 시간 + 나타나고 사라지는 시간
        public void Show(string message, float duration)
        {
            gameObject.SetActive(true);
            if (group == null) Awake();
            transform.SetAsLastSibling();
            messageText.text = message;
            if (play != null) StopCoroutine(play);
            play = StartCoroutine(Play(duration));
        }

        // 일시정지·배속과 상관없이 실제 시간으로 센다
        IEnumerator Play(float duration)
        {
            float hold = Mathf.Max(0f, duration - fade * 2f);
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                group.alpha = t / fade;
                yield return null;
            }
            group.alpha = 1f;
            yield return new WaitForSecondsRealtime(hold);
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / fade;
                yield return null;
            }
            group.alpha = 0f;
            play = null;
            gameObject.SetActive(false);
        }
    }
}
