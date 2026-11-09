using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 방치 정산 팝업: 홈으로 돌아오면 비운 동안 번 골드·균사석·처치 수·넘긴 스테이지·앱 밖 보상을 보여 준다.
    // Main 씬 캔버스에 배치(꺼 둔 IdleReportPopup — 자기 캔버스로 다른 UI 위 층). 화면은 안의 IdleReportView 프리팹에서 고친다.
    // 왼쪽 "확인"은 닫는다. 오른쪽 "광고 보고 2배 보상"은 리워드 광고를 끝까지 보면 방금 받은 골드·균사석을 한 번 더 준다 (이미 지갑에 들어 있으므로 같은 양을 더 넣는다)
    public class IdleReportPopup : MonoBehaviour
    {
        [SerializeField] IdleReportView view;
        [SerializeField] TMP_Text title;            // 제목 (알림 때 바꾼다)
        [SerializeField] GameObject list;           // 정산 줄 묶음 (알림 때 숨긴다)
        [SerializeField] TMP_Text messageText;      // 알림 본문 (정산 줄 자리)

        Button doubleButton;
        IdleAway.Report report;     // 지금 보이는 정산
        bool doubled;               // 2배 보상을 이미 받았다

        public void Show(IdleAway.Report r)
        {
            Init();
            SetTitle(Loc.T("idle.title"));
            SetMessageMode(false);
            report = r;
            doubled = false;
            Render();
            if (doubleButton != null)
            {
                doubleButton.interactable = true;
                doubleButton.gameObject.SetActive(r.gold + r.offlineGold + r.mycelite > 0);
            }
            AdService.Initialize();
            AdService.Prepare();            // 누를 때 광고가 준비돼 있게
            PopupGate.Opened(this, Hide);
            gameObject.SetActive(true);
        }

        // 알림만 보여 준다 (방치 화면 던전 결과 등): 제목·본문 + 확인. 2배 버튼은 숨긴다
        public bool IsOpen => gameObject.activeSelf;
        string pendingTitle, pendingBody;
        System.Action closed;   // 지금 안내가 닫힐 때 할 일   // 정산 창이 열려 있을 때 온 알림 — 닫은 뒤 보여 준다

        // onClosed: 확인으로(또는 다른 팝업에 밀려) 닫힐 때 한 번 (오류 안내 뒤 다시 시작 등)
        public void ShowMessage(string heading, string message, System.Action onClosed = null)
        {
            Init();
            if (IsOpen && onClosed == null) { pendingTitle = heading; pendingBody = message; return; }
            if (IsOpen) { report = null; pendingTitle = pendingBody = null; }   // 닫힐 때 할 일이 있는 안내는 지금 띄운다
            closed = onClosed;
            report = null;
            SetTitle(heading);
            SetMessageMode(true);
            if (messageText != null) messageText.text = message;
            if (doubleButton != null) doubleButton.gameObject.SetActive(false);
            PopupGate.Opened(this, Hide);
            gameObject.SetActive(true);
        }

        void SetTitle(string heading)
        {
            if (title != null) title.text = heading;
        }

        // 프리팹 화면에서 알림일 때: 정산 줄(List)·시간 줄을 숨기고 그 자리에 여러 줄 본문을 보인다
        void SetMessageMode(bool on)
        {
            if (list != null) list.SetActive(!on);
            if (view != null && view.timeText != null) view.timeText.gameObject.SetActive(!on);
            if (messageText != null) messageText.gameObject.SetActive(on);
        }

        bool initialized;

        // 처음 쓸 때 한 번 (꺼 둔 채 배치돼 Show가 Awake보다 먼저 올 수 있다)
        void Awake() => Init();

        void Init()
        {
            if (initialized || view == null) return;
            initialized = true;
            if (view.okButton != null) view.okButton.onClick.AddListener(Hide);
            doubleButton = view.doubleButton;
            if (doubleButton != null)
            {
                UiSkin.GreyWhenDisabled(doubleButton);
                doubleButton.onClick.AddListener(WatchForDouble);
            }
        }

        void RenderView()
        {
            var r = report;
            long m = doubled ? 2 : 1;
            if (view.timeText != null) view.timeText.text = Loc.F("idle.away", FormatTime(r.seconds + r.offlineSeconds));
            if (view.goldText != null) view.goldText.text = "+" + Wallet.Format(Times(IdleProgress.SaturatingAdd(r.gold, r.offlineGold), m));
            if (view.goldSubText != null)
            {
                view.goldSubText.gameObject.SetActive(r.offlineGold > 0);
                view.goldSubText.text = Loc.F("idle.offline_sub", FormatTime(r.offlineSeconds), Wallet.Format(Times(r.offlineGold, m)));
            }
            if (view.myceliteRow != null) view.myceliteRow.SetActive(r.mycelite > 0);
            if (view.myceliteText != null) view.myceliteText.text = "+" + Wallet.Format(Times(r.mycelite, m));
            var info = new System.Collections.Generic.List<string>();
            if (r.kills > 0) info.Add(Loc.F("idle.kills", r.kills.ToString("N0")));
            if (r.stages > 0) info.Add(Loc.F("idle.stage_up", r.stages, IdleBattle.HudStageNumber(IdleProgress.Stage)));   // HUD와 같은 "7-7" 표기
            if (view.infoRow != null) view.infoRow.SetActive(info.Count > 0);
            if (view.infoText != null) view.infoText.text = string.Join("  ·  ", info);
            if (view.doubledRow != null) view.doubledRow.SetActive(doubled);
        }

        void Render()
        {
            if (view != null) RenderView();
        }

        // 광고를 끝까지 보면 같은 양을 한 번 더 지갑에 넣고 화면도 2배로 바꾼다 (한 번만)
        void WatchForDouble()
        {
            if (doubled || report == null) return;
            doubleButton.interactable = false;      // 광고가 뜨기 전에 또 누르지 않게
            var watched = report;
            bool given = false;
            bool started = AdService.ShowRewarded(AdPlacement.OfflineDouble, () =>
            {
                if (given) return;
                given = true;
                // 보상부터 넣는다. 광고를 보는 사이 씬이 바뀌어 이 창이 사라졌어도 보상은 받는다
                Wallet.Add(Currency.Gold, IdleProgress.SaturatingAdd(watched.gold, watched.offlineGold));
                Wallet.Add(Currency.Mycelite, watched.mycelite);
                if (this == null || report != watched) return;
                Hide();     // 광고를 다 보고 보상을 받았으면 정산 창도 같이 닫는다
            });
            if (!started) doubleButton.interactable = true;
        }

        void Hide()
        {
            PopupGate.Closed(this);
            report = null;
            gameObject.SetActive(false);
            var onClosed = closed;
            closed = null;
            if (onClosed != null) { onClosed(); return; }
            if (pendingBody == null) return;
            string t = pendingTitle, b = pendingBody;
            pendingTitle = pendingBody = null;
            ShowMessage(t, b);
        }

        // 표시용 곱하기 (아주 높은 스테이지에서 long이 넘쳐 음수로 보이지 않게)
        static long Times(long v, long m) => m == 2 ? IdleProgress.SaturatingAdd(v, v) : v;

        static string FormatTime(double seconds)
        {
            int s = Mathf.Max(0, (int)seconds);
            if (s >= 3600) return Loc.F("time.hm", s / 3600, s % 3600 / 60);
            if (s >= 60) return Loc.F("time.ms", s / 60, s % 60);
            return Loc.F("time.s", s);
        }
    }
}
