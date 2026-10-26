using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 수확 필드. 수확기·버섯을 풀에서 꺼내 놓고, 매 프레임 수확기를 움직이며 벽·바·버섯과의 충돌을 계산한다.
    // 좌표는 이 RectTransform 로컬(가운데 기준, 캔버스 픽셀). 상하좌우 가장자리 모두 벽이라 수확기는 빠져나가지 않는다.
    // 수확기는 판 시작 때 한 번 던지면 시간이 끝날 때까지 같은 속도로 튕겨 다닌다.
    // 버섯은 군락지 단위로 돋는다: 가운데 군락 버섯 1개 + 둘레의 단일 버섯 몇 개. 군락지 하나는 모두 같은 등급이다.
    // 군락지의 버섯을 다 캐면 군락지가 사라지고, 정해진 시간 뒤 빈 자리에 새 군락지가 돋는다.
    // 군락지가 많아 필드에 다 안 들어가면 버섯 크기를 줄여서 넣는다
    [RequireComponent(typeof(RectTransform))]
    public class HarvestField : MonoBehaviour
    {
        [System.Serializable]
        public class TierLook
        {
            public MushroomTier tier;
            public int hpMul = 1;                     // 체력 배율
            public float scoreMul = 1f;               // 점수 배율
            public Color tint = Color.white;          // 임시 구분용 색 (등급 그림이 생기면 바꾼다)
        }

        [Header("수확기")]
        [SerializeField] Harvester harvesterPrefab;
        [SerializeField] RectTransform harvesterLayer;
        [SerializeField] float speed = 2200f;         // 날아가는 속도(픽셀/초). 감속 없이 계속 이 속도다
        [SerializeField, Range(0f, 0.7f)] float minVertical = 0.25f;   // 세로 속도가 최소 이 비율은 되게 한다 (좌우로만 오가며 갇히지 않게)
        [SerializeField] bool pierce = true;          // 한 방에 캐진 버섯은 튕기지 않고 뚫고 지나간다

        [Header("바")]
        [SerializeField] Bar bar;
        [SerializeField] float restGap = 4f;          // 바 위에 놓일 때 띄울 거리
        [SerializeField] float maxBarRatio = 0.6f;    // 바 길이 상한 (필드 폭 대비)

        [Header("버섯")]
        [SerializeField] Mushroom clusterPrefab;      // 군락지 가운데
        [SerializeField] Mushroom singlePrefab;       // 군락지 둘레
        [SerializeField] RectTransform mushroomLayer;
        [SerializeField] TierLook[] tiers =
        {
            new TierLook { tier = MushroomTier.Normal, hpMul = 1, scoreMul = 1f, tint = Color.white },
            new TierLook { tier = MushroomTier.Epic, hpMul = 2, scoreMul = 3f, tint = new Color(0.75f, 0.6f, 1f) },
            new TierLook { tier = MushroomTier.Unique, hpMul = 3, scoreMul = 8f, tint = new Color(1f, 0.6f, 0.45f) },
        };
        [SerializeField] Color goldenTint = new Color(1f, 0.85f, 0.25f);
        [SerializeField] float ringGap = 6f;          // 가운데 버섯과 둘레 버섯 사이
        [SerializeField] float colonyGap = 16f;       // 군락지끼리 띄울 거리
        [SerializeField] float minMushroomSize = 0.35f;   // 군락지가 많을 때 줄일 수 있는 최소 크기
        [SerializeField] float spawnTop = 30f;        // 위 가장자리에서 띄울 거리
        [SerializeField] float spawnBottom = 380f;    // 아래 가장자리에서 띄울 거리 (바 근처는 비운다)
        [SerializeField] float spawnSide = 20f;

        public class Colony
        {
            public int id;
            public Vector2 center;
            public float footprint;                   // 군락지 전체 반지름
            public readonly List<Mushroom> members = new List<Mushroom>();
        }

        // 필드 장치(그루터기·꽃밭·도토리)처럼 버섯을 놓지 않을 자리. solid면 수확기가 튕긴다
        public class Obstacle
        {
            public Vector2 position;
            public float radius;
            public bool solid;
            public System.Action<Harvester> touched;  // 수확기가 닿을 때마다 (닿아 있는 동안 처음 한 번)
            internal readonly List<Harvester> inside = new List<Harvester>();
        }

        RectTransform rect;
        RoundStats stats;
        float mushroomSize = 1f;
        int nextColonyId;
        readonly Dictionary<Harvester, PrefabPool<Harvester>> harvesterPools = new Dictionary<Harvester, PrefabPool<Harvester>>();
        PrefabPool<Mushroom> clusterPool, singlePool;
        readonly List<Harvester> harvesters = new List<Harvester>();
        readonly List<Mushroom> mushrooms = new List<Mushroom>();
        readonly List<Colony> colonies = new List<Colony>();
        readonly List<float> respawns = new List<float>();   // 군락지가 다시 돋기까지 남은 시간
        readonly List<Bar> bars = new List<Bar>();           // 본 바 + 분신 바
        readonly List<Obstacle> obstacles = new List<Obstacle>();

        public RectTransform Rect => rect;
        public RectTransform HarvesterLayer => harvesterLayer;
        public RoundStats Stats => stats;
        public bool Running { get; private set; }
        public IReadOnlyList<Harvester> Harvesters => harvesters;
        public IReadOnlyList<Mushroom> Mushrooms => mushrooms;
        public IReadOnlyList<Colony> Colonies => colonies;
        public Harvester Main => harvesters.Count > 0 ? harvesters[0] : null;   // 플레이어가 던지는 수확기
        public bool MainReady => Running && Main != null && !Main.IsMoving;    // 아직 안 던졌다 (한 판에 한 번만 던진다)
        public Bar Bar => bar;
        public float Speed => speed * (stats != null ? stats.SpeedMul : 1f);
        public float MushroomSize => mushroomSize;

        public event System.Action<Harvester> Launched;
        public event System.Action<Harvester> BarHit;
        public event System.Action<Mushroom, Harvester> Harvested;   // 풀로 돌아가기 직전에 불린다. 스킬 피해로 캤으면 수확기가 null일 수 있다
        public event System.Action<Mushroom, Harvester, int, bool> Hit;   // 맞힌 버섯, 수확기, 피해, 치명타
        public event System.Action<Vector2> ColonyCleared;           // 군락지 하나를 다 캤다 (가운데 위치)
        public event System.Action BeforeColonies;                    // 판 시작 때 군락지를 놓기 직전 (장치 자리를 먼저 잡는다)

        Vector2 barBaseSize;
        Vector2 barBasePos;
        bool barBaseSaved;

        void Awake()
        {
            rect = (RectTransform)transform;
            if (bar != null) bars.Add(bar);
            SaveBarBase();
        }

        // 씬에 놓인 바의 원래 길이·자리. HarvestGame.Awake가 이 Awake보다 먼저 Configure를 부를 수 있어 처음 쓸 때 한 번 저장한다
        void SaveBarBase()
        {
            if (barBaseSaved || bar == null) return;
            var barRect = (RectTransform)bar.transform;
            barBaseSize = barRect.sizeDelta;
            barBasePos = barRect.anchoredPosition;
            barBaseSaved = true;
        }

        // 판 시작 전에 HarvestGame이 넣는다. 안 넣으면 스킬 없는 기본값으로 시작한다. 바 길이는 씬의 원래 길이 × 바 길이 배율
        public void Configure(RoundStats roundStats)
        {
            stats = roundStats;
            SaveBarBase();
            if (bar != null)
            {
                var barRect = (RectTransform)bar.transform;
                float width = barBaseSize.x * stats.BarMul;
                float max = ((RectTransform)transform).rect.width * maxBarRatio;
                barRect.sizeDelta = new Vector2(max > 0f ? Mathf.Min(width, max) : width, barBaseSize.y);
                barRect.anchoredPosition = barBasePos;
            }
        }

        void Start()
        {
            if (stats == null) stats = RoundStats.FromSkills(new RoundBase());
            Canvas.ForceUpdateCanvases();

            // 판 중에 만들지 않도록 최대치만큼 미리 만들어 둔다 (군락지, 함께 던지는 수확기 + 분열체)
            int perColony = stats.SinglesPerColony;
            clusterPool = new PrefabPool<Mushroom>(clusterPrefab, mushroomLayer, stats.Colonies + 1);
            singlePool = new PrefabPool<Mushroom>(singlePrefab, mushroomLayer, (stats.Colonies + 1) * perColony);
            harvesterPools.Add(harvesterPrefab, new PrefabPool<Harvester>(harvesterPrefab, harvesterLayer, 1 + stats.ExtraHarvesters + stats.MaxSplit));
            BeginRound();
        }

        // 판을 처음부터 다시 (밸런스 측정기가 판마다 부른다). Configure로 새 수치를 넣은 뒤 부른다
        public void Restart()
        {
            for (int i = harvesters.Count - 1; i >= 0; i--) DespawnHarvester(harvesters[i]);
            for (int i = mushrooms.Count - 1; i >= 0; i--) Despawn(mushrooms[i]);
            colonies.Clear();
            respawns.Clear();
            obstacles.Clear();
            bars.Clear();
            if (bar != null) bars.Add(bar);
            Running = false;
            BeginRound();
        }

        void BeginRound()
        {
            // 함께 던지는 수확기는 메인 양옆에 놓는다
            SpawnHarvester(RestPosition(0f));
            float gap = harvesterPrefab.BaseRadius * stats.SizeMul * 2.2f;
            for (int i = 1; i <= stats.ExtraHarvesters; i++)
                SpawnHarvester(RestPosition((i % 2 == 1 ? 1f : -1f) * ((i + 1) / 2) * gap));

            BeforeColonies?.Invoke();
            if (stats.BossRound) SpawnBoss();
            else PlanColonies();
            Running = true;
        }

        void Update() => Step(Time.deltaTime);

        // 한 프레임 진행. 테스트·빨리감기에서도 직접 부를 수 있다
        public void Step(float dt)
        {
            if (!Running || dt <= 0f) return;

            Rect bounds = rect.rect;
            for (int i = harvesters.Count - 1; i >= 0; i--)
            {
                var h = harvesters[i];
                h.TickBuffs(dt);
                if (h.Temporary && (h.Life -= dt) <= 0f) DespawnHarvester(h);
            }
            for (int i = 0; i < harvesters.Count; i++) Move(harvesters[i], dt, bounds);

            // 캐진 버섯은 충돌 계산이 다 끝난 뒤에 돌려놓는다
            for (int i = mushrooms.Count - 1; i >= 0; i--)
            {
                var m = mushrooms[i];
                if (m.Alive) continue;
                var colony = FindColony(m.Colony);
                Despawn(m);
                if (colony == null) continue;
                colony.members.Remove(m);
                if (colony.members.Count > 0) continue;
                colonies.Remove(colony);
                if (!stats.BossRound) respawns.Add(stats.ColonyRespawn);   // 보스 판엔 군락지가 다시 돋지 않는다
                ColonyCleared?.Invoke(colony.center);
            }

            for (int i = respawns.Count - 1; i >= 0; i--)
            {
                respawns[i] -= dt;
                if (respawns[i] > 0f) continue;
                if (TrySpawnColony()) respawns.RemoveAt(i);
                else respawns[i] = 0.5f;   // 빈 자리가 없으면 조금 뒤 다시
            }
        }

        // 플레이어 투척. 판 시작 때 한 번만 된다. 함께 던지는 수확기(장인의 도구)는 조금씩 벌어진 방향으로 같이 나간다
        public bool LaunchMain(Vector2 direction)
        {
            if (!MainReady) return false;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
            for (int i = harvesters.Count - 1; i >= 1; i--)
            {
                var h = harvesters[i];
                if (h.Temporary || h.IsMoving) continue;
                float angle = (i % 2 == 1 ? 1f : -1f) * ((i + 1) / 2) * 12f;
                Launch(h, Quaternion.Euler(0f, 0f, angle) * direction);
            }
            Launch(Main, direction);
            return true;
        }

        public void Launch(Harvester h, Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
            h.Launch(KeepSpeed(h, direction));
            Launched?.Invoke(h);
        }

        // 스킬 피해 (번개·회오리·포자 폭발·꼬마 수확기). source는 점수·콤보를 받을 수확기 (없으면 null)
        public bool DealDamage(Mushroom m, int damage, Harvester source)
        {
            if (m == null || !m.Alive) return false;
            if (m.IsBoss) damage = Mathf.RoundToInt(damage * stats.BossDamageMul);
            bool harvested = m.Hit(Mathf.Max(1, damage));
            Hit?.Invoke(m, source, damage, false);
            if (harvested) Harvested?.Invoke(m, source);
            return harvested;
        }

        // ── 분신 바·장치 ──
        public void AddBar(Bar extra)
        {
            if (!bars.Contains(extra)) bars.Add(extra);
        }

        public void RemoveBar(Bar extra)
        {
            if (extra != bar) bars.Remove(extra);
        }

        public Obstacle AddObstacle(Vector2 position, float radius, bool solid, System.Action<Harvester> touched)
        {
            var o = new Obstacle { position = position, radius = radius, solid = solid, touched = touched };
            obstacles.Add(o);
            return o;
        }

        // 군락지·장치와 겹치지 않는 빈 자리 (장치를 놓을 때 쓴다)
        public bool FindFreeSpot(float radius, out Vector2 spot) => FindSpot(radius, new List<Vector2>(), out spot);

        // 분신·소환용. 첫 번째로 꺼낸 것이 메인이 된다
        public Harvester SpawnHarvester(Vector2 position, Harvester prefab = null)
        {
            if (prefab == null) prefab = harvesterPrefab;
            if (!harvesterPools.TryGetValue(prefab, out var pool))
                harvesterPools.Add(prefab, pool = new PrefabPool<Harvester>(prefab, harvesterLayer));
            var h = pool.Get();
            h.Pool = pool;
            h.BonusDamage = stats.BonusDamage;
            h.PowerDamage = stats.PowerAttack;
            h.StumpMul = 1f + stats.Base.stumpAttack;
            h.WeakMul = stats.Base.sporeWeak;
            h.Size = stats.SizeMul;
            h.Place(position);
            harvesters.Add(h);
            return h;
        }

        public void DespawnHarvester(Harvester h)
        {
            if (!harvesters.Remove(h)) return;
            h.Pool.Release(h);
        }

        // 시간이 다 되면 멈춘다. 수확기는 그 자리에 선다
        public void Stop()
        {
            Running = false;
            foreach (var h in harvesters) h.Stop();
        }

        // 바를 가로 x 위치로 (측정기 봇의 조작). 필드 밖으로는 나가지 않는다
        public void MoveBarTo(float x, float maxStep)
        {
            if (bar == null) return;
            float now = ((RectTransform)bar.transform).anchoredPosition.x;
            bar.MoveBy(Mathf.Clamp(x - now, -maxStep, maxStep), rect);
        }

        // 바 위 가운데에서 x만큼 옆
        public Vector2 RestPosition(float x)
        {
            float r = harvesterPrefab.BaseRadius * (stats != null ? stats.SizeMul : 1f);
            if (bar == null) return new Vector2(x, rect.rect.yMin + r + restGap);
            Rect b = bar.GetRect(rect);
            return new Vector2(Mathf.Clamp(b.center.x + x, b.xMin + r, b.xMax - r), b.yMax + r + restGap);
        }

        void Move(Harvester h, float dt, Rect bounds)
        {
            if (!h.IsMoving) return;

            // 한 번에 반지름 절반 이상 움직이지 않게 쪼개서 얇은 바나 작은 버섯을 지나쳐 버리지 않게 한다
            float dist = h.Velocity.magnitude * dt;
            int steps = Mathf.Clamp(Mathf.CeilToInt(dist / Mathf.Max(1f, h.Radius * 0.5f)), 1, 32);
            float sub = dt / steps;

            Vector2 p = h.Position;
            Vector2 v = h.Velocity;
            float r = h.Radius;
            for (int s = 0; s < steps; s++)
            {
                p += v * sub;

                if (p.x - r < bounds.xMin) { p.x = bounds.xMin + r; v.x = Mathf.Abs(v.x); }
                else if (p.x + r > bounds.xMax) { p.x = bounds.xMax - r; v.x = -Mathf.Abs(v.x); }
                if (p.y - r < bounds.yMin) { p.y = bounds.yMin + r; v.y = Mathf.Abs(v.y); }
                else if (p.y + r > bounds.yMax) { p.y = bounds.yMax - r; v.y = -Mathf.Abs(v.y); }

                for (int b = 0; b < bars.Count; b++)
                {
                    if (!Bounce(ref p, ref v, r, bars[b].GetRect(rect))) continue;
                    h.Combo = 0;
                    BarHit?.Invoke(h);
                    // 유도: 바에 튕길 때마다 가장 가까운 군락지 쪽으로 (위로만)
                    if (h.Has(HarvesterBuff.Homing) && NearestColony(p, out var target))
                    {
                        Vector2 dir = target - p;
                        if (dir.y < 0.2f * dir.magnitude) dir.y = 0.2f * dir.magnitude;
                        v = dir.normalized * v.magnitude;
                    }
                }

                for (int i = 0; i < obstacles.Count; i++)
                {
                    var o = obstacles[i];
                    Vector2 d = p - o.position;
                    float min = r + o.radius;
                    bool overlap = d.sqrMagnitude < min * min;
                    if (!overlap)
                    {
                        o.inside.Remove(h);
                        continue;
                    }
                    if (!o.inside.Contains(h))
                    {
                        o.inside.Add(h);
                        o.touched?.Invoke(h);
                    }
                    if (!o.solid) continue;
                    Vector2 n = d.sqrMagnitude > 0.0001f ? d.normalized : -v.normalized;
                    p = o.position + n * min;
                    if (Vector2.Dot(v, n) < 0f) v = Vector2.Reflect(v, n);
                }

                var touching = h.Touching;
                for (int i = 0; i < mushrooms.Count; i++)
                {
                    var m = mushrooms[i];
                    if (!m.Alive) continue;
                    Vector2 d = p - m.Position;
                    float min = r + m.Radius;
                    if (d.sqrMagnitude >= min * min)
                    {
                        touching.Remove(m);
                        continue;
                    }
                    // 관통 중에 겹쳐 지나가는 동안에는 한 번만 때린다
                    if (touching.Contains(m)) continue;

                    int damage = Mathf.Max(1, Mathf.RoundToInt(h.Damage * h.NextHitMul * (m.IsBoss ? stats.BossDamageMul : 1f)));
                    h.NextHitMul = 1f;
                    bool crit = stats.CritChance > 0f && Random.value < stats.CritChance;
                    if (crit) damage = Mathf.CeilToInt(damage * stats.CritMul);
                    bool harvested = m.Hit(damage);
                    Hit?.Invoke(m, h, damage, crit);
                    if (harvested)
                    {
                        h.Combo++;
                        Harvested?.Invoke(m, h);
                        if (pierce) continue;
                    }
                    if (h.Has(HarvesterBuff.Pierce) && !m.IsBoss)
                    {
                        touching.Add(m);
                        continue;
                    }
                    Vector2 n = d.sqrMagnitude > 0.0001f ? d.normalized : -v.normalized;
                    p = m.Position + n * min;
                    if (Vector2.Dot(v, n) < 0f) v = Vector2.Reflect(v, n);
                }
            }

            h.Position = p;
            h.Velocity = KeepSpeed(h, v);
            h.Sync(dt);
        }

        bool NearestColony(Vector2 from, out Vector2 center)
        {
            center = default;
            float best = float.MaxValue;
            foreach (var c in colonies)
            {
                float d = (c.center - from).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                center = c.center;
            }
            return best < float.MaxValue;
        }

        // 방향은 두고 속도를 맞춘다 (가속 버프 중이면 더 빠르게). 세로 성분이 너무 작으면 키워서 좌우로만 오가지 않게 한다
        Vector2 KeepSpeed(Harvester h, Vector2 v)
        {
            Vector2 dir = v.normalized;
            if (Mathf.Abs(dir.y) < minVertical)
            {
                float y = dir.y >= 0f ? minVertical : -minVertical;
                float x = Mathf.Sqrt(1f - y * y) * (dir.x >= 0f ? 1f : -1f);
                dir = new Vector2(x, y);
            }
            float haste = h.Has(HarvesterBuff.Haste) ? 1f + stats.Base.hasteBoost : 1f;
            if (h.Has(HarvesterBuff.Slow)) haste *= stats.Base.sporeSlow;
            return dir * Speed * haste;
        }

        // 바는 윗면(선분)만 충돌한다. 위에서 내려오는 중이고 중심이 윗면보다 위일 때만 막고 반사한다.
        // 옆·아래에서 오면 그대로 뚫고 지나가므로 바 밑이나 옆에 갇히지 않는다. 반사했으면 true
        static bool Bounce(ref Vector2 p, ref Vector2 v, float r, Rect box)
        {
            if (v.y >= 0f || p.y < box.yMax) return false;
            Vector2 closest = new Vector2(Mathf.Clamp(p.x, box.xMin, box.xMax), box.yMax);
            Vector2 d = p - closest;
            if (d.sqrMagnitude >= r * r) return false;

            // 끝 모서리에 걸치면 비스듬한 법선이 된다. 중심이 윗면보다 위라 법선은 늘 위쪽을 향한다
            Vector2 n = d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.up;
            p = closest + n * r;
            if (Vector2.Dot(v, n) >= 0f) return false;
            v = Vector2.Reflect(v, n);
            return true;
        }

        // ── 군락지 ──

        // 군락지 하나의 반지름 (가운데 + 둘레 버섯까지)
        float Footprint(float size)
        {
            float rc = clusterPrefab.BaseRadius * size, rs = singlePrefab.BaseRadius * size;
            return stats.SinglesPerColony > 0 ? rc + ringGap + rs * 2f : rc;
        }

        // 판 시작 때 군락지를 모두 놓는다. 엇갈린 격자(벌집 모양) 칸에 나눠 놓고 칸 안에서 조금씩 흔든다.
        // 칸이 군락지 수보다 모자라면 버섯 크기를 줄여 칸을 늘린다
        void PlanColonies()
        {
            List<Vector2> cells = null;
            for (mushroomSize = 1f; mushroomSize >= minMushroomSize; mushroomSize *= 0.95f)
            {
                float foot = Footprint(mushroomSize);
                cells = GridCells(foot);
                cells.RemoveAll(c => !ObstacleFree(c, foot));
                if (cells.Count >= stats.Colonies) break;
            }
            mushroomSize = Mathf.Max(mushroomSize, minMushroomSize);

            // 칸을 섞어 앞에서부터 쓴다
            for (int i = cells.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }
            int placed = Mathf.Min(cells.Count, stats.Colonies);
            for (int i = 0; i < placed; i++) SpawnColony(cells[i]);
            // 그래도 모자란 군락지는 리스폰 대기열로 (빈 자리가 생기면 돋는다)
            for (int i = placed; i < stats.Colonies; i++) respawns.Add(0.5f);
        }

        // 군락지 반지름 foot으로 스폰 구역을 채우는 엇갈린 격자의 가운데 자리들 (칸 안에서 남는 만큼 흔든다)
        List<Vector2> GridCells(float foot)
        {
            var cells = new List<Vector2>();
            Rect b = rect.rect;
            float xMin = b.xMin + spawnSide + foot, xMax = b.xMax - spawnSide - foot;
            float yMin = b.yMin + spawnBottom + foot, yMax = b.yMax - spawnTop - foot;
            if (xMin > xMax || yMin > yMax) return cells;

            float dx = foot * 2f + colonyGap;
            float dy = dx * 0.866f;                     // 엇갈린 줄 사이 (정삼각형 높이)
            int rows = Mathf.FloorToInt((yMax - yMin) / dy) + 1;
            for (int r = 0; r < rows; r++)
            {
                float offset = (r % 2) * dx * 0.5f;
                int cols = Mathf.FloorToInt((xMax - xMin - offset) / dx) + 1;
                if (cols <= 0) continue;
                // 남는 폭·높이를 가운데 맞춤
                float spareX = xMax - xMin - offset - (cols - 1) * dx;
                float spareY = yMax - yMin - (rows - 1) * dy;
                for (int c = 0; c < cols; c++)
                {
                    var pos = new Vector2(xMin + offset + spareX * 0.5f + c * dx, yMin + spareY * 0.5f + r * dy);
                    float jitter = Mathf.Min(spareX, spareY) * 0.25f;
                    pos += Random.insideUnitCircle * Mathf.Min(jitter, colonyGap * 0.5f);
                    cells.Add(pos);
                }
            }
            return cells;
        }

        // 보스 판: 군락지 대신 큰 보스 하나를 필드 가운데 위쪽에 놓는다. 유도·번개·회오리가 보스를 노리도록 군락지 하나로 등록한다.
        // 체력 = 기본 보스 체력 × 보스 hpRate × 스테이지 hpRate. 기믹은 BossGimmicks가 맡는다
        void SpawnBoss()
        {
            var b = stats.Base;
            Rect r = rect.rect;
            float radius = clusterPrefab.BaseRadius * b.bossSize;
            var center = new Vector2(0f, Mathf.Lerp(r.yMin + spawnBottom + radius, r.yMax - spawnTop - radius, 0.6f));
            var colony = new Colony { id = nextColonyId++, center = center, footprint = radius };
            colonies.Add(colony);
            SpawnBossPart(center, stats.BossHp * stats.BossHpMul, b.bossSize);
        }

        // 보스 몸 하나 (본체 또는 분신). 보스 군락지에 넣는다
        public Mushroom SpawnBossPart(Vector2 position, float hp, float size)
        {
            var colony = colonies.Count > 0 ? colonies[0] : null;
            var tint = stats.Base.bossTint;
            if (stats.Boss != null && !string.IsNullOrEmpty(stats.Boss.color)) ColorUtility.TryParseHtmlString(stats.Boss.color, out tint);
            var boss = clusterPool.Get();
            boss.Pool = clusterPool;
            boss.Place(position);
            boss.Setup(MushroomTier.Legendary, hp / Mathf.Max(1, clusterPrefab.MaxHp), false, size, tint);
            boss.Colony = colony != null ? colony.id : -1;
            boss.IsCenter = true;
            boss.IsBoss = true;
            mushrooms.Add(boss);
            if (colony != null) colony.members.Add(boss);
            return boss;
        }

        // 보스가 아직 남아 있는지 (분신 포함)
        public bool AnyBossAlive()
        {
            foreach (var m in mushrooms) if (m.IsBoss && m.Alive) return true;
            return false;
        }

        // 임시 수확기(분열체)를 모두 없앤다 (죽음의 천사)
        public int PurgeTemporaryHarvesters()
        {
            int removed = 0;
            for (int i = harvesters.Count - 1; i >= 0; i--)
            {
                if (!harvesters[i].Temporary) continue;
                DespawnHarvester(harvesters[i]);
                removed++;
            }
            return removed;
        }

        // 다시 돋을 때: 격자 칸 중 빈 곳을 먼저 보고, 없으면 아무 빈 자리나 찾는다
        bool TrySpawnColony()
        {
            float foot = Footprint(mushroomSize);
            var none = new List<Vector2>();
            var cells = GridCells(foot);
            for (int i = cells.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }
            foreach (var cell in cells)
            {
                if (!SpotFree(cell, foot, none)) continue;
                SpawnColony(cell);
                return true;
            }
            if (!FindSpot(foot, none, out var spot)) return false;
            SpawnColony(spot);
            return true;
        }

        // 다른 군락지·수확기와 겹치지 않는 가운데 자리. pending은 아직 놓지 않은 예정 자리들
        bool FindSpot(float foot, List<Vector2> pending, out Vector2 spot)
        {
            Rect b = rect.rect;
            float xMin = b.xMin + spawnSide + foot, xMax = b.xMax - spawnSide - foot;
            float yMin = b.yMin + spawnBottom + foot, yMax = b.yMax - spawnTop - foot;
            spot = default;
            if (xMin > xMax || yMin > yMax) return false;

            for (int attempt = 0; attempt < 60; attempt++)
            {
                var pos = new Vector2(Random.Range(xMin, xMax), Random.Range(yMin, yMax));
                if (!SpotFree(pos, foot, pending)) continue;
                spot = pos;
                return true;
            }
            return false;
        }

        bool SpotFree(Vector2 pos, float foot, List<Vector2> pending)
        {
            foreach (var c in colonies)
            {
                float min = foot + c.footprint + colonyGap;
                if ((c.center - pos).sqrMagnitude < min * min) return false;
            }
            foreach (var other in pending)
            {
                float min = foot * 2f + colonyGap;
                if ((other - pos).sqrMagnitude < min * min) return false;
            }
            // 남아 있는 버섯 (다 캐지 않은 옛 군락지 버섯은 colonies에 남아 있으므로 위에서 걸린다)
            foreach (var h in harvesters)
            {
                float min = foot + h.Radius;
                if ((h.Position - pos).sqrMagnitude < min * min) return false;
            }
            return ObstacleFree(pos, foot);
        }

        bool ObstacleFree(Vector2 pos, float foot)
        {
            foreach (var o in obstacles)
            {
                float min = foot + o.radius + colonyGap;
                if ((o.position - pos).sqrMagnitude < min * min) return false;
            }
            return true;
        }

        void SpawnColony(Vector2 center)
        {
            var colony = new Colony { id = nextColonyId++, center = center, footprint = Footprint(mushroomSize) };
            var tier = RollTier();
            var look = Look(tier);

            var centerMushroom = SpawnMushroom(clusterPool, center, colony.id, look);
            centerMushroom.IsCenter = true;
            colony.members.Add(centerMushroom);
            int n = stats.SinglesPerColony;
            float ring = (clusterPrefab.BaseRadius + ringGap + singlePrefab.BaseRadius) * mushroomSize;
            float start = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < n; i++)
            {
                float a = start + i * Mathf.PI * 2f / n + Random.Range(-0.15f, 0.15f);
                var pos = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ring;
                colony.members.Add(SpawnMushroom(singlePool, pos, colony.id, look));
            }
            colonies.Add(colony);
        }

        Mushroom SpawnMushroom(PrefabPool<Mushroom> pool, Vector2 pos, int colonyId, TierLook look)
        {
            var m = pool.Get();
            m.Pool = pool;
            m.Place(pos);
            bool golden = stats.GoldenChance > 0f && Random.value < stats.GoldenChance;
            m.Setup(look.tier, look.hpMul * stats.HpRate, golden, mushroomSize, golden ? goldenTint : look.tint);
            m.Colony = colonyId;
            mushrooms.Add(m);
            return m;
        }

        // 유니크 → 에픽 → 일반 순으로 굴린다
        MushroomTier RollTier()
        {
            float roll = Random.value;
            if (roll < stats.UniqueChance) return MushroomTier.Unique;
            if (roll < stats.UniqueChance + stats.EpicChance) return MushroomTier.Epic;
            return MushroomTier.Normal;
        }

        TierLook Look(MushroomTier tier)
        {
            foreach (var t in tiers) if (t.tier == tier) return t;
            return tiers.Length > 0 ? tiers[0] : new TierLook();
        }

        public float ScoreMul(MushroomTier tier) => Look(tier).scoreMul;

        Colony FindColony(int id)
        {
            foreach (var c in colonies) if (c.id == id) return c;
            return null;
        }

        void Despawn(Mushroom m)
        {
            mushrooms.Remove(m);
            m.Pool.Release(m);
        }
    }
}
