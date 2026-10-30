using UnityEngine;

namespace MoreMush.Mobile
{
    // 팝업은 한 번에 하나만: 새 팝업이 열리면(Opened) 열려 있던 팝업을 닫는다.
    // UiPopup·편성 안 장비 탭(TabGroup popups)·꼬마 고르기·방치 정산이 여기에 알린다. Main 하단 탭을 바꾸면 CloseAll
    public static class PopupGate
    {
        static Object owner;
        static System.Action closeOwner;

        public static void Opened(Object who, System.Action close)
        {
            if (owner != who) CloseCurrent();
            owner = who;
            closeOwner = close;
        }

        public static void Closed(Object who)
        {
            if (owner != who) return;
            owner = null;
            closeOwner = null;
        }

        public static void CloseAll() => CloseCurrent();

        static void CloseCurrent()
        {
            var close = closeOwner;
            bool alive = owner != null;     // 씬이 바뀌어 사라진 팝업이면 부르지 않는다
            owner = null;
            closeOwner = null;
            if (alive) close?.Invoke();
        }
    }
}
