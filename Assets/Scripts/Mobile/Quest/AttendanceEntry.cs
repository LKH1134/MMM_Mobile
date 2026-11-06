using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 홈 왼쪽 아이콘 열의 출석 버튼 (퀘스트와 도감 사이) + 출석 자동 처리.
    // 버튼은 퀘스트 버튼을 복제해 도감 자리에 놓고 도감을 한 칸 내린다 (프리팹을 고치지 않아도 되게, MainHudView가 만든다).
    // 날짜가 바뀌면(Main에 들어올 때, 접속 중이면 한국 00시가 지나는 순간) 보상을 바로 주고 출석 창을 띄운다.
    // 다른 팝업·씬 로딩 중에는 끝날 때까지 기다린다 (보상은 기다리는 동안에도 받은 것으로 치지 않는다 — 창이 뜰 때 준다).
    // 마지막 날까지 다 받은 뒤에는 버튼을 만들지 않는다 (도감도 제자리)
    public class AttendanceEntry : MonoBehaviour
    {
        const string IconPath = "UI/Icon/daily_reward";
        const float StartDelay = 1f;        // Main에 들어온 직후 앱 밖 보상 창 등이 먼저 뜨게 기다린다
        const float CheckEvery = 1f;

        GameObject dot;
        float wait = StartDelay;

        public static AttendanceEntry Create(Button template, Button below)
        {
            if (template == null || Attendance.Finished) return null;
            var t = (RectTransform)template.transform;
            var go = Instantiate(template.gameObject, t.parent, false);
            go.name = "AttendanceButton";
            go.transform.SetSiblingIndex(t.GetSiblingIndex() + 1);
            var rt = (RectTransform)go.transform;
            if (below != null)
            {
                var b = (RectTransform)below.transform;
                Vector2 step = b.anchoredPosition - t.anchoredPosition;      // 퀘스트 → 도감 간격만큼
                rt.anchoredPosition = b.anchoredPosition;
                b.anchoredPosition += step;
            }
            else rt.anchoredPosition += new Vector2(0f, -rt.rect.height * 1.4f);

            var button = go.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => AttendancePopup.Open());
            var icon = Resources.Load<Sprite>(IconPath);
            if (icon != null && go.TryGetComponent<Image>(out var image)) image.sprite = icon;
            var label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "출석";

            var entry = go.AddComponent<AttendanceEntry>();
            var dotT = go.transform.Find("Dot");
            entry.dot = dotT != null ? dotT.gameObject : null;
            return entry;
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
            if (PopupGate.IsOpen || SceneLoader.IsLoading || AdService.IsShowing) return;
            int day = Attendance.Claim();
            if (day > 0) AttendancePopup.Open(day);
        }
    }
}
