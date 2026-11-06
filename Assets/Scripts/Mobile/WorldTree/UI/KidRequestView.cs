using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // [UI 개편 2026-11-05] 세계수 꼬마 시트의 버섯 부탁 칸 (프리팹 KidPopup 안 Requests/Request_n/Card).
    // 홈 일일 의뢰 카드 조각(QuestCardView) + 부탁한 꼬마 그림. 값은 TreeKidScreen이 넣는다
    public class KidRequestView : QuestCardView
    {
        public Image kidIcon;
    }
}
