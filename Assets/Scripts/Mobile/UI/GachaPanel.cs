using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 뽑기 패널: 광고 뽑기(11회, 광고를 끝까지 보면, 하루 한 번 — 한국 00시에 다시) · 재화 뽑기(11회) · 재화 뽑기(35회).
    // 11회·35회는 보너스 포함 (10+1, 30+5) — 비용은 10회분·30회분. 비용·재화는 임시값이다 (기획 확정 전)
    // kind = Character면 뽑은 개수만큼 꼬마를 같은 확률로 골라(등급 없음) Formation에 더한다. 무기·방어구는 장비(Equipment)로 바꿔 더한다.
    // 뽑은 뒤에는 GachaResultPopup으로 무엇이 나왔는지 칸을 하나씩 띄워 보여 준다.
    // 오른쪽 열은 GachaCardView로 꾸민다: 뽑기 레벨·경험치 막대 + 무료 소환(초록) · 재화 소환 11회/35회(보라, 재화 아이콘·가격)
    public class GachaPanel : MonoBehaviour
    {

        [SerializeField] GachaKind kind = GachaKind.Weapon;
        [FormerlySerializedAs("adButton")]
        [SerializeField] Button freeButton;         // 광고 뽑기 (뽑기상품 시트의 광고 상품)
        [SerializeField] Button coinButton11;       // 재화 뽑기 — 이 종류의 유료 상품 중 횟수가 적은 것
        [SerializeField] Button coinButton35;       // 재화 뽑기 — 횟수가 많은 것
        [FormerlySerializedAs("adLabel")]
        [SerializeField] TMP_Text freeLabel;
        [SerializeField] TMP_Text coinLabel11;
        [SerializeField] TMP_Text coinLabel35;
        [SerializeField] TMP_Text infoText;         // 예전 레벨·결과 글자 — 경험치 막대(GachaCardView)로 바꿔 숨긴다
        [SerializeField] TMP_Text titleText;        // 맨 위 띠(Name)의 뽑기 이름
        [SerializeField] Image previewImage;        // 왼쪽 큰 칸 안의 대표 그림(임시) — 그림만 종류에 맞게 바꾼다
        [SerializeField] Toast toast;               // 안내 문구 (없어도 된다)

        [Header("오른쪽 열 (프리팹에 배치)")]
        [SerializeField] GachaCardView.ExpRow expRow;
        [SerializeField] GachaCardView.ButtonText freeText, coin11Text, coin35Text;
        [SerializeField] GachaResultPopup resultPopup;   // 비우면 씬에서 찾는다

        bool adBusy;            // 광고를 띄우는 중 (연타 방지). 광고가 닫히거나 실패하면 풀린다
        GachaProductRow adProduct, smallProduct, bigProduct;   // 뽑기상품 시트 (없으면 그 버튼을 막는다)

        // 이 종류의 상품: 광고 하나, 유료는 횟수 순서로 앞의 둘
        void LoadProducts()
        {
            var paid = new System.Collections.Generic.List<GachaProductRow>();
            foreach (var p in GameBalance.GachaProducts)
            {
                if (p.kind != kind) continue;
                if (p.requiresAd) { if (adProduct == null) adProduct = p; }
                else paid.Add(p);
            }
            paid.Sort((a, b) => a.pullCount.CompareTo(b.pullCount));
            smallProduct = paid.Count > 0 ? paid[0] : null;
            bigProduct = paid.Count > 1 ? paid[1] : null;
        }

        int AdLimit => adProduct != null ? adProduct.dailyLimit : 0;
        bool FreeAvailable => adProduct != null && GachaProgress.FreeAvailable(kind, AdLimit);

        void Awake()
        {
            if (freeButton != null) freeButton.onClick.AddListener(OnFreeClicked);
            LoadProducts();
            if (coinButton11 != null) coinButton11.onClick.AddListener(() => PayAndRoll(smallProduct));
            if (coinButton35 != null) coinButton35.onClick.AddListener(() => PayAndRoll(bigProduct));
            if (titleText != null) titleText.text = Title(kind);
            ShowPreview();
            if (coin11Text != null && coin11Text.costIcon != null) coin11Text.costIcon.sprite = CostIcon(smallProduct);
            if (coin35Text != null && coin35Text.costIcon != null) coin35Text.costIcon.sprite = CostIcon(bigProduct);
            foreach (var b in new[] { freeButton, coinButton11, coinButton35 }) if (b != null) UiSkin.GreyWhenDisabled(b);
        }

        const string GemIcon = "UI/Icon/mycelium_stone";
        const string GoldIcon = "UI/Icon/gold";

        // 대표 그림 (임시): 장비는 가장 높은 등급 장비, 꼬마는 별똥 꼬마 3단계
        void ShowPreview()
        {
            if (previewImage == null) return;
            var sprite = kind == GachaKind.Character ? Formation.Icon(PreviewCharacter, Formation.EvoStageCount)
                : Equipment.Of(kind) is Equipment eq ? eq.Sprite(eq.Count - 1) : null;
            previewImage.sprite = sprite;
            previewImage.enabled = sprite != null;
        }

        const string PreviewCharacter = "star";

        void OnEnable()
        {
            Wallet.Changed += RefreshButtons;    // 전투 골드로 1초마다 온다 — 살 수 있는지만 다시 본다
            GachaProgress.Changed += Refresh;
            Loc.Changed += OnLanguageChanged;
            AdService.Status += OnAdStatus;
            AdService.Initialize();
            if (FreeAvailable) AdService.Prepare();     // 누를 때 광고가 준비돼 있게
            Refresh();
        }

        void OnDisable()
        {
            Wallet.Changed -= RefreshButtons;
            GachaProgress.Changed -= Refresh;
            Loc.Changed -= OnLanguageChanged;
            AdService.Status -= OnAdStatus;
        }

        // 언어가 바뀌면 제목·버튼 글자를 다시 쓴다
        void OnLanguageChanged()
        {
            if (titleText != null) titleText.text = Title(kind);
            Refresh();
        }

        void OnAdStatus(string message)
        {
            adBusy = false;
            Refresh();
        }

        void OnFreeClicked()
        {
            if (!FreeAvailable) { Notify(Loc.T("gacha.ad_used")); return; }
            if (adBusy) return;
            string credit = "Gacha." + kind;
            if (AdCredits.TryUse(credit))   // 지난번에 광고를 다 보고도 못 받은 뽑기
            {
                GachaProgress.MarkFreeUsed(kind);
                DoRoll(adProduct.pullCount);
                return;
            }
            adBusy = true;
            // 광고를 끝까지 봐야 소환한다. 보상 콜백은 한 번만 불린다 (그 안에서만 사용 처리·소환)
            bool started = AdService.ShowRewarded(AdPlacement.Gacha, () =>
            {
                adBusy = false;
                if (this == null || !isActiveAndEnabled) { AdCredits.Add(credit); return; }   // 광고 보는 사이 창이 닫혔다 → 다음 한 번 광고 없이
                if (!FreeAvailable) return;
                GachaProgress.MarkFreeUsed(kind);
                DoRoll(adProduct.pullCount);
            });
            if (!started)
            {
                adBusy = false;
                Notify(Loc.T("gacha.ad_loading"));
            }
        }

        void PayAndRoll(GachaProductRow product)
        {
            if (product == null) return;
            if (!Wallet.TrySpend(product.currency, product.price)) { Notify(Loc.T("gacha.no_funds")); return; }
            DoRoll(product.pullCount);
        }

        static Sprite CostIcon(GachaProductRow p) => Resources.Load<Sprite>(p != null && p.currency == Currency.Gold ? GoldIcon : GemIcon);

        static bool CanBuy(GachaProductRow p) => p != null && Wallet.CanAfford(p.currency, p.price);

        void DoRoll(int count)
        {
            var result = GachaProgress.Roll(kind, count);
            DailyQuests.Add(QuestKind.Gacha, count);
            GuideQuests.Report(GuideKind.Gacha, count);
            if (kind == GachaKind.Character)
            {
                var ids = GachaProgress.PickCharacters(result);
                Shuffle(ids);
                var entries = new List<GachaResultPopup.Entry>();
                var seen = new HashSet<string>();
                foreach (var id in ids)
                    entries.Add(new GachaResultPopup.Entry { icon = Formation.Icon(id), grade = 0, label = Formation.Name(id), isNew = !Formation.IsOwned(id) && seen.Add(id) });
                GachaProgress.Apply(kind, result);
                Formation.AddOwned(ids);
                ShowResult(entries);
            }
            else if (Equipment.Of(kind) is Equipment eq)
            {
                var items = eq.PickItems(result);
                Shuffle(items);
                var entries = new List<GachaResultPopup.Entry>();
                var seen = new HashSet<int>();
                foreach (int i in items)
                    entries.Add(new GachaResultPopup.Entry { icon = eq.Sprite(i), grade = eq.Grade(i), isNew = !eq.IsOwned(i) && seen.Add(i) });
                GachaProgress.Apply(kind, result);
                eq.AddItems(items);
                ShowResult(entries);
            }
            else
            {
                GachaProgress.Apply(kind, result);
                Notify(FormatResult(kind, count, result));
            }
            Refresh();
        }

        // 뽑기 연출 (Main 씬에 배치한 GachaResultPopup)
        void ShowResult(List<GachaResultPopup.Entry> entries)
        {
            if (resultPopup == null) resultPopup = FindFirstObjectByType<GachaResultPopup>(FindObjectsInactive.Include);
            if (resultPopup != null) resultPopup.Show(entries);
        }

        // 뽑은 순서를 섞어 보여 준다 (PickItems·PickCharacters는 낮은 등급부터 모아 돌려준다)
        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        static string FormatResult(GachaKind kind, int count, int[] result)
        {
            var sb = new StringBuilder(Loc.F("gacha.result", count));
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

        static string Title(GachaKind k) => k switch
        {
            GachaKind.Weapon => Loc.T("gacha.title_weapon"),
            GachaKind.Accessory => Loc.T("gacha.title_armor"),
            _ => Loc.T("gacha.title_mini"),
        };

        void Notify(string message)
        {
            if (toast != null) toast.Show(message, 2f);
            else Debug.Log($"[Gacha] {message}");
        }

        static void SetButton(GachaCardView.ButtonText b, string title, string sub)
        {
            if (b == null) return;
            if (b.title != null) b.title.text = title;
            if (b.sub != null) b.sub.text = sub;
        }

        void RefreshButtons() => RefreshButtons(FreeAvailable);

        void RefreshButtons(bool free)
        {
            if (freeButton != null) freeButton.interactable = free && !adBusy;
            if (coinButton11 != null) coinButton11.interactable = CanBuy(smallProduct);
            if (coinButton35 != null) coinButton35.interactable = CanBuy(bigProduct);
        }

        void Refresh()
        {
            bool free = FreeAvailable;
            // [UI 개편 2026-11-05] 한 줄 "광고 보고 11회 소환"(앞은 연한 노랑·뒤는 흰색). 다 보면 이름은 그대로 두고 버튼만 회색(interactable)
            string freeTitle = adProduct == null ? "-"
                : Loc.F("gacha.ad_pull", adProduct.pullCount) + (AdLimit > 0 ? $" {AdLimit - GachaProgress.FreeLeft(kind, AdLimit)}/{AdLimit}" : "");   // 본 횟수/하루 횟수 (안 봤으면 0/n, 다 보면 n/n·회색)
            SetButton(freeText, freeTitle, "");
            SetButton(coin11Text, smallProduct != null ? Loc.F("gacha.pull", smallProduct.pullCount) : "-", smallProduct != null ? Wallet.Format(smallProduct.price) : "");
            SetButton(coin35Text, bigProduct != null ? Loc.F("gacha.pull", bigProduct.pullCount) : "-", bigProduct != null ? Wallet.Format(bigProduct.price) : "");

            RefreshButtons(free);

            if (expRow != null)
            {
                int level = GachaProgress.Level(kind);
                GachaCardView.SetExp(expRow, level, GachaProgress.Exp(kind), GachaProgress.ExpToNext(level));
            }
        }
    }
}
