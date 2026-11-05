using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // [UI 개편 2026-11-05] "광고 보기" 창의 화면 조각 (프리팹 Prefabs/Mobile/UI/Main/Resources/AdBoxPopup).
    // 값 채우기·버튼 동작은 AdBoxPopup이 한다. 프리팹이 없으면 AdBoxPopup이 예전처럼 코드로 만든다
    public class AdBoxView : MonoBehaviour
    {
        public TMP_Text goldText;
        public TMP_Text myceliteText;
        public TMP_Text statusText;     // 광고 불러오는 중 등
        public Button watchButton;      // 광고 보기
        public Button cancelButton;     // 취소
    }
}
