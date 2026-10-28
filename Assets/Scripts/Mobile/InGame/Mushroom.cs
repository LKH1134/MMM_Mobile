using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 버섯. 단일 개체는 maxHp 1(한 번 맞으면 캠), 군락 개체는 maxHp를 높여 체력이 다 닳을 때까지 맞아야 캔다.
    // 종류마다 프리팹을 따로 두고, HarvestField가 프리팹별 풀에서 꺼내 놓는다.
    // 꺼낸 뒤 Setup으로 등급(체력 배율·색)·황금 변종·크기를 정한다 — 임시로 그림 색만 바꿔 구분한다
    [RequireComponent(typeof(RectTransform))]
    public class Mushroom : MonoBehaviour, IPoolable
    {
        [SerializeField] int maxHp = 1;
        [SerializeField] long reward = 10;            // 캤을 때 오르는 기본 점수
        [SerializeField] int drop = 1;                // 캤을 때 얻는 기본 수확량(버섯 개수)
        [SerializeField] float radius = 50f;          // 충돌 반지름 (크기 1일 때)
        [SerializeField] RectTransform visual;        // 맞을 때 출렁이는 그림
        [SerializeField] GameObject hpRoot;           // 체력 바. 다치기 전엔 숨긴다. 비우면 표시 없음
        [SerializeField] RectTransform hpFill;        // 체력 바 안쪽 채움. 앵커 x 최대값 = 남은 비율

        RectTransform rect;
        Graphic tintTarget;
        // 출렁임: 코루틴 대신 Update에서 진행한다. 측정기는 한 프레임 안에서 수천 판을 돌리는데,
        // 끝나지 못한 코루틴을 맞을 때마다 멈추고 새로 걸면 Unity 안쪽에 쌓여 판이 점점 느려졌다
        float punchTime = -1f;                        // 0 이상이면 진행 중
        float punchDuration, punchFrom, punchTo;
        float hpMul = 1f;
        float scale = 1f;

        public int MaxHp => Mathf.Max(1, Mathf.RoundToInt(maxHp * hpMul));
        public int Hp { get; private set; }
        public bool Alive => Hp > 0;
        public long Reward => reward;
        public int Drop => drop;
        public float Radius => radius * scale;
        public float BaseRadius => radius;
        public MushroomTier Tier { get; private set; }
        public bool Golden { get; private set; }
        public int Colony { get; set; } = -1;         // 속한 군락지 번호 (HarvestField가 쓴다)
        public bool IsCenter { get; set; }            // 군락지 가운데 군락 버섯 ("군락 충돌" 스킬이 이걸 본다)
        // 보스 판의 보스 (관통되지 않고, 보스 피해가 붙는다). 보스는 머리 위 작은 체력 바 대신 BossHud가 화면 위에 보여 준다
        public bool IsBoss
        {
            get => isBoss;
            set { isBoss = value; ShowHp(); }
        }

        bool isBoss;
        public Vector2 Position => rect.anchoredPosition;
        internal PrefabPool<Mushroom> Pool { get; set; }

        void Awake()
        {
            rect = (RectTransform)transform;
            if (visual != null) tintTarget = visual.GetComponent<Graphic>();
        }

        // 등급·황금 변종·크기를 정한다. 꺼낸 직후(OnSpawned 다음)에 부른다. 체력 = 기본 체력 × hpMultiplier (등급 × 스테이지 계수)
        public void Setup(MushroomTier tier, float hpMultiplier, bool golden, float size, Color tint)
        {
            Tier = tier;
            Golden = golden;
            hpMul = Mathf.Max(0.0001f, hpMultiplier);
            scale = size;
            rect.localScale = new Vector3(size, size, 1f);
            if (tintTarget != null) tintTarget.color = tint;
            Hp = MaxHp;
            ShowHp();
        }

        public void OnSpawned()
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            hpMul = 1f;
            scale = 1f;
            rect.localScale = Vector3.one;
            Tier = MushroomTier.Normal;
            Golden = false;
            Colony = -1;
            IsCenter = false;
            IsBoss = false;
            if (tintTarget != null) tintTarget.color = Color.white;
            Hp = MaxHp;
            ShowHp();
            if (visual != null) visual.localScale = Vector3.one;
            Pop(0.18f, 0f, 1f);
        }

        public void OnDespawned()
        {
            punchTime = -1f;
        }

        public void Place(Vector2 position) => rect.anchoredPosition = position;

        // 맞으면 체력을 깎는다. 이번에 캐졌으면 true
        public bool Hit(int damage)
        {
            if (!Alive) return false;
            Hp = Mathf.Max(0, Hp - Mathf.Max(1, damage));
            ShowHp();
            if (Alive) Pop(0.15f, 0.8f, 1f);
            return !Alive;
        }

        // 체력 회복 (보스 기믹). 최대 체력을 넘지 않는다
        public void Heal(int amount)
        {
            if (!Alive || amount <= 0) return;
            Hp = Mathf.Min(MaxHp, Hp + amount);
            ShowHp();
        }

        void ShowHp()
        {
            if (hpRoot == null) return;
            hpRoot.SetActive(!isBoss && Hp < MaxHp && Hp > 0);
            if (hpFill != null) hpFill.anchorMax = new Vector2((float)Hp / MaxHp, hpFill.anchorMax.y);
        }

        // from에서 1.15배로 부풀었다가 to로 돌아온다 (다시 걸면 처음부터)
        void Pop(float duration, float from, float to)
        {
            if (visual == null || !isActiveAndEnabled) return;
            punchDuration = duration;
            punchFrom = from;
            punchTo = to;
            punchTime = 0f;
            ShowPunch();
        }

        void Update()
        {
            if (punchTime < 0f) return;
            punchTime += Time.deltaTime;
            ShowPunch();
        }

        void ShowPunch()
        {
            if (punchTime >= punchDuration)
            {
                visual.localScale = Vector3.one * punchTo;
                punchTime = -1f;
                return;
            }
            float k = punchTime / punchDuration;
            float s = k < 0.5f ? Mathf.Lerp(punchFrom, 1.15f, k * 2f) : Mathf.Lerp(1.15f, punchTo, k * 2f - 1f);
            visual.localScale = Vector3.one * s;
        }
    }
}
