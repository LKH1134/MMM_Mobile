using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // InGame 결과 팝업. 시간이 다 되면 HarvestGame이 열어 이번 판에 번 골드(점수 × 전환율)와 점수·수확량을 보여 준다.
    // 닫기 버튼·배경 눌러 닫기는 없다 — 다시 하기(이 씬을 다시 연다) 아니면 세계수로 돌아가기.
    // 첫 클리어 균사석은 따로 강조한 줄로 보여 준다. 보스에 지면 버튼이 "재도전 / 파밍 / 세계수로" 셋이 된다
    // (파밍 = StageProgress.ChooseFarm 뒤 다시 연다)
    [RequireComponent(typeof(UiPopup))]
    public class ResultPopup : MonoBehaviour
    {
        [SerializeField] TMP_Text earnedText;
        [SerializeField] TMP_Text countText;
        [SerializeField] Button retryButton;
        [SerializeField] Button worldTreeButton;
        [SerializeField] TMP_Text myceliteText;          // "첫 클리어 보상 · 균사석 +N". 없으면 숨긴다
        [SerializeField] Button farmButton;              // 보스에 졌을 때만 보인다
        [SerializeField] TMP_Text retryLabel;
        [SerializeField] string retryText = "다시 하기";
        [SerializeField] string bossRetryText = "재도전";
        [SerializeField] float choiceWidth = 230f;       // 버튼 셋일 때 폭 (둘일 때는 프리팹 크기)
        [SerializeField] string worldTreeScene = "WorldTree";
        [SerializeField] float countUpDuration = 0.8f;   // 번 골드가 0부터 올라가는 시간

        UiPopup popup;
        Coroutine countUp;
        bool laidOut;
        Vector2 retryPos, worldTreePos, buttonSize;

        void Awake()
        {
            popup = GetComponent<UiPopup>();
            retryButton.onClick.AddListener(Retry);
            worldTreeButton.onClick.AddListener(GoWorldTree);
            if (farmButton != null) farmButton.onClick.AddListener(Farm);
        }

        // note: "보스 처치"·"보스 실패"·"목표 21/25 · 다시 도전" 같은 한 마디를 앞에 붙인다.
        // mycelite: 첫 클리어 균사석 (0이면 줄을 숨긴다), bossLost: 보스에 졌으면 재도전·파밍 선택
        public void Show(long earned, long score, long harvested, string note = null, long mycelite = 0, bool bossLost = false)
        {
            // 꺼진 채로 만들어져 아직 Awake 전일 수 있다. Open이 켜면서 Awake가 돈다
            if (popup == null) popup = GetComponent<UiPopup>();
            popup.Open();
            countText.text = (string.IsNullOrEmpty(note) ? "" : note + " · ") + $"점수 {score:N0} · 버섯 {harvested:N0}개";
            if (myceliteText != null)
            {
                myceliteText.gameObject.SetActive(mycelite > 0);
                myceliteText.text = "";
            }
            ShowChoice(bossLost);
            if (countUp != null) StopCoroutine(countUp);
            countUp = StartCoroutine(CountUp(earned, mycelite));
        }

        // 보스에 지면 버튼 셋(재도전·파밍·세계수로)으로 좁혀 늘어놓는다. 아니면 프리팹 배치 그대로
        void ShowChoice(bool bossLost)
        {
            var retryRect = (RectTransform)retryButton.transform;
            var worldRect = (RectTransform)worldTreeButton.transform;
            if (!laidOut)
            {
                laidOut = true;
                retryPos = retryRect.anchoredPosition;
                worldTreePos = worldRect.anchoredPosition;
                buttonSize = retryRect.sizeDelta;
            }
            bool three = bossLost && farmButton != null;
            if (farmButton != null) farmButton.gameObject.SetActive(three);
            if (retryLabel != null) retryLabel.text = bossLost ? bossRetryText : retryText;
            float gap = three ? choiceWidth + 15f : 0f;
            var size = three ? new Vector2(choiceWidth, buttonSize.y) : buttonSize;
            retryRect.sizeDelta = worldRect.sizeDelta = size;
            retryRect.anchoredPosition = three ? new Vector2(-gap, retryPos.y) : retryPos;
            worldRect.anchoredPosition = three ? new Vector2(gap, worldTreePos.y) : worldTreePos;
            if (three)
            {
                var farmRect = (RectTransform)farmButton.transform;
                farmRect.sizeDelta = size;
                farmRect.anchoredPosition = new Vector2(0f, retryPos.y);
            }
        }

        // 시간이 멈춰 있어도 돌도록 unscaled로 센다
        IEnumerator CountUp(long earned, long mycelite)
        {
            for (float t = 0f; t < countUpDuration; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / countUpDuration, 3f);
                earnedText.text = ((long)(earned * k)).ToString("N0");
                if (mycelite > 0 && myceliteText != null) myceliteText.text = $"첫 클리어 보상 · 균사석 +{(long)(mycelite * k)}";
                yield return null;
            }
            earnedText.text = earned.ToString("N0");
            if (mycelite > 0 && myceliteText != null) myceliteText.text = $"첫 클리어 보상 · 균사석 +{mycelite}";
            countUp = null;
        }

        void Retry() => SceneManager.LoadScene(gameObject.scene.name);

        void Farm()
        {
            StageProgress.ChooseFarm();
            SceneManager.LoadScene(gameObject.scene.name);
        }

        void GoWorldTree()
        {
            if (Application.CanStreamedLevelBeLoaded(worldTreeScene)) SceneManager.LoadScene(worldTreeScene);
            else Debug.LogWarning($"[ResultPopup] 빌드 설정에 없는 씬: {worldTreeScene}");
        }
    }
}
