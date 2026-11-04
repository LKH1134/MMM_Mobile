using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 편성 화면의 꼬마 칸 하나 (위쪽 편성 칸 줄과 아래 보유 꼬마 목록이 같이 쓴다).
    // 모양은 프리팹(FormationScreen 안 SlotTemplate·CellTemplate)에서 고치고, 값은 FormationScreenView가 넣는다
    public class FormationCellView : MonoBehaviour
    {
        public Button button;
        public Image background;
        public Image icon;
        public TMP_Text nameText;       // 없어도 된다 (편성 칸)
        public TMP_Text levelText;
        public GameObject badge;        // 편성 중 표시 (목록)
        public GameObject selection;    // 고른 칸 표시
        public GameObject lockMark;     // 아직 못 얻음 표시 (자물쇠 아이콘)

        [System.NonSerialized] public string id;
        [System.NonSerialized] public int slot = -1;
    }
}
