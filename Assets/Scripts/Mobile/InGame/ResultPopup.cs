using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // InGame 결과 팝업. 시간이 다 되면 HarvestGame이 열어 이번 판에 번 돈·캔 버섯 수를 보여 준다.
    // 닫기 버튼·배경 눌러 닫기는 없다 — 다시 하기(이 씬을 다시 연다) 아니면 세계수로 돌아가기
    [RequireComponent(typeof(UiPopup))]
    public class ResultPopup : MonoBehaviour
    {
        [SerializeField] TMP_Text earnedText;
        [SerializeField] TMP_Text countText;
        [SerializeField] Button retryButton;
        [SerializeField] Button worldTreeButton;
        [SerializeField] string worldTreeScene = "WorldTree";
        [SerializeField] float countUpDuration = 0.8f;   // 번 돈이 0부터 올라가는 시간

        UiPopup popup;
        Coroutine countUp;

        void Awake()
        {
            popup = GetComponent<UiPopup>();
            retryButton.onClick.AddListener(Retry);
            worldTreeButton.onClick.AddListener(GoWorldTree);
        }

        public void Show(long earned, int harvested)
        {
            // 꺼진 채로 만들어져 아직 Awake 전일 수 있다. Open이 켜면서 Awake가 돈다
            if (popup == null) popup = GetComponent<UiPopup>();
            popup.Open();
            countText.text = $"버섯 {harvested:N0}개";
            if (countUp != null) StopCoroutine(countUp);
            countUp = StartCoroutine(CountUp(earned));
        }

        // 시간이 멈춰 있어도 돌도록 unscaled로 센다
        IEnumerator CountUp(long earned)
        {
            for (float t = 0f; t < countUpDuration; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / countUpDuration, 3f);
                earnedText.text = ((long)(earned * k)).ToString("N0");
                yield return null;
            }
            earnedText.text = earned.ToString("N0");
            countUp = null;
        }

        void Retry() => SceneManager.LoadScene(gameObject.scene.name);

        void GoWorldTree()
        {
            if (Application.CanStreamedLevelBeLoaded(worldTreeScene)) SceneManager.LoadScene(worldTreeScene);
            else Debug.LogWarning($"[ResultPopup] 빌드 설정에 없는 씬: {worldTreeScene}");
        }
    }
}
