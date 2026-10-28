using UnityEngine;

namespace MoreMush.Mobile
{
    // 팝업 프리팹 자리. 씬에는 팝업을 두지 않고, 처음 Get할 때 parent 아래에 프리팹을 만든다(닫힌 상태). 이후엔 같은 것을 돌려준다.
    // Created는 만들어진 직후 한 번 불린다 — 팝업 이벤트 구독 같은 초기화를 여기서 한다.
    [System.Serializable]
    public class PopupSlot
    {
        [SerializeField] UiPopup prefab;

        UiPopup instance;

        public event System.Action<UiPopup> Created;

        // 아직 안 만들었으면 닫힌 것으로 본다
        public bool IsOpen => instance != null && instance.IsOpen;

        public UiPopup Get(Transform parent)
        {
            if (instance != null) return instance;
            instance = Object.Instantiate(prefab, parent, false);
            instance.name = prefab.name;
            instance.gameObject.SetActive(false);
            Created?.Invoke(instance);
            return instance;
        }

        public void Close()
        {
            if (instance != null) instance.Close();
        }
    }
}
