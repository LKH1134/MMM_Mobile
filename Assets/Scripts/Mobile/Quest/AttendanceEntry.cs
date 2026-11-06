using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 홈 왼쪽 아이콘 열의 출석 버튼 (프리팹 MainHud › SideButtons › AttendanceButton, 퀘스트와 도감 사이) + 출석 자동 처리.
    // 날짜가 바뀌면(Main에 들어올 때, 접속 중이면 한국 00시가 지나는 순간) 보상을 바로 주고 출석 화면을 띄운다.
    // 다른 팝업·씬 로딩·광고 중에는 끝날 때까지 기다린다 (보상은 화면이 뜰 때 준다).
    // 마지막 날까지 다 받았으면 버튼을 숨기고 아래 버튼(도감)을 이 자리로 올린다
    public class AttendanceEntry : MonoBehaviour
    {
        const float StartDelay = 1f;        // Main에 들어온 직후 앱 밖 보상 창 등이 먼저 뜨게 기다린다
        const float CheckEvery = 1f;

        [SerializeField] Button button;
        [SerializeField] GameObject dot;                  // 오늘 받을 게 남았으면 켠다
        [SerializeField] RectTransform moveUpWhenHidden;  // 버튼을 숨길 때 이 자리로 올릴 버튼 (도감)
        [SerializeField] AttendanceScreenView screen;     // Main 씬의 출석 화면 (씬에서 연결)

        float wait = StartDelay;

        void Awake()
        {
            if (button != null) button.onClick.AddListener(() => { if (screen != null) screen.Open(); });
        }

        void Start()
        {
            if (!Attendance.Finished) return;
            if (moveUpWhenHidden != null) moveUpWhenHidden.anchoredPosition = ((RectTransform)transform).anchoredPosition;
            gameObject.SetActive(false);
        }

        void OnEnable()
        {
            Attendance.Changed += RefreshDot;
            RefreshDot();
        }

        void OnDisable() => Attendance.Changed -= RefreshDot;

        void RefreshDot()
        {
            if (dot != null) dot.SetActive(Attendance.CanClaim);
        }

        void Update()
        {
            wait -= Time.unscaledDeltaTime;
            if (wait > 0f) return;
            wait = CheckEvery;
            if (!Attendance.CheckDay())
            {
                if (dot != null && dot.activeSelf) RefreshDot();    // 다른 곳에서 받았으면 점을 끈다
                return;
            }
            RefreshDot();
            // 다른 창이 떠 있거나 씬을 옮기는 중이면 다음 확인 때 (다른 팝업을 닫아 버리지 않게)
            if (screen == null || PopupGate.IsOpen || SceneLoader.IsLoading || AdService.IsShowing) return;
            int day = Attendance.Claim();
            if (day > 0) screen.Open(day);
        }
    }
}
