using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 수확 필드. 수확기·버섯을 풀에서 꺼내 놓고, 매 프레임 수확기를 움직이며 벽·바·버섯과의 충돌을 계산한다.
    // 좌표는 이 RectTransform 로컬(가운데 기준, 캔버스 픽셀). 상하좌우 가장자리 모두 벽이라 수확기는 빠져나가지 않는다.
    // 수확기는 판 시작 때 한 번 던지면 시간이 끝날 때까지 같은 속도로 튕겨 다닌다.
    // 캐진 버섯은 풀로 돌아가고, 잠시 뒤 빈 자리에 새 버섯이 돋는다
    [RequireComponent(typeof(RectTransform))]
    public class HarvestField : MonoBehaviour
    {
        [System.Serializable]
        public class MushroomKind
        {
            public Mushroom prefab;
            public float weight = 1f;                 // 돋아날 때 뽑힐 비중
            public int maxAlive;                      // 필드에 동시에 있을 수 있는 수. 0이면 제한 없음
            public int prewarm = 8;
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

        [Header("버섯")]
        [SerializeField] MushroomKind[] kinds;
        [SerializeField] RectTransform mushroomLayer;
        [SerializeField] int mushroomCount = 12;      // 필드에 유지할 버섯 수
        [SerializeField] float respawnDelay = 0.8f;
        [SerializeField] float spawnTop = 30f;        // 위 가장자리에서 띄울 거리
        [SerializeField] float spawnBottom = 380f;    // 아래 가장자리에서 띄울 거리 (바 근처는 비운다)
        [SerializeField] float spawnSide = 20f;
        [SerializeField] float spawnGap = 12f;        // 버섯끼리 띄울 최소 거리

        RectTransform rect;
        readonly Dictionary<Harvester, PrefabPool<Harvester>> harvesterPools = new Dictionary<Harvester, PrefabPool<Harvester>>();
        readonly Dictionary<Mushroom, PrefabPool<Mushroom>> mushroomPools = new Dictionary<Mushroom, PrefabPool<Mushroom>>();
        readonly List<Harvester> harvesters = new List<Harvester>();
        readonly List<Mushroom> mushrooms = new List<Mushroom>();
        readonly List<float> respawns = new List<float>();   // 남은 대기 시간

        public RectTransform Rect => rect;
        public bool Running { get; private set; }
        public IReadOnlyList<Harvester> Harvesters => harvesters;
        public IReadOnlyList<Mushroom> Mushrooms => mushrooms;
        public Harvester Main => harvesters.Count > 0 ? harvesters[0] : null;   // 플레이어가 던지는 수확기
        public bool MainReady => Running && Main != null && !Main.IsMoving;    // 아직 안 던졌다 (한 판에 한 번만 던진다)
        public Bar Bar => bar;

        public event System.Action<Harvester> Launched;
        public event System.Action<Harvester> BarHit;
        public event System.Action<Mushroom, Harvester> Harvested;   // 풀로 돌아가기 직전에 불린다

        void Awake()
        {
            rect = (RectTransform)transform;
            harvesterPools.Add(harvesterPrefab, new PrefabPool<Harvester>(harvesterPrefab, harvesterLayer, 1));
            foreach (var kind in kinds)
            {
                if (kind.prefab == null || mushroomPools.ContainsKey(kind.prefab)) continue;
                mushroomPools.Add(kind.prefab, new PrefabPool<Mushroom>(kind.prefab, mushroomLayer, kind.prewarm));
            }
        }

        void Start()
        {
            Canvas.ForceUpdateCanvases();
            SpawnHarvester(RestPosition(0f));
            for (int i = 0; i < mushroomCount; i++) TrySpawnMushroom();
            Running = true;
        }

        void Update() => Step(Time.deltaTime);

        // 한 프레임 진행. 테스트·빨리감기에서도 직접 부를 수 있다
        public void Step(float dt)
        {
            if (!Running || dt <= 0f) return;

            Rect bounds = rect.rect;
            Rect barRect = bar != null ? bar.GetRect(rect) : default;
            for (int i = 0; i < harvesters.Count; i++) Move(harvesters[i], dt, bounds, barRect);

            // 캐진 버섯은 충돌 계산이 다 끝난 뒤에 돌려놓는다
            for (int i = mushrooms.Count - 1; i >= 0; i--)
            {
                if (mushrooms[i].Alive) continue;
                Despawn(mushrooms[i]);
                respawns.Add(respawnDelay);
            }

            for (int i = respawns.Count - 1; i >= 0; i--)
            {
                respawns[i] -= dt;
                if (respawns[i] > 0f) continue;
                if (TrySpawnMushroom()) respawns.RemoveAt(i);
                else respawns[i] = 0.2f;   // 빈 자리가 없으면 조금 뒤 다시
            }
        }

        // 플레이어 투척. 판 시작 때 한 번만 된다
        public bool LaunchMain(Vector2 direction)
        {
            if (!MainReady) return false;
            Launch(Main, direction);
            return true;
        }

        public void Launch(Harvester h, Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
            h.Launch(KeepSpeed(direction));
            Launched?.Invoke(h);
        }

        // 분신·소환용. 첫 번째로 꺼낸 것이 메인이 된다
        public Harvester SpawnHarvester(Vector2 position, Harvester prefab = null)
        {
            if (prefab == null) prefab = harvesterPrefab;
            if (!harvesterPools.TryGetValue(prefab, out var pool))
                harvesterPools.Add(prefab, pool = new PrefabPool<Harvester>(prefab, harvesterLayer));
            var h = pool.Get();
            h.Pool = pool;
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

        // 바 위 가운데에서 x만큼 옆
        public Vector2 RestPosition(float x)
        {
            float r = harvesterPrefab.Radius;
            if (bar == null) return new Vector2(x, rect.rect.yMin + r + restGap);
            Rect b = bar.GetRect(rect);
            return new Vector2(Mathf.Clamp(b.center.x + x, b.xMin + r, b.xMax - r), b.yMax + r + restGap);
        }

        void Move(Harvester h, float dt, Rect bounds, Rect barRect)
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

                if (bar != null && Bounce(ref p, ref v, r, barRect))
                {
                    h.Combo = 0;
                    BarHit?.Invoke(h);
                }

                for (int i = 0; i < mushrooms.Count; i++)
                {
                    var m = mushrooms[i];
                    if (!m.Alive) continue;
                    Vector2 d = p - m.Position;
                    float min = r + m.Radius;
                    if (d.sqrMagnitude >= min * min) continue;

                    bool harvested = m.Hit(h.Damage);
                    if (harvested)
                    {
                        h.Combo++;
                        Harvested?.Invoke(m, h);
                        if (pierce) continue;
                    }
                    Vector2 n = d.sqrMagnitude > 0.0001f ? d.normalized : -v.normalized;
                    p = m.Position + n * min;
                    if (Vector2.Dot(v, n) < 0f) v = Vector2.Reflect(v, n);
                }
            }

            h.Position = p;
            h.Velocity = KeepSpeed(v);
            h.Sync(dt);
        }

        // 방향은 두고 속도를 speed로 맞춘다. 세로 성분이 너무 작으면 키워서 좌우로만 오가지 않게 한다
        Vector2 KeepSpeed(Vector2 v)
        {
            Vector2 dir = v.normalized;
            if (Mathf.Abs(dir.y) < minVertical)
            {
                float y = dir.y >= 0f ? minVertical : -minVertical;
                float x = Mathf.Sqrt(1f - y * y) * (dir.x >= 0f ? 1f : -1f);
                dir = new Vector2(x, y);
            }
            return dir * speed;
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

        bool TrySpawnMushroom()
        {
            var prefab = PickKind();
            if (prefab == null) return false;

            Rect b = rect.rect;
            float r = prefab.Radius;
            float xMin = b.xMin + spawnSide + r, xMax = b.xMax - spawnSide - r;
            float yMin = b.yMin + spawnBottom + r, yMax = b.yMax - spawnTop - r;
            if (xMin > xMax || yMin > yMax) return false;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                var pos = new Vector2(Random.Range(xMin, xMax), Random.Range(yMin, yMax));
                if (!IsFree(pos, r)) continue;

                var pool = mushroomPools[prefab];
                var m = pool.Get();
                m.Pool = pool;
                m.Place(pos);
                mushrooms.Add(m);
                return true;
            }
            return false;
        }

        bool IsFree(Vector2 pos, float r)
        {
            foreach (var m in mushrooms)
            {
                float min = r + m.Radius + spawnGap;
                if ((m.Position - pos).sqrMagnitude < min * min) return false;
            }
            foreach (var h in harvesters)
            {
                float min = r + h.Radius;
                if ((h.Position - pos).sqrMagnitude < min * min) return false;
            }
            return true;
        }

        // 비중대로 뽑되, 최대 수에 닿은 종류는 뺀다 (오래 버티는 군락이 필드를 채우지 않게)
        Mushroom PickKind()
        {
            float total = 0f;
            foreach (var kind in kinds) if (CanSpawn(kind)) total += Mathf.Max(0f, kind.weight);
            if (total <= 0f) return null;

            float roll = Random.value * total;
            Mushroom last = null;
            foreach (var kind in kinds)
            {
                if (!CanSpawn(kind)) continue;
                last = kind.prefab;
                roll -= Mathf.Max(0f, kind.weight);
                if (roll <= 0f) return kind.prefab;
            }
            return last;
        }

        bool CanSpawn(MushroomKind kind) =>
            kind.prefab != null && (kind.maxAlive <= 0 || mushroomPools[kind.prefab].CountActive < kind.maxAlive);

        void Despawn(Mushroom m)
        {
            mushrooms.Remove(m);
            m.Pool.Release(m);
        }
    }
}
