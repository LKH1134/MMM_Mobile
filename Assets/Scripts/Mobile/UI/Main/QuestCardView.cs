using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 일일 의뢰 카드 하나 (프리팹 QuestScreen 안 CardTemplate). 값은 QuestScreenView가 넣는다
    public class QuestCardView : MonoBehaviour
    {
        public TMP_Text title;
        public Image icon;
        public TMP_Text desc;
        public TMP_Text reward;
        public RectTransform barFill;
        public TMP_Text progress;
        public Button button;
        public TMP_Text buttonText;
    }
}
