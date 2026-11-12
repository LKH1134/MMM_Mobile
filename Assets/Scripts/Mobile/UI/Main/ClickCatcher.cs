using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MoreMush.Mobile
{
    // 빈 바탕을 눌렀을 때만 알린다 (드래그한 뒤에는 누름으로 치지 않는다 — Unity가 알아서 거른다).
    // 버튼 같은 앞쪽 UI가 먼저 받으면 여기까지 오지 않는다
    public class ClickCatcher : MonoBehaviour, IPointerClickHandler
    {
        public event Action Clicked;

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke();
    }
}
