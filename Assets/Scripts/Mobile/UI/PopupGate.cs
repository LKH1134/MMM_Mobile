using UnityEngine;

namespace MoreMush.Mobile
{
    // 팝업은 한 번에 하나만: 새 팝업이 열리면(Opened) 열려 있던 팝업을 닫는다.
    // UiPopup·편성 안 장비 탭(TabGroup popups)·꼬마 고르기·방치 정산이 여기에 알린다. Main 하단 탭을 바꾸면 CloseAll
    public static class PopupGate
    {
        static Object owner;
        static System.Action closeOwner;
        static bool ownerClosesOnBack;

        // backCloses = false: 뒤로가기로 닫히지 않는 팝업 (닫기 버튼이 없는 결과 팝업 등) — 뒤로가기는 먹고 아무것도 안 한다
        public static void Opened(Object who, System.Action close, bool backCloses = true)
        {
            if (owner != who) CloseCurrent();
            owner = who;
            closeOwner = close;
            ownerClosesOnBack = backCloses;
        }

        public static void Closed(Object who)
        {
            if (owner != who) return;
            owner = null;
            closeOwner = null;
        }

        // 뒤로가기(BackButton): 열린 팝업이 있으면 닫고 true. 없으면 false (씬이 처리한다)
        public static bool HandleBack()
        {
            if (owner == null) return false;
            if (ownerClosesOnBack) CloseCurrent();
            return true;
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
