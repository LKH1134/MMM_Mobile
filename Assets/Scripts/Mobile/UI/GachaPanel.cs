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
    // kind = Character면 등급을 뽑은 뒤 그 등급의 꼬마로 바꿔 Formation에 더한다. 무기·방어구·펫은 장비(Equipment)로 바꿔 더한다.
    // 뽑은 뒤에는 GachaResultPopup으로 무엇이 나왔는지 칸을 하나씩 띄워 보여 준다.
    // 오른쪽 열은 GachaCardView로 꾸민다: 뽑기 레벨·경험치 막대 + 무료 소환(초록) · 재화 소환 11회/35회(보라, 재화 아이콘·가격)
    public class GachaPanel : MonoBehaviour
    {
        public const int Count11 = 11;
        static int AdPulls => Mathf.Max(1, GameBalance.ConstInt("adGachaPulls"));     // 광고 뽑기 한 번에 나오는 수 (상수 시트, 유료 11·35회와 따로)
        public const int Count35 = 35;

        [SerializeField] GachaKind kind = GachaKind.Weapon;
        [FormerlySerializedAs("adButton")]
        [SerializeField] Button freeButton;         // 광고 뽑기(11회, 광고를 보면 하루 한 번)
        [SerializeField] Button coinButton11;       // 재화 뽑기(11회)
        [SerializeField] Button coinButton35;       // 재화 뽑기(35회)
        [FormerlySerializedAs("adLabel")]
        [SerializeField] TMP_Text freeLabel;
        [SerializeField] TMP_Text coinLabel11;
        [SerializeField] TMP_Text coinLabel35;
        [SerializeField] TMP_Text infoText;         // 예전 레벨·결과 글자 — 경험치 막대(GachaCardView)로 바꿔 숨긴다
        [SerializeField] TMP_Text titleText;        // 맨 위 띠의 뽑기 이름. 비우면 "Name" 띠 안에 만든다
        [SerializeField] Image previewFrame;        // 왼쪽 큰 칸 — 이 뽑기의 대표 그림(임시)을 안에 띄운다. 비우면 "Image" 칸을 쓴다
        [SerializeField] Toast toast;               // 안내 문구 (없어도 된다)
        [SerializeField] Currency currency = Currency.Mycelite;
        [SerializeField] long cost11 = 100;
        [SerializeField] long cost35 = 300;

        GachaResultPopup resultPopup;
        bool adBusy;            // 광고를 띄우는 중 (연타 방지). 광고가 닫히거나 실패하면 풀린다
        GachaCardView.ExpRow expRow;
        GachaCardView.ButtonText freeText, coin11Text, coin35Text;

        void Awake()
        {
            if (freeButton != null) freeButton.onClick.AddListener(OnFreeClicked);
            if (coinButton11 != null) coinButton11.onClick.AddListener(() => PayAndRoll(Count11, cost11));
            if (coinButton35 != null) coinButton35.onClick.AddListener(() => PayAndRoll(Count35, cost35));
            if (titleText == null) titleText = CreateTitle(transform.Find("Name"));
            if (titleText != null) titleText.text = Title(kind);
            if (previewFrame == null) previewFrame = transform.Find("Image")?.GetComponent<Image>();
            ShowPreview();
            BuildRightColumn();
        }

        void BuildRightColumn()
        {
            var font = titleText != null ? titleText.font : GetComponentInChildren<TMP_Text>(true)?.font;
            if (infoText != null) infoText.gameObject.SetActive(false);
            GachaCardView.Layout(freeButton, coinButton11, coinButton35);
            expRow = GachaCardView.BuildExp((RectTransform)transform, font);
            var costIcon = Resources.Load<Sprite>(currency == Currency.Mycelite ? GemIcon : GoldIcon);
            freeText = GachaCardView.Style(freeButton, freeLabel, GachaCardView.FreeSprite, GachaCardView.FreeColor, null, font);
            coin11Text = GachaCardView.Style(coinButton11, coinLabel11, GachaCardView.CoinSprite, GachaCardView.CoinColor, costIcon, font);
            coin35Text = GachaCardView.Style(coinButton35, coinLabel35, GachaCardView.CoinSprite, GachaCardView.CoinColor, costIcon, font);
        }

        const string GemIcon = "Art/Dream/Icons/gem";
        const string GoldIcon = "Art/Dream/Icons/gold";

        // 대표 그림 (임시): 장비는 가장 높은 등급 장비, 꼬마는 영웅 꼬마
        void ShowPreview()
        {
            if (previewFrame == null) return;
            var sprite = kind == GachaKind.Character ? Formation.Icon(PreviewCharacter)
                : Equipment.Of(kind) is Equipment eq ? eq.Sprite(eq.Count - 1) : null;
            if (sprite == null) return;
            var img = new GameObject("Preview", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(previewFrame.transform, false);
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(20f, 20f);
            rt.offsetMax = new Vector2(-20f, -20f);
        }

        const string PreviewCharacter = "star";

        // 왼쪽 위 리본(Name 이미지)을 꽉 채우는 글자. 글꼴은 이 패널의 다른 글자에서 가져온다
        TMP_Text CreateTitle(Transform bar)
        {
            if (bar == null) return null;
            var label = GetComponentInChildren<TMP_Text>(true);
            var t = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(bar, false);
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(20f, 2f);
            rt.offsetMax = new Vector2(-20f, -2f);
            if (label != null) t.font = label.font;
            t.color = Color.white;              // Name 띠는 카드 그림 위 반투명 어두운 리본
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(40, 28, 20, 255);
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.enableAutoSizing = true;
            t.fontSizeMin = 20f;
            t.fontSizeMax = 40f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        void OnEnable()
        {
            Wallet.Changed += Refresh;
            GachaProgress.Changed += Refresh;
            AdService.Status += OnAdStatus;
            AdService.Initialize();
            if (GachaProgress.FreeAvailable(kind)) AdService.Prepare();     // 누를 때 광고가 준비돼 있게
            Refresh();
        }

        void OnDisable()
        {
            Wallet.Changed -= Refresh;
            GachaProgress.Changed -= Refresh;
            AdService.Status -= OnAdStatus;
        }

        void OnAdStatus(string message)
        {
            adBusy = false;
            Refresh();
        }

        void OnFreeClicked()
        {
            if (!GachaProgress.FreeAvailable(kind)) { Notify("오늘의 광고 뽑기를 이미 사용했습니다"); return; }
            if (adBusy) return;
            adBusy = true;
            // 광고를 끝까지 봐야 소환한다. 보상 콜백은 한 번만 불린다 (그 안에서만 사용 처리·소환)
            bool started = AdService.ShowRewarded(AdPlacement.Gacha, () =>
            {
                adBusy = false;
                if (this == null || !GachaProgress.FreeAvailable(kind)) return;
                GachaProgress.MarkFreeUsed(kind);
                DoRoll(AdPulls);
            });
            if (!started)
            {
                adBusy = false;
                Notify("광고를 불러오는 중입니다. 잠시 후 다시 눌러 주세요");
            }
        }

        void PayAndRoll(int count, long cost)
        {
            if (!Wallet.TrySpend(currency, cost)) { Notify("재화가 부족합니다"); return; }
            DoRoll(count);
        }

        void DoRoll(int count)
        {
            var result = GachaProgress.Roll(kind, count);
            DailyQuests.Add(QuestKind.Gacha, count);
            if (kind == GachaKind.Character)
            {
                var ids = GachaProgress.PickCharacters(result);
                Shuffle(ids);
                var entries = new List<GachaResultPopup.Entry>();
                var seen = new HashSet<string>();
                foreach (var id in ids)
                    entries.Add(new GachaResultPopup.Entry { icon = Formation.Icon(id), grade = Formation.Grade(id), isNew = !Formation.IsOwned(id) && seen.Add(id) });
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
                    entries.Add(new GachaResultPopup.Entry { icon = eq.Sprite(i), grade = Equipment.Grade(i), isNew = !eq.IsOwned(i) && seen.Add(i) });
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

        // 뽑기 연출 (GachaResultPopup). 처음 쓸 때 캔버스 맨 위에 만들고, 글꼴은 이 패널 글자에서 가져온다
        void ShowResult(List<GachaResultPopup.Entry> entries)
        {
            if (resultPopup == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                var label = GetComponentInChildren<TMP_Text>(true);
                resultPopup = GachaResultPopup.Create(canvas != null ? canvas.rootCanvas.transform : transform, label != null ? label.font : null);
            }
            resultPopup.Show(entries);
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

        static string Title(GachaKind k) => k switch
        {
            GachaKind.Weapon => "수확기 뽑기",
            GachaKind.Accessory => "방어구 뽑기",
            GachaKind.Pet => "펫 뽑기",
            _ => "꼬마 뽑기",
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

        void Refresh()
        {
            bool free = GachaProgress.FreeAvailable(kind);
            SetButton(freeText, $"{AdPulls}회 소환", free ? "광고 시청" : "내일 다시");
            SetButton(coin11Text, $"{Count11}회 소환", Wallet.Format(cost11));
            SetButton(coin35Text, $"{Count35}회 소환", Wallet.Format(cost35));

            if (freeButton != null) freeButton.interactable = free && !adBusy;
            if (coinButton11 != null) coinButton11.interactable = Wallet.CanAfford(currency, cost11);
            if (coinButton35 != null) coinButton35.interactable = Wallet.CanAfford(currency, cost35);

            if (expRow != null)
            {
                int level = GachaProgress.Level(kind);
                GachaCardView.SetExp(expRow, level, GachaProgress.Exp(kind), GachaProgress.ExpToNext(level));
            }
        }
    }
}
