using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 룰렛 화면 (프리팹 RouletteScreen, 가운데 창). 원판 6칸의 보상과 남은 횟수·다음 돌리기까지 남은 시간을 보여 주고,
    // "돌리기"를 누르면 원판이 돌다가 위쪽 핀이 가리키는 칸이 결과가 된다 (Roulette.TrySpin이 칸을 먼저 정한다).
    // 돌리는 중에 창이 닫히면 그 자리에서 보상을 받는다 (OnDisable). 모양·자리는 프리팹에서 고친다
    [RequireComponent(typeof(UiPopup))]
    public class RouletteScreenView : MonoBehaviour
    {
        [System.Serializable]
        public class SliceView
        {
            public RectTransform root;
            public Image icon;
            public TMP_Text amount;
        }

        [SerializeField] RectTransform wheel;
        [SerializeField] RectTransform pin;                  // 위쪽 핀 (칸 경계를 지날 때 까딱인다). 비워도 된다
        [SerializeField] SliceView[] slices;                 // 0번(맨 위)부터 시계 방향
        [SerializeField] TMP_Text remainText;
        [SerializeField] TMP_Text timerText;
        [SerializeField] Button spinButton;
        [SerializeField] TMP_Text spinLabel;
        [SerializeField] Button closeButton;
        [SerializeField] GameObject resultBox;               // 결과 안내 (원판 가운데 위)
        [SerializeField] Image resultIcon;
        [SerializeField] TMP_Text resultText;

        [Header("그림 (Resources 경로)")]
        [SerializeField] string goldIcon = "UI/Icon/gold";
        [SerializeField] string myceliteIcon = "UI/Icon/mycelium_stone";

        [Header("회전")]
        [SerializeField] float spinSeconds = 4.2f;
        [SerializeField] int extraTurns = 5;                 // 멈추기 전에 도는 바퀴 수
        [SerializeField] float resultSeconds = 2.2f;         // 결과 안내가 떠 있는 시간

        const float SliceAngle = 360f / Roulette.SliceCount;

        UiPopup popup;
        bool spinning;
        float wheelZ;
        int shownSecond = -1;
        Coroutine hideResult;

        void Awake()
        {
            popup = GetComponent<UiPopup>();
            if (spinButton != null)
            {
                spinButton.onClick.AddListener(Spin);
                UiSkin.GreyWhenDisabled(spinButton);
            }
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            // 꺼진 채로 만들어져 아직 Awake 전일 수 있다. Open이 켜면서 Awake가 돈다
            if (popup == null) popup = GetComponent<UiPopup>();
            popup.Open();
        }

        void Close()
        {
            if (spinning) return;       // 돌아가는 동안은 못 닫는다 (바깥·뒤로가기로 닫히면 OnDisable이 보상을 준다)
            popup.Close();
        }

        void OnEnable()
        {
            RefreshSlices();
            if (Roulette.HasPending) ShowResult(Roulette.ClaimPending());     // 지난번에 돌리다 꺼졌으면 지금 받는다
            Refresh();
        }

        void OnDisable()
        {
            if (spinning)
            {
                spinning = false;
                Roulette.ClaimPending();
            }
            if (pin != null) pin.localRotation = Quaternion.identity;
            if (resultBox != null) resultBox.SetActive(false);
        }

        // 칸마다 그림과 수량 (골드는 지금 스테이지 기준이라 열 때마다 다시 쓴다)
        void RefreshSlices()
        {
            if (slices == null) return;
            for (int i = 0; i < slices.Length && i < Roulette.Slices.Length; i++)
            {
                var v = slices[i];
                if (v == null) continue;
                var kind = Roulette.Slices[i].kind;
                if (v.icon != null) v.icon.sprite = Resources.Load<Sprite>(kind == RouletteKind.Gold ? goldIcon : myceliteIcon);
                if (v.amount != null) v.amount.text = Wallet.Format(Roulette.AmountOf(i));
            }
        }

        void Spin()
        {
            if (spinning || !Roulette.TrySpin(out int slice)) { Refresh(); return; }
            spinning = true;
            if (resultBox != null) resultBox.SetActive(false);
            Refresh();
            StartCoroutine(SpinRoutine(slice));
        }

        IEnumerator SpinRoutine(int slice)
        {
            // 한 칸 안에서 조금 비껴 서게 (칸 가장자리 바로 앞까지는 가지 않는다)
            float jitter = Random.Range(-0.38f, 0.38f) * SliceAngle;
            float start = wheelZ;
            // 칸 번호 k는 시계 방향 k×60°에 있다 → 원판을 반시계로 k×60° 돌려 맨 위로 온다 (z 값이 그만큼 커진다).
            // 원판이 시계 방향(z 감소)으로 도는 연출이라 시작값보다 extraTurns바퀴 이상 작은 같은 자리를 목표로 한다
            float baseAngle = slice * SliceAngle + jitter;
            int turns = Mathf.CeilToInt((baseAngle - start + 360f * extraTurns) / 360f);
            float target = baseAngle - 360f * turns;
            float last = start;
            for (float t = 0f; t < spinSeconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / spinSeconds);
                float eased = 1f - Mathf.Pow(1f - k, 3f);
                wheelZ = Mathf.Lerp(start, target, eased);
                ApplyWheel(last);
                last = wheelZ;
                yield return null;
            }
            wheelZ = target;
            ApplyWheel(last);
            if (pin != null) pin.localRotation = Quaternion.identity;
            spinning = false;
            ShowResult(Roulette.ClaimPending());
            Refresh();
        }

        void ApplyWheel(float previousZ)
        {
            if (wheel != null) wheel.localRotation = Quaternion.Euler(0f, 0f, wheelZ);
            if (pin == null) return;
            // 핀 끝이 칸 경계(30°+60°×n)를 지나는 순간 까딱: 경계를 넘은 직후 몇 도 눌렸다가 돌아온다
            float pos = (-wheelZ + SliceAngle * 0.5f) / SliceAngle;
            float frac = pos - Mathf.Floor(pos);
            float tilt = frac < 0.3f && wheelZ < previousZ ? Mathf.Lerp(16f, 0f, frac / 0.3f) : 0f;
            pin.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        void ShowResult((int slice, RouletteKind kind, long amount) r)
        {
            if (r.slice < 0 || resultBox == null) return;
            if (resultIcon != null) resultIcon.sprite = Resources.Load<Sprite>(r.kind == RouletteKind.Gold ? goldIcon : myceliteIcon);
            if (resultText != null) resultText.text = Loc.F(r.kind == RouletteKind.Gold ? "roulette.result_gold" : "roulette.result_mycelite", Wallet.Format(r.amount));
            resultBox.SetActive(true);
            if (hideResult != null) StopCoroutine(hideResult);
            hideResult = StartCoroutine(HideResultLater());
        }

        IEnumerator HideResultLater()
        {
            yield return new WaitForSecondsRealtime(resultSeconds);
            hideResult = null;
            if (resultBox != null) resultBox.SetActive(false);
        }

        void Update()
        {
            if (spinning) return;
            // 남은 시간 글자는 1초마다 (대기가 끝나는 순간 돌리기가 켜진다)
            int sec = (int)Roulette.CooldownLeft;
            if (sec == shownSecond) return;
            shownSecond = sec;
            Refresh();
        }

        void Refresh()
        {
            int left = Roulette.Remaining;
            double cool = Roulette.CooldownLeft;
            shownSecond = (int)cool;
            bool can = !spinning && Roulette.CanSpin;
            if (remainText != null) remainText.text = Loc.F("roulette.remain", left, Roulette.PerDay);
            if (spinLabel != null) spinLabel.text = Loc.F("roulette.spin", left, Roulette.PerDay);
            if (spinButton != null) spinButton.interactable = can;
            if (closeButton != null) closeButton.interactable = !spinning;
            if (timerText == null) return;
            if (spinning) timerText.text = "";
            else if (left <= 0) timerText.text = Loc.F("roulette.done_today", Format(DungeonTickets.UntilReset));
            else if (cool > 0) timerText.text = Loc.F("roulette.wait", Format(System.TimeSpan.FromSeconds(System.Math.Ceiling(cool))));
            else timerText.text = Loc.T("roulette.ready");
        }

        static string Format(System.TimeSpan t) =>
            t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";
    }
}
