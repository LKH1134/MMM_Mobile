using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 출석 창의 하루 칸 (프리팹 AttendanceScreen 안 Day1~Day7). 모양은 프리팹에서 고치고, 값은 AttendanceScreenView가 넣는다
    public class AttendanceCellView : MonoBehaviour
    {
        public Image background;
        public TMP_Text dayText;
        public Image icon;
        public Image check;         // 받은 날 체크 표시
        public TMP_Text amount;
        public GameObject next;     // 다음에 받을 날 표시 (NEXT)
    }
}
