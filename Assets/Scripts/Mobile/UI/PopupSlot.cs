using UnityEngine;
using UnityEngine.Serialization;

namespace MoreMush.Mobile
{
    // 팝업 자리: 씬에 배치해 꺼 둔 팝업(UiPopup)을 가리킨다. 모양·자리는 씬(프리팹 인스턴스)에서 고친다.
    // Created는 처음 Get할 때 한 번 불린다 — 팝업 이벤트 구독 같은 초기화를 여기서 한다.
    [System.Serializable]
    public class PopupSlot
    {
        [FormerlySerializedAs("prefab")]
        [SerializeField] UiPopup popup;

        bool initialized;

        public event System.Action<UiPopup> Created;

        public bool IsOpen => popup != null && popup.IsOpen;

        // parent는 예전 호환용 (팝업은 이미 씬의 제자리에 있다)
        public UiPopup Get(Transform parent = null)
        {
            if (!initialized && popup != null)
            {
                initialized = true;
                Created?.Invoke(popup);
            }
            return popup;
        }

        public void Close()
        {
            if (popup != null) popup.Close();
        }
    }
}
