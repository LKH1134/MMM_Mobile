using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 버섯 특성 화면 (프리팹 MushroomTrait, Equit 자식). 팝업이 아니다 —
    // 편성 화면 [버섯 특성] 버튼을 누르면 편성 화면 안쪽만 이 화면으로 바뀐다 (FormationScreenView가 켜고 끄고, 아래 세 버튼을 돌아가기·초기화·Lv업으로 바꾼다):
    //   왼쪽: 고른 꼬마 그림·레벨 + 패시브 3칸 (이름 / 스킬 그림 · 효과). 데이터는 꼬마스킬 시트(GgomaSkills). 해금 전은 회색 판 + 해금 조건
    //   오른쪽: 특성 나무 (위아래 스크롤, 열 때마다 맨 아래부터). 칸 = 그림 + 올린 값/최대. 칸을 누르면 위 설명줄에 지금 → 다음 효과와 값이 뜬다.
    //   Lv업 = 고른 칸을 골드 + 균사석으로 한 칸 올림. 초기화 = 균사석을 내고 모두 0으로 (MushroomTraits)
    public class MushroomTraitPanelView : MonoBehaviour
    {
        [System.Serializable]
        public class PassiveSlot
        {
            public GameObject root;
            public TMP_Text nameText;
            public Image icon;
            public TMP_Text effectText;
            public GameObject lockMark;     // 해금 전에 칸을 덮는 회색 판 (안에 해금 조건 글자)
            public TMP_Text lockText;
        }

        [System.Serializable]
        public class TraitNode
        {
            public Button button;
            public TMP_Text countText;
            public GameObject selection;
            public string effectName = "공격력";
            public float perLevel = 4f;      // 한 칸 올릴 때 늘어나는 %
            public int max = 5;
            public int tier = 1;             // 1(맨 아래) … 6(맨 위). 클수록 올리는 값이 비싸다
            public int[] requires = new int[0];   // 선이 이어진 아래쪽 칸 번호들. 이 중 하나라도 1 이상이어야 이 칸을 올릴 수 있다 (비면 처음부터 열림)
        }

        // 프리팹을 설계한 크기. 자리(region)에 맞춰 같은 비율로 줄이거나 늘린다
        public static readonly Vector2 DesignSize = new Vector2(1250f, 1450f);

        [Header("꼬마")]
        [SerializeField] Image ggomaIcon;
        [SerializeField] TMP_Text ggomaLevel;
        [SerializeField] PassiveSlot[] passives = new PassiveSlot[0];
        [SerializeField] Sprite[] effectIcons = new Sprite[0];     // GgomaSkillEffect 순서

        [Header("특성 나무")]
        [SerializeField] GameObject tip;
        [SerializeField] TMP_Text tipNow;
        [SerializeField] TMP_Text tipNext;
        [SerializeField] TMP_Text tipCost;
        [SerializeField] TraitNode[] nodes = new TraitNode[0];

        // 고른 칸·재화·레벨이 바뀌었다 (편성 화면이 아래 버튼을 켜고 끈다)
        public event System.Action StateChanged;

        static readonly Color LockedTint = new Color(0.62f, 0.62f, 0.62f, 1f);

        string ggomaId;
        int picked = -1;
        ScrollRect treeScroll;

        void Awake()
        {
            treeScroll = GetComponentInChildren<ScrollRect>(true);
            // 특성 칸이 아닌 빈 곳(나무 바탕 · 화면 바탕)을 누르면 고른 칸을 푼다
            if (treeScroll != null) treeScroll.gameObject.AddComponent<ClickCatcher>().Clicked += Deselect;
            var block = transform.Find("Block");
            if (block != null) block.gameObject.AddComponent<ClickCatcher>().Clicked += Deselect;
            for (int i = 0; i < nodes.Length; i++)
            {
                int index = i;
                if (nodes[i].button != null) nodes[i].button.onClick.AddListener(() => PickNode(index));
            }
        }

        void OnEnable()
        {
            Wallet.Changed += RefreshTree;
            Refresh();
        }

        void OnDisable() => Wallet.Changed -= RefreshTree;

        // worldMin·worldMax = 이 화면이 차지할 자리의 월드 좌표 모서리. 가운데에 맞추고 같은 비율로 줄여 넣는다
        public void Open(string id, Vector3 worldMin, Vector3 worldMax)
        {
            var rt = (RectTransform)transform;
            var parent = (RectTransform)rt.parent;
            Vector2 a = parent.InverseTransformPoint(worldMin), b = parent.InverseTransformPoint(worldMax);
            Vector2 size = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = DesignSize;
            rt.anchoredPosition = (a + b) * 0.5f - parent.rect.center;
            float scale = Mathf.Min(size.x / DesignSize.x, size.y / DesignSize.y);
            rt.localScale = new Vector3(scale, scale, 1f);

            picked = -1;
            ggomaId = id;
            gameObject.SetActive(true);
            Refresh();
            if (treeScroll != null)       // 열 때마다 맨 아래(첫 특성)에서 시작한다
            {
                Canvas.ForceUpdateCanvases();
                treeScroll.StopMovement();
                treeScroll.verticalNormalizedPosition = 0f;
            }
            StateChanged?.Invoke();
        }

        public void Close() => gameObject.SetActive(false);

        // 열린 채로 고른 꼬마가 바뀌면 패시브 칸만 다시 그린다
        public void SetGgoma(string id)
        {
            if (id == ggomaId) return;
            ggomaId = id;
            Refresh();
        }

        // ── 특성 올리기 · 초기화 ──────────────────────────────

        int LevelOf(int i) => Mathf.Clamp(MushroomTraits.Level(i), 0, nodes[i].max);
        long MyceliteCost(int i) => MushroomTraits.MyceliteCost(nodes[i].tier, LevelOf(i));

        // 아래쪽 이어진 칸을 하나라도 올렸으면 열린다
        bool Unlocked(int i)
        {
            var req = nodes[i].requires;
            if (req == null || req.Length == 0) return true;
            foreach (int r in req) if (r >= 0 && r < nodes.Length && LevelOf(r) > 0) return true;
            return false;
        }

        public bool CanLevelUp
        {
            get
            {
                if (picked < 0 || LevelOf(picked) >= nodes[picked].max || !Unlocked(picked)) return false;
                return Wallet.CanAfford(Currency.Mycelite, MyceliteCost(picked));
            }
        }

        // 고른 칸을 올리는 값. 칸을 안 골랐거나 최대치면 false
        public bool TryGetPickCost(out long mycelite)
        {
            mycelite = 0;
            if (picked < 0 || LevelOf(picked) >= nodes[picked].max || !Unlocked(picked)) return false;
            mycelite = MyceliteCost(picked);
            return true;
        }

        public bool CanReset => MushroomTraits.AnyLevel(nodes.Length) && Wallet.CanAfford(Currency.Mycelite, MushroomTraits.ResetMycelite);

        public bool LevelUp()
        {
            if (!CanLevelUp) return false;
            int i = picked, level = LevelOf(i);
            if (!Wallet.TrySpend(Currency.Mycelite, MyceliteCost(i))) return false;
            MushroomTraits.SetLevel(i, level + 1);
            Refresh();
            StateChanged?.Invoke();
            return true;
        }

        public bool ResetAll()
        {
            if (!CanReset || !Wallet.TrySpend(Currency.Mycelite, MushroomTraits.ResetMycelite)) return false;
            MushroomTraits.ResetAll(nodes.Length);
            Refresh();
            StateChanged?.Invoke();
            return true;
        }

        // ── 화면 ──────────────────────────────────────────────

        void Deselect()
        {
            if (picked < 0) return;
            picked = -1;
            RefreshTree();
            StateChanged?.Invoke();
        }

        void PickNode(int index)
        {
            picked = picked == index ? -1 : index;
            RefreshTree();
            StateChanged?.Invoke();
        }

        void Refresh()
        {
            RefreshGgoma();
            RefreshTree();
        }

        void RefreshGgoma()
        {
            bool has = !string.IsNullOrEmpty(ggomaId);
            if (ggomaIcon != null)
            {
                ggomaIcon.sprite = has ? Formation.Icon(ggomaId) : null;
                ggomaIcon.enabled = has;
            }
            if (ggomaLevel != null) ggomaLevel.text = has ? "Lv." + Mathf.Max(1, Formation.Level(ggomaId)) : "";

            var rows = has ? GgomaSkills.Of(ggomaId) : System.Array.Empty<GgomaSkillRow>();
            for (int i = 0; i < passives.Length; i++)
            {
                var p = passives[i];
                var row = i < rows.Count ? rows[i] : null;
                if (p.root != null) p.root.SetActive(row != null);
                if (row == null) continue;
                if (p.nameText != null) p.nameText.text = string.Format(Loc.T("trait.passive_stage", "{0}단계 패시브"), row.stage);   // 표에 키가 생기면 그 문구를 쓴다
                if (p.effectText != null) p.effectText.text = GgomaSkills.EffectText(row);
                int effect = (int)row.effect;
                if (p.icon != null) p.icon.sprite = effect < effectIcons.Length ? effectIcons[effect] : null;
                if (p.lockMark != null) p.lockMark.SetActive(!GgomaSkills.IsUnlocked(row));   // 진화 단계가 모자라면 회색 판으로 덮는다
                if (p.lockText != null) p.lockText.text = string.Format(Loc.T("trait.unlock_stage", "{0}단계 진화 시 해금"), row.stage);
            }
        }

        void RefreshTree()
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                if (n.countText != null) n.countText.text = LevelOf(i) + "/" + n.max;
                if (n.selection != null) n.selection.SetActive(i == picked);
                if (n.button != null)       // 아직 못 올리는 칸은 어둡게
                {
                    var tint = Unlocked(i) ? Color.white : LockedTint;
                    foreach (var img in n.button.GetComponentsInChildren<Image>(true))
                        if (img.gameObject != n.selection) img.color = tint;
                }
            }
            if (tip != null) tip.SetActive(picked >= 0);
            if (picked < 0) return;
            var node = nodes[picked];
            int level = LevelOf(picked);
            if (tipNow != null) tipNow.text = $"{node.effectName} +{level * node.perLevel:0.#}%";
            if (tipNext != null) tipNext.text = $"{node.effectName} +{System.Math.Min(node.max, level + 1) * node.perLevel:0.#}%";
            if (tipCost != null)
                tipCost.text = level >= node.max ? "MAX"
                    : !Unlocked(picked) ? Loc.T("trait.need_lower", "아래 특성을 먼저 올리세요")
                    : string.Format(Loc.T("trait.cost", "균사석 {0}"), Wallet.Format(MyceliteCost(picked)));
        }
    }
}
