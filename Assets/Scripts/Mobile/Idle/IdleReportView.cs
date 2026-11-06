using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // [UI 개편 2026-11-05] 방치 정산 팝업의 화면 조각 (프리팹 Prefabs/Mobile/UI/Main/Resources/IdleReportPopup).
    // 값 채우기·버튼 동작은 IdleReportPopup이 한다. 프리팹이 없으면 IdleReportPopup이 예전처럼 코드로 만든다.
    // 비어 있는 칸은 건너뛴다 (예: 균사석 줄을 지워도 된다)
    public class IdleReportView : MonoBehaviour
    {
        public TMP_Text timeText;       // "자리를 비운 동안 1분 25초"
        public TMP_Text goldText;       // "+984b"
        public TMP_Text goldSubText;    // "(앱 밖 1분 25초 +984b)" — 앱 밖 보상이 없으면 숨김
        public GameObject myceliteRow;  // 균사석을 안 받았으면 숨김
        public TMP_Text myceliteText;
        public GameObject infoRow;      // 처치·스테이지가 없으면 숨김
        public TMP_Text infoText;       // "처치 120마리 · 스테이지 +1 → 7-4"
        public GameObject doubledRow;   // 2배를 받으면 켠다
        public Button okButton;         // 확인 (닫기)
        public Button doubleButton;     // 광고 보고 2배 보상
    }
}
