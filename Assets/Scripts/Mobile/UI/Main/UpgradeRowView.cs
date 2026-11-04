using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 강화 노트의 한 줄 (프리팹 UpgradeRow): 왼쪽 카드(아이콘·레벨·이름·값) / 스프링 / 오른쪽 강화 버튼(강화·비용) / 잠금 덮개.
    // 모양은 프리팹에서 고치고, 값은 UpgradeNotebookView가 넣는다
    public class UpgradeRowView : MonoBehaviour
    {
        public Image icon;
        public TMP_Text level;
        public TMP_Text nameText;
        public TMP_Text value;
        public Button button;
        public Image buttonImage;
        public TMP_Text buttonText;
        public GameObject costRow;
        public TMP_Text cost;
        public GameObject lockCover;
        public TMP_Text lockText;
        public Graphic[] springs;      // 접힌 선 위 스프링. 스크롤 가림(RectMask2D)을 받지 않고(maskable 끔), 반 넘게 목록 밖으로 나가면 통째로 숨긴다

        [System.NonSerialized] public IdleStat stat;
        [System.NonSerialized] public long nextCost;    // Refresh 때 셈해 둔 다음 레벨 비용
        [System.NonSerialized] public bool buyable;     // 열렸고 최대·단계 제한이 아님
        [System.NonSerialized] public float labelY, labelHeight, labelSize;   // 버튼 글자의 프리팹 원래 자리 (단계 제한 문구 뒤 되돌리기용)
    }
}
