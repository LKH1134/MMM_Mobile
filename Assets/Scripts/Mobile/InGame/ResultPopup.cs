using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // InGame 결과 팝업. 시간이 다 되면 HarvestGame이 열어 이번 판에 번 골드(점수 × 전환율)와 점수·수확량을 보여 준다.
    // 닫기 버튼·배경 눌러 닫기는 없다 — 다음 판(이 씬을 다시 연다) 아니면 스킬트리로.
    // 다음 판 버튼 글자는 판이 끝난 뒤 진행(StageProgress)에 따라: 스테이지가 올랐으면 "다음 스테이지", 파밍을 마쳐 보스가 기다리면 "보스 도전",
    // 마지막 스테이지에 머물면 "다시 하기". 첫 클리어 균사석은 따로 강조한 줄로 보여 준다.
    // 보스에 지면 버튼이 "재도전 / 파밍 / 스킬트리로" 셋이 된다 (파밍 = StageProgress.ChooseFarm 뒤 다시 연다)
    [RequireComponent(typeof(UiPopup))]
    public class ResultPopup : MonoBehaviour
    {
        [SerializeField] TMP_Text earnedText;
        [SerializeField] TMP_Text countText;
        [SerializeField] Button retryButton;
        [SerializeField] Button skillTreeButton;
        [SerializeField] TMP_Text myceliteText;          // "첫 클리어 보상 · 균사석 +N". 없으면 숨긴다
        [SerializeField] Button farmButton;              // 보스에 졌을 때만 보인다
        [SerializeField] TMP_Text retryLabel;
        [SerializeField] string nextStageText = "다음 스테이지";
        [SerializeField] string bossChallengeText = "보스 도전";
        [SerializeField] string retryText = "다시 하기";
        [SerializeField] string bossRetryText = "재도전";
        [SerializeField] float choiceWidth = 230f;       // 버튼 셋일 때 폭 (둘일 때는 프리팹 크기)
        [SerializeField] string skillTreeScene = "SkillTree";
        [SerializeField] float countUpDuration = 0.8f;   // 번 골드가 0부터 올라가는 시간

        UiPopup popup;
        Coroutine countUp;
        bool laidOut;
        Vector2 retryPos, skillTreePos, buttonSize;

        void Awake()
        {
            popup = GetComponent<UiPopup>();
            retryButton.onClick.AddListener(Retry);
            skillTreeButton.onClick.AddListener(GoSkillTree);
            if (farmButton != null) farmButton.onClick.AddListener(Farm);
        }

        // note: "보스 처치"·"보스 실패" 같은 한 마디를 앞에 붙인다.
        // mycelite: 첫 클리어 균사석 (0이면 줄을 숨긴다), bossLost: 보스에 졌으면 재도전·파밍 선택,
        // playedStage: 방금 끝난 판의 스테이지 (진행이 이미 반영된 StageProgress와 비교해 버튼 글자를 정한다)
        public void Show(long earned, long score, long harvested, string note = null, long mycelite = 0, bool bossLost = false, int playedStage = 0)
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
            ShowChoice(bossLost, playedStage);
            if (countUp != null) StopCoroutine(countUp);
            countUp = StartCoroutine(CountUp(earned, mycelite));
        }

        string NextLabel(bool bossLost, int playedStage)
        {
            if (bossLost) return bossRetryText;
            if (StageProgress.Current != playedStage) return nextStageText;
            return StageProgress.BossActive ? bossChallengeText : retryText;
        }

        // 보스에 지면 버튼 셋(재도전·파밍·스킬트리로)으로 좁혀 늘어놓는다. 아니면 프리팹 배치 그대로
        void ShowChoice(bool bossLost, int playedStage)
        {
            var retryRect = (RectTransform)retryButton.transform;
            var treeRect = (RectTransform)skillTreeButton.transform;
            if (!laidOut)
            {
                laidOut = true;
                retryPos = retryRect.anchoredPosition;
                skillTreePos = treeRect.anchoredPosition;
                buttonSize = retryRect.sizeDelta;
            }
            bool three = bossLost && farmButton != null;
            if (farmButton != null) farmButton.gameObject.SetActive(three);
            if (retryLabel != null) retryLabel.text = NextLabel(bossLost, playedStage);
            float gap = three ? choiceWidth + 15f : 0f;
            var size = three ? new Vector2(choiceWidth, buttonSize.y) : buttonSize;
            retryRect.sizeDelta = treeRect.sizeDelta = size;
            retryRect.anchoredPosition = three ? new Vector2(-gap, retryPos.y) : retryPos;
            treeRect.anchoredPosition = three ? new Vector2(gap, skillTreePos.y) : skillTreePos;
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

        void GoSkillTree()
        {
            if (Application.CanStreamedLevelBeLoaded(skillTreeScene)) SceneManager.LoadScene(skillTreeScene);
            else Debug.LogWarning($"[ResultPopup] 빌드 설정에 없는 씬: {skillTreeScene}");
        }
    }
}
