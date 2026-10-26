using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 옵션 팝업의 데이터 초기화 버튼. 실수로 지우지 않게 두 번 눌러야 한다 —
    // 처음 누르면 글자가 확인 문구로 바뀌고, confirmTime 안에 한 번 더 누르면 저장된 데이터(PlayerPrefs)를 모두 지우고
    // 처음 로딩(Start 씬)부터 다시 시작한다. 시간이 지나면 원래 글자로 돌아간다
    [RequireComponent(typeof(Button))]
    public class DataResetButton : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] string normalText = "데이터 초기화";
        [SerializeField] string confirmText = "한 번 더 누르면 초기화";
        [SerializeField] float confirmTime = 3f;
        [SerializeField] string startScene = "Start";

        bool armed;
        Coroutine timer;

        void Awake() => GetComponent<Button>().onClick.AddListener(Press);

        void OnEnable() => Disarm();

        void Press()
        {
            if (!armed)
            {
                armed = true;
                if (label != null) label.text = confirmText;
                timer = StartCoroutine(DisarmLater());
                return;
            }
            ResetAll();
        }

        // 일시정지 중(옵션 팝업이 열린 InGame)에도 흐르게 실제 시간으로 센다
        IEnumerator DisarmLater()
        {
            yield return new WaitForSecondsRealtime(confirmTime);
            Disarm();
        }

        void Disarm()
        {
            armed = false;
            if (timer != null) StopCoroutine(timer);
            timer = null;
            if (label != null) label.text = normalText;
        }

        void ResetAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(startScene)) SceneManager.LoadScene(startScene);
            else Debug.LogWarning($"[DataResetButton] 빌드 설정에 없는 씬: {startScene}");
        }
    }
}
