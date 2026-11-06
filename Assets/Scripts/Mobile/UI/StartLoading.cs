using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreMush.Mobile
{
    // Start 씬 로딩 화면. 퍼센트 글자(00% → 100%)와 진행 바(progress_back 위로 progress_fill이 차오름)를 같이 움직인다.
    // nextScene을 넣으면 그 씬을 뒤에서 읽고, 다 차면 넘어간다. 비워 두면 연출만 하고 멈춘다.
    public class StartLoading : MonoBehaviour
    {
        [SerializeField] TMP_Text percentText;
        [SerializeField] RectTransform fill;      // 진행 바 안쪽 채움. 앵커 x 최대값으로 길이를 늘린다
        [SerializeField] string nextScene = "";
        [SerializeField] float minDuration = 2f;  // 로딩이 빨라도 이만큼은 보여 준다
        [SerializeField] float consentCheckTimeout = 6f;   // 광고 동의 정보 확인을 이만큼까지만 기다린다 (오프라인 등). 양식이 떠 있으면 답할 때까지 기다린다

        void Start() => StartCoroutine(Run());

        IEnumerator Run()
        {
            // 게임 표(밸런스)를 여기서 미리 읽는다 — 홈 화면이 뜨는 순간 멈칫하지 않게. 읽지 못하면 안내하고 멈춘다 (깨진 채로 홈에 가지 않게)
            Show(0f);
            yield return null;      // 0% 화면을 한 번 그린 뒤 읽는다
            if (!GameBalance.Preload(out _))
            {
                if (percentText != null) { percentText.enableAutoSizing = true; percentText.fontSizeMin = 18f; percentText.textWrappingMode = TextWrappingModes.Normal; }
                if (percentText != null) percentText.text = "게임 데이터를 읽지 못했어요\n앱을 다시 실행해 주세요";
                yield break;
            }

            // 광고 동의를 여기서 시작한다: 처음 실행한 동의 지역(EEA·영국 등) 사용자에게 로딩 화면 위로 동의 양식이 뜬다
            AdService.Initialize();
            AsyncOperation op = null;
            if (!string.IsNullOrEmpty(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene))
            {
                op = SceneManager.LoadSceneAsync(nextScene);
                op.allowSceneActivation = false;
            }
            else if (!string.IsNullOrEmpty(nextScene))
                Debug.LogWarning($"[StartLoading] 빌드 설정에 없는 씬: {nextScene}");

            float t = 0f;
            float shown = 0f;
            while (shown < 1f)
            {
                t += Time.unscaledDeltaTime;
                // 실제 로딩(0.9에서 멈춤)과 최소 시간 중 느린 쪽을 따른다
                float real = op == null ? 1f : Mathf.Clamp01(op.progress / 0.9f);
                shown = Mathf.Min(real, t / Mathf.Max(0.01f, minDuration));
                Show(shown);
                yield return null;
            }

            // 동의 양식이 떠 있으면 답할 때까지, 아직 확인 중이면 consentCheckTimeout까지 기다린 뒤 넘어간다
            float waited = 0f;
            while (AdService.Consent == AdService.ConsentStep.FormShowing
                   || (AdService.Consent == AdService.ConsentStep.Checking && waited < consentCheckTimeout))
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (op != null) op.allowSceneActivation = true;
        }

        void Show(float p)
        {
            if (percentText != null) percentText.text = $"{Mathf.FloorToInt(p * 100f):00}%";
            if (fill != null) fill.anchorMax = new Vector2(p, fill.anchorMax.y);
        }
    }
}
