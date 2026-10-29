using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 뽑기 패널: 광고 뽑기(11회, 하루 한 번) · 재화 뽑기(11회) · 재화 뽑기(35회).
    // 11회·35회는 보너스 포함 (10+1, 30+5) — 비용은 10회분·30회분. 비용·재화는 임시값이다 (기획 확정 전)
    public class GachaPanel : MonoBehaviour
    {
        public const int Count11 = 11;
        public const int Count35 = 35;

        // 광고 SDK를 붙이면 이 핸들러를 채운다: 광고를 보여 주고 끝나면 콜백(보상 지급 여부)을 부른다.
        // 비어 있으면 에디터·개발 빌드에서만 바로 지급한다
        public static Action<Action<bool>> RewardedAdHandler;

        [SerializeField] GachaKind kind = GachaKind.Weapon;
        [SerializeField] Button adButton;           // 광고 뽑기(11회)
        [SerializeField] Button coinButton11;       // 재화 뽑기(11회)
        [SerializeField] Button coinButton35;       // 재화 뽑기(35회)
        [SerializeField] TMP_Text adLabel;
        [SerializeField] TMP_Text coinLabel11;
        [SerializeField] TMP_Text coinLabel35;
        [SerializeField] TMP_Text infoText;         // 레벨·경험치·마지막 결과 (없어도 된다)
        [SerializeField] Toast toast;               // 안내 문구 (없어도 된다)
        [SerializeField] Currency currency = Currency.Mycelite;
        [SerializeField] long cost11 = 100;
        [SerializeField] long cost35 = 300;

        string lastResult = "";
        bool adPlaying;

        void Awake()
        {
            if (adButton != null) adButton.onClick.AddListener(OnAdClicked);
            if (coinButton11 != null) coinButton11.onClick.AddListener(() => PayAndRoll(Count11, cost11));
            if (coinButton35 != null) coinButton35.onClick.AddListener(() => PayAndRoll(Count35, cost35));
        }

        void OnEnable()
        {
            Wallet.Changed += Refresh;
            GachaProgress.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            Wallet.Changed -= Refresh;
            GachaProgress.Changed -= Refresh;
        }

        void OnAdClicked()
        {
            if (adPlaying) return;
            if (!GachaProgress.AdAvailable(kind)) { Notify("오늘의 광고 뽑기를 이미 사용했습니다"); return; }

            if (RewardedAdHandler != null)
            {
                adPlaying = true;
                RewardedAdHandler(rewarded =>
                {
                    adPlaying = false;
                    if (rewarded) GrantAdRoll();
                    else Notify("광고를 끝까지 봐야 보상을 받을 수 있습니다");
                });
                return;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GrantAdRoll();
#else
            Notify("광고를 준비 중입니다");
#endif
        }

        void GrantAdRoll()
        {
            GachaProgress.MarkAdUsed(kind);
            DoRoll(Count11);
        }

        void PayAndRoll(int count, long cost)
        {
            if (!Wallet.TrySpend(currency, cost)) { Notify("재화가 부족합니다"); return; }
            DoRoll(count);
        }

        void DoRoll(int count)
        {
            var result = GachaProgress.Roll(kind, count);
            GachaProgress.Apply(kind, result);
            lastResult = FormatResult(count, result);
            Refresh();
            Notify(lastResult);
        }

        static string FormatResult(int count, int[] result)
        {
            var sb = new StringBuilder($"{count}회 결과: ");
            bool first = true;
            for (int g = GachaProgress.GradeCount - 1; g >= 0; g--)
            {
                if (result[g] <= 0) continue;
                if (!first) sb.Append(" / ");
                sb.Append(GachaProgress.GradeNames[g]).Append(' ').Append(result[g]);
                first = false;
            }
            return sb.ToString();
        }

        void Notify(string message)
        {
            if (toast != null) toast.Show(message, 2f);
            else Debug.Log($"[Gacha] {message}");
        }

        void Refresh()
        {
            if (adLabel != null) adLabel.text = GachaProgress.AdAvailable(kind) ? $"광고 뽑기 ({Count11}회)\n무료" : $"광고 뽑기 ({Count11}회)\n내일 다시";
            if (coinLabel11 != null) coinLabel11.text = $"재화 뽑기 ({Count11}회)\n{Wallet.Format(cost11)}";
            if (coinLabel35 != null) coinLabel35.text = $"재화 뽑기 ({Count35}회)\n{Wallet.Format(cost35)}";

            if (adButton != null) adButton.interactable = GachaProgress.AdAvailable(kind);
            if (coinButton11 != null) coinButton11.interactable = Wallet.CanAfford(currency, cost11);
            if (coinButton35 != null) coinButton35.interactable = Wallet.CanAfford(currency, cost35);

            if (infoText != null)
            {
                int level = GachaProgress.Level(kind), need = GachaProgress.ExpToNext(level);
                string exp = need > 0 ? $"{GachaProgress.Exp(kind)}/{need}" : "MAX";
                infoText.text = $"Lv.{level} ({exp})" + (lastResult.Length > 0 ? "\n" + lastResult : "");
            }
        }
    }
}
