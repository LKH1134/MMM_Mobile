using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // [UI 개편 2026-11-06] 세계수 성장 시트 (프리팹 GrowPopup의 Sheet/Page/Content/GrowScreen). 1/3 · 2/3으로 나눈 화면:
    //   왼쪽 = 세계수 그림·단계 띠 / 현재 효과(아래 2/3)
    //   오른쪽 = 네 칸(각 1/4 높이): 진화 · 나이테(성장) · 공물(바치기) · 세계수의 가호(목록 → 가호 메뉴판 팝업 BlessPopup)
    // 모양은 프리팹에서 고치고 값과 버튼만 여기서 넣는다. 이 화면이 있으면 예전 TreeGrowPanel은 꺼 둔다
    public class TreeGrowScreen : MonoBehaviour
    {
        [Header("왼쪽: 세계수")]
        [SerializeField] Image treeImage;
        [SerializeField] TMP_Text treeTitle;        // "4단계 버섯 나무"
        [SerializeField] TMP_Text effects;          // 현재 효과 목록

        [Header("오른쪽: 진화")]
        [SerializeField] Image evolveIcon;          // 다음 단계 나무
        [SerializeField] TMP_Text evolveTitle;      // "세계수 진화 → 5단계"
        [SerializeField] RectTransform evolveFill;
        [SerializeField] TMP_Text evolveExp;
        [SerializeField] TMP_Text evolveInfo;       // 조건·열리는 것
        [SerializeField] Button evolveButton;
        [SerializeField] TMP_Text evolveLabel;
        [SerializeField] GameObject evolveGoldRow;      // 버튼 안 비용 줄: 골드 아이콘 + 값
        [SerializeField] TMP_Text evolveGoldText;
        [SerializeField] GameObject evolveMyceliteRow;  // 균사석 아이콘 + 값 (필요 없으면 숨김)
        [SerializeField] TMP_Text evolveMyceliteText;

        [Header("오른쪽: 나이테")]
        [SerializeField] TMP_Text ringLevel;
        [SerializeField] RectTransform ringFill;
        [SerializeField] TMP_Text ringDesc;
        [SerializeField] Button ringButton;
        [SerializeField] TMP_Text ringLabel;
        [SerializeField] GameObject ringCostRow;        // 버튼 안 비용 줄: 골드 아이콘 + 값 (최대면 숨김)
        [SerializeField] TMP_Text ringCostText;

        [Header("오른쪽: 공물")]
        [SerializeField] TMP_Text tributeDesc;
        [SerializeField] TMP_Text tributePrefer;
        [SerializeField] Button tributeButton;
        [SerializeField] TMP_Text tributeLabel;
        [SerializeField] RectTransform tributeItems;    // 선호 버섯 칸들 (가로 묶음)
        [SerializeField] GameObject tributeItemTemplate;  // 칸 견본: Icon · Count(오른쪽 아래 ×n) · Name(아래 가운데)

        [Header("오른쪽: 가호")]
        [SerializeField] TMP_Text blessCount;
        [SerializeField] TMP_Text blessNext;
        [SerializeField] Button blessButton;

        [Header("가호 메뉴판 팝업 (GrowPopup/BlessPopup) — 줄 견본: 가호 줄 Badge/Stage·Effect, 제목 줄, 창고 줄 Icon·Name·Count")]
        [SerializeField] GameObject listPanel;
        [SerializeField] RectTransform listContent;
        [SerializeField] GameObject blessRowTemplate;
        [SerializeField] TMP_Text headerTemplate;
        [SerializeField] GameObject storeRowTemplate;
        [SerializeField] Button listClose;

        readonly List<GameObject> rows = new List<GameObject>();
        readonly List<GameObject> tributeCells = new List<GameObject>();
        static Sprite onSprite, offSprite;
        bool dirty;
        float tick;

        void Awake()
        {
            if (blessRowTemplate != null) blessRowTemplate.SetActive(false);
            if (headerTemplate != null) headerTemplate.gameObject.SetActive(false);
            if (storeRowTemplate != null) storeRowTemplate.SetActive(false);
            if (tributeItemTemplate != null) tributeItemTemplate.SetActive(false);
            if (listPanel != null) listPanel.SetActive(false);
            if (evolveButton != null) { UiSkin.GreyWhenDisabled(evolveButton); evolveButton.onClick.AddListener(Evolve); }
            if (ringButton != null) { UiSkin.GreyWhenDisabled(ringButton); ringButton.onClick.AddListener(RingUp); }
            if (tributeButton != null) { UiSkin.GreyWhenDisabled(tributeButton); tributeButton.onClick.AddListener(Tribute); }
            if (blessButton != null) blessButton.onClick.AddListener(() => ShowList(true));
            if (listClose != null) listClose.onClick.AddListener(() => ShowList(false));
            // 팝업 바깥 어두운 곳(Dim)을 눌러도 닫힌다
            var dim = listPanel != null ? listPanel.transform.Find("Dim") : null;
            if (dim != null && dim.TryGetComponent<Button>(out var dimButton)) dimButton.onClick.AddListener(() => ShowList(false));
        }

        void OnEnable()
        {
            WorldTree.Changed += MarkDirty;
            Wallet.Changed += MarkDirty;
            Formation.Changed += MarkDirty;
            Refresh();
        }

        void OnDisable()
        {
            WorldTree.Changed -= MarkDirty;
            Wallet.Changed -= MarkDirty;
            Formation.Changed -= MarkDirty;
            ShowList(false);
        }

        void MarkDirty() => dirty = true;

        // 값이 바뀌면 바로, 아니면 5초마다 다시 쓴다 (세계수 씬에서만 열려 처치로 창고 수가 바뀌지 않는다)
        void LateUpdate()
        {
            tick -= Time.unscaledDeltaTime;
            if (!dirty && tick > 0f) return;
            tick = 5f;
            Refresh();
        }

        void Refresh()
        {
            bool changed = dirty || !refreshedOnce;   // 1초 틱만으로는 목록을 다시 만들지 않는다 (값이 바뀐 때만)
            refreshedOnce = true;
            dirty = false;
            var row = WorldTree.CurrentRow;
            bool max = WorldTree.IsMaxStage;

            // 세계수
            if (treeImage != null) { treeImage.sprite = WorldTree.TreeSprite(WorldTree.Stage); treeImage.preserveAspect = true; treeImage.enabled = treeImage.sprite != null; }
            if (treeTitle != null) treeTitle.text = $"{WorldTree.Stage}단계 {row.name}";
            if (effects != null) effects.text = EffectsText(row);

            // 진화
            if (evolveTitle != null) evolveTitle.text = max ? "최고 단계" : $"세계수 진화 → {WorldTree.Stage + 1}단계";
            if (evolveIcon != null) { evolveIcon.sprite = WorldTree.TreeSprite(max ? WorldTree.Stage : WorldTree.Stage + 1); evolveIcon.preserveAspect = true; evolveIcon.enabled = evolveIcon.sprite != null; }
            float exp = max ? 1f : WorldTree.EvoExp > 0 ? Mathf.Clamp01((float)(WorldTree.Exp / WorldTree.EvoExp)) : 1f;
            SetFill(evolveFill, exp);
            if (evolveExp != null) evolveExp.text = max ? "MAX" : $"경험치 {Wallet.Format((long)WorldTree.Exp)}/{Wallet.Format(WorldTree.EvoExp)}";
            if (evolveInfo != null) evolveInfo.text = max ? "" : EvolveInfo(row);
            if (evolveButton != null)
            {
                evolveButton.gameObject.SetActive(!max);
                evolveButton.interactable = !max && WorldTree.CanEvolve() == WorldTree.GrowFail.None;
            }
            if (evolveLabel != null) evolveLabel.text = "진화";
            if (evolveGoldRow != null) evolveGoldRow.SetActive(!max);
            if (evolveGoldText != null) evolveGoldText.text = Wallet.Format(WorldTree.EvoGold);
            if (evolveMyceliteRow != null) evolveMyceliteRow.SetActive(!max && WorldTree.EvoMycelite > 0);
            if (evolveMyceliteText != null) evolveMyceliteText.text = Wallet.Format(WorldTree.EvoMycelite);

            // 나이테
            if (ringLevel != null) ringLevel.text = $"Lv. {WorldTree.Ring} / {WorldTree.RingMax}";
            SetFill(ringFill, WorldTree.RingMax > 0 ? Mathf.Clamp01(WorldTree.Ring / (float)WorldTree.RingMax) : 1f);
            if (ringDesc != null) ringDesc.text = WorldTree.IsMaxRing ? "최대 Lv" : "";
            if (ringButton != null) ringButton.interactable = !WorldTree.IsMaxRing && WorldTree.CanRingUp() == WorldTree.GrowFail.None;
            if (ringLabel != null) ringLabel.text = WorldTree.IsMaxRing ? "최대" : "성장";
            if (ringCostRow != null) ringCostRow.SetActive(!WorldTree.IsMaxRing);
            CenterWhenNoCost(ringLabel, WorldTree.IsMaxRing, ref ringLabelMin);
            if (ringCostText != null) ringCostText.text = Wallet.Format(WorldTree.RingCost);

            // 공물
            if (tributeDesc != null) tributeDesc.text = $"공물 <size=70%>(창고 버섯 {Wallet.Format(WorldTree.StoreTotal)}개)</size>";   // 칸 제목 줄
            if (tributePrefer != null) tributePrefer.text = "선호 버섯 <size=80%>(×" + GameBalance.Const("tributePreferMul").ToString("0.#") + ")</size>";
            RefreshTributeItems(row);
            if (tributeButton != null) tributeButton.interactable = WorldTree.CanTribute;
            if (tributeLabel != null) tributeLabel.text = WorldTree.TributePreview > 0 ? $"바치기\n<size=60%>경험치 +{Wallet.Format((long)WorldTree.TributePreview)}</size>" : "바치기";

            // 가호
            int got = 0, all = 0;
            BlessRow next = null;
            foreach (var b in GameBalance.Blessings)
            {
                all++;
                if (WorldTree.BlessActive(b)) got++;
                else if (next == null || b.stage < next.stage) next = b;
            }
            if (blessCount != null) blessCount.text = $"세계수의 가호 <size=70%>({got}/{all})</size>";   // 칸 제목 줄 (자세한 건 목록)
            if (blessNext != null) blessNext.text = next != null ? $"다음 {next.stage}단계: {WorldTree.EffectText(next.effect, next.value)}" : "모든 가호를 받았어요";

            if (changed && listPanel != null && listPanel.activeSelf) FillList();   // 목록은 통째로 다시 만든다 — 바뀌었을 때만
        }

        bool refreshedOnce;

        // 비용 줄이 숨겨지면(최대 등) 버튼 글자를 세로 가운데로, 다시 보이면 프리팹 자리(위쪽)로
        Vector2? ringLabelMin;
        static void CenterWhenNoCost(TMP_Text label, bool noCost, ref Vector2? original)
        {
            if (label == null) return;
            var r = label.rectTransform;
            if (original == null) original = r.anchorMin;
            r.anchorMin = noCost ? new Vector2(original.Value.x, 0f) : original.Value;
        }

        static void SetFill(RectTransform fill, float v)
        {
            if (fill == null) return;
            fill.anchorMax = new Vector2(Mathf.Clamp01(v), fill.anchorMax.y);
            fill.gameObject.SetActive(v > 0f);
        }

        // 지금 받는 효과: 효과 합계(가호·꼬마 특기·장식) + 이 단계의 제작 슬롯·장식 칸
        static string EffectsText(TreeStageRow row)
        {
            var sb = new StringBuilder();
            foreach (TreeEffect e in System.Enum.GetValues(typeof(TreeEffect)))
            {
                double v = WorldTree.Bonus(e);
                if (v <= 0) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(WorldTree.EffectText(e, v));
            }
            int deco = row.rootSlots + row.trunkSlots + row.branchSlots + row.crownSlots;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append($"제작 슬롯 {row.craftSlots} · 장식 칸 {deco}");
            return sb.ToString();
        }

        // 진화 칸 본문: 비용(골드·균사석)은 버튼에, 여기는 조건(나이테·경험치)과 진화하면 좋아지는 것만
        static string EvolveInfo(TreeStageRow row)
        {
            var sb = new StringBuilder("요구: ");
            sb.Append(TreeUi.Check(WorldTree.IsMaxRing, $"나이테 Lv.{WorldTree.RingMax}"));
            if (WorldTree.Stage < GameBalance.TreeStages.Count)
            {
                var next = GameBalance.TreeStages[WorldTree.Stage];
                var gain = new StringBuilder();
                if (next.craftSlots > row.craftSlots) gain.Append($"제작 슬롯 {row.craftSlots}→{next.craftSlots}  ");
                int a = row.rootSlots + row.trunkSlots + row.branchSlots + row.crownSlots;
                int b = next.rootSlots + next.trunkSlots + next.branchSlots + next.crownSlots;
                if (b > a) gain.Append($"장식 칸 {a}→{b}  ");
                foreach (var bless in GameBalance.Blessings)
                    if (bless.stage == next.stage) gain.Append("가호 ").Append(WorldTree.EffectText(bless.effect, bless.value));
                if (gain.Length > 0) sb.Append("\n").Append(gain.ToString().TrimEnd());
            }
            return sb.ToString();
        }

        // 공물 칸: 이 단계가 좋아하는 버섯 그림 + 창고 개수(오른쪽 아래 ×n) + 이름(아래 가운데)
        void RefreshTributeItems(TreeStageRow row)
        {
            if (tributeItems == null || tributeItemTemplate == null) return;
            int n = row.preferIds != null ? row.preferIds.Length : 0;
            while (tributeCells.Count < n)
            {
                var go = Instantiate(tributeItemTemplate, tributeItems);
                tributeCells.Add(go);
            }
            for (int i = 0; i < tributeCells.Count; i++)
            {
                var cell = tributeCells[i];
                bool on = i < n;
                cell.SetActive(on);
                if (!on) continue;
                int id = row.preferIds[i];
                var icon = cell.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null) { icon.sprite = WorldTree.EnemySprite(id); icon.preserveAspect = true; icon.enabled = icon.sprite != null; }
                SetText(cell.transform, "Count", "×" + Wallet.Format(WorldTree.StoreCount(id)));
                SetText(cell.transform, "Name", WorldTree.EnemyName(id));
            }
        }

        static string PreferNames(TreeStageRow row)
        {
            if (row.preferIds == null || row.preferIds.Length == 0) return "-";
            var sb = new StringBuilder();
            foreach (int id in row.preferIds)
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append(WorldTree.EnemyName(id));
            }
            return sb.ToString();
        }

        // ── 가호·창고 목록 창 ──
        void ShowList(bool on)
        {
            if (listPanel == null) return;
            listPanel.SetActive(on);
            if (on) FillList();
        }

        void FillList()
        {
            if (listContent == null) return;
            foreach (var r in rows) if (r != null) Destroy(r);
            rows.Clear();
            if (onSprite == null) onSprite = Resources.Load<Sprite>("UI/UI_PP_In_Color_Beige1");
            if (offSprite == null) offSprite = Resources.Load<Sprite>("UI/UI_PP_In_Color_Grey");

            // 가호: 받은 것은 베이지, 아직은 회색 칸
            foreach (var b in GameBalance.Blessings)
            {
                if (blessRowTemplate == null) break;
                bool on = WorldTree.BlessActive(b);
                var row = Add(blessRowTemplate);
                var bg = row.GetComponent<Image>();
                if (bg != null) bg.sprite = on ? onSprite : offSprite;
                SetText(row.transform, "Badge/Stage", $"{b.stage}단계");
                SetText(row.transform, "Effect", WorldTree.EffectText(b.effect, b.value) + (on ? "" : "  <size=80%>(잠김)</size>"));
            }

            // 창고 버섯
            if (headerTemplate != null)
            {
                var h = Add(headerTemplate.gameObject);
                h.GetComponent<TMP_Text>().text = $"창고 버섯 ({Wallet.Format(WorldTree.StoreTotal)})";
            }
            if (storeRowTemplate == null) return;
            if (WorldTree.StoreTotal <= 0)
            {
                var row = Add(storeRowTemplate);
                SetText(row.transform, "Name", "방치 전투에서 버섯을 잡으면 창고에 쌓여요");
                SetText(row.transform, "Count", "");
                var ic = row.transform.Find("Icon"); if (ic != null) ic.gameObject.SetActive(false);
            }
            foreach (var e in GameBalance.Enemies)
            {
                long n = WorldTree.StoreCount(e.index);
                if (n <= 0) continue;
                var row = Add(storeRowTemplate);
                var icon = row.transform.Find("Icon")?.GetComponent<Image>();
                if (icon != null) { icon.sprite = WorldTree.EnemySprite(e.index); icon.preserveAspect = true; icon.enabled = icon.sprite != null; }
                SetText(row.transform, "Name", e.name + (WorldTree.Prefers(e.index) ? TreeUi.Colored("  선호", TreeUi.Good) : ""));
                SetText(row.transform, "Count", "×" + Wallet.Format(n));
            }
        }

        GameObject Add(GameObject template)
        {
            var go = Instantiate(template, listContent);
            go.SetActive(true);
            rows.Add(go);
            return go;
        }

        static void SetText(Transform t, string path, string text)
        {
            var c = t.Find(path)?.GetComponent<TMP_Text>();
            if (c != null) c.text = text;
        }

        // ── 버튼 ──
        void RingUp()
        {
            if (WorldTree.RingUp()) Toast($"나이테 Lv {WorldTree.Ring}!");
            Refresh();
        }

        void Tribute()
        {
            double exp = WorldTree.Tribute();
            if (exp > 0) Toast($"경험치 +{Wallet.Format((long)exp)}");
            Refresh();
        }

        void Evolve()
        {
            if (WorldTree.Evolve()) Toast($"세계수가 {WorldTree.CurrentRow.name}(으)로 자랐어요!");
            Refresh();
        }

        static void Toast(string message)
        {
            var screen = FindAnyObjectByType<WorldTreeScreen>();
            if (screen != null) screen.ShowToast(message);
        }
    }
}
