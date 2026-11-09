using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 뽑기 결과 칸 하나 (GachaResultPopup 안 Grid 아래 CellTemplate). 등급 테두리 + 그림 + 이름(등급) + 처음 얻은 것은 "NEW!".
    // 모양은 템플릿에서 고친다. 칸 크기는 GachaResultPopup이 배율(localScale)로 맞춘다
    public class GachaResultCell : MonoBehaviour
    {
        public Image glow;          // 등급별 테두리 그림
        public Image icon;
        public TMP_Text nameText;
        public GameObject newBadge;
    }
}
