using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // [UI 개편 2026-11-05] 세계수 꼬마 시트의 보유 꼬마 칸 (프리팹 KidPopup 안 KidCell). 값은 TreeKidScreen이 넣는다.
    // Badges(세로 묶음) 안에 EvolveBadge
    public class KidCellView : MonoBehaviour
    {
        public Button button;
        public Image icon;
        public TMP_Text levelText;
        public TMP_Text nameText;
        public GameObject evolveBadge;   // [진화 가능] 파란 칸
        public GameObject selection;     // 고른 꼬마 표시

        [System.NonSerialized] public string id;
    }
}
