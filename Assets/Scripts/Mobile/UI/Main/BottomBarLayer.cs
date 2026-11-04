using UnityEngine;

namespace MoreMush.Mobile
{
    // 하단 공용 바를 화면 내용(전투·강화 노트·편성·던전·뽑기)보다 늘 앞에 그린다 (PopupLayer.BottomBar).
    // 아래 X가 있는 팝업형 화면(PopupLayer.Panel·Popup)은 이보다 위라서 하단 바를 덮는다. 하단 바 오브젝트에 붙인다
    public class BottomBarLayer : MonoBehaviour
    {
        void Awake() => PopupLayer.Raise(gameObject, PopupLayer.BottomBar);
    }
}
