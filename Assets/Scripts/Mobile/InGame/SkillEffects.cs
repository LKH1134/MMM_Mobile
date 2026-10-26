using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 판 중에 발동하는 스킬 효과 (HarvestField 이벤트를 듣는다). 수치는 HarvestField.Stats(RoundStats)에서 읽는다.
    //   바에 닿을 때 (던지는 수확기만, 분열체 제외): 사냥 준비·가속·관통·유도·약손은 그 수확기에, 피버·지혜는 판 전체에 100% 걸린다 (다시 닿으면 시간을 새로 채움).
    //     분열·분신술은 확률 (지혜가 걸려 있으면 그만큼 더한다)
    //   단일 버섯을 캘 때: 이독제독 / 군락지를 다 캘 때: 연속 번개 / 군락(가운데 버섯)에 부딪힐 때: 회오리·포자 폭발
    //   첫 투척 때: 전설의 할아버지 (보유 버프마다 확률로 바로 건다)
    //   회전 꼬마 수확기: 메인 수확기 둘레를 돌며 닿는 버섯을 때린다
    // 분열체·분신 바·꼬마 수확기·번개 그림은 판 시작 때 최대치만큼 미리 만들어 두고 풀에서 꺼내 쓴다
    public class SkillEffects : MonoBehaviour
    {
        [SerializeField] HarvestField field;
        [SerializeField] TMP_Text buffText;           // 걸린 버프 목록 (임시 표시). 비워도 된다
        [SerializeField] Sprite orbitSprite;          // 꼬마 수확기 그림 (임시: 수확기 그림)
        [SerializeField] Sprite flashSprite;          // 번개·폭발 표시 (임시: 동그라미)

        [Header("모양")]
        [SerializeField] float orbitSize = 54f;
        [SerializeField] float orbitDistance = 50f;   // 메인 수확기 가장자리에서 떨어진 거리
        [SerializeField] float orbitSpeed = 420f;     // 도/초
        [SerializeField] float orbitHitCooldown = 0.2f;
        [SerializeField] float splitSpread = 25f;     // 분열체가 갈라지는 각도
        [SerializeField] float splitDamage = 0.5f;    // 분열체 공격력 배율
        [SerializeField] float flashTime = 0.25f;
        [SerializeField] Color cloneBarTint = new Color(1f, 1f, 1f, 0.55f);

        [Header("버프 색 (임시)")]
        [SerializeField] Color powerTint = new Color(1f, 0.75f, 0.45f);
        [SerializeField] Color hasteTint = new Color(0.55f, 0.9f, 1f);
        [SerializeField] Color pierceTint = new Color(1f, 0.5f, 0.5f);
        [SerializeField] Color homingTint = new Color(0.6f, 1f, 0.6f);
        [SerializeField] Color splitTint = new Color(0.85f, 0.75f, 1f);

        class CloneBar
        {
            public Bar bar;
            public float life;
        }

        class Dot
        {
            public int colony;
            public float life, tick;
        }

        class Flash
        {
            public Image image;
            public float life;
        }

        RoundStats s;
        float fever, wisdom, acorn;
        bool openingDone;
        bool dealing;                                 // 스킬 피해 중 (스킬 피해가 다른 스킬을 다시 부르지 않게)
        PrefabPool<Bar> clonePool;
        readonly List<CloneBar> clones = new List<CloneBar>();
        readonly List<Dot> dots = new List<Dot>();
        readonly List<RectTransform> orbits = new List<RectTransform>();
        readonly Dictionary<Mushroom, float> orbitCooldown = new Dictionary<Mushroom, float>();
        readonly List<Mushroom> scratch = new List<Mushroom>();
        readonly List<Flash> flashes = new List<Flash>();
        readonly Stack<Image> freeFlashes = new Stack<Image>();
        readonly StringBuilder label = new StringBuilder();
        float orbitAngle;

        public float ComboMul => fever > 0f ? 1f + s.FeverCombo : 1f;          // 피버 중 콤보 계수 배율
        public float ScoreMul => acorn > 0f ? s.Base.acornScoreMul : 1f;      // 도토리를 모두 밝힌 동안 점수 배율
        public bool Fever => fever > 0f;
        public bool Wisdom => wisdom > 0f;
        public event System.Action<Harvester> Healed;  // 약손이 걸렸다 (포자 디버프 제거는 디버프 쪽에서 듣는다)

        void Awake()
        {
            field.BarHit += OnBarHit;
            field.Launched += OnLaunched;
            field.Harvested += OnHarvested;
            field.Hit += OnHit;
            field.ColonyCleared += OnColonyCleared;
        }

        void Start()
        {
            s = field.Stats;
            // 분신 바: 본 바를 본떠 최대 수만큼 미리 만든다
            if (s.CloneBarMax > 0 && field.Bar != null)
                clonePool = new PrefabPool<Bar>(field.Bar, field.Bar.transform.parent, s.CloneBarMax);
            for (int i = 0; i < s.OrbitCount; i++) orbits.Add(MakeRect("Orbit", orbitSprite, orbitSize, field.HarvesterLayer));
            int flashCount = (s.LightningChance > 0f ? 2 : 0) + (s.BurstChance > 0f ? 2 : 0);
            for (int i = 0; i < flashCount; i++) freeFlashes.Push(MakeRect("Flash", flashSprite, 100f, field.Rect).GetComponent<Image>());
            if (buffText != null) buffText.text = "";
        }

        void Update()
        {
            if (s == null || !field.Running) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            fever = Mathf.Max(0f, fever - dt);
            wisdom = Mathf.Max(0f, wisdom - dt);
            acorn = Mathf.Max(0f, acorn - dt);

            for (int i = clones.Count - 1; i >= 0; i--)
            {
                if ((clones[i].life -= dt) > 0f) continue;
                field.RemoveBar(clones[i].bar);
                clonePool.Release(clones[i].bar);
                clones.RemoveAt(i);
            }

            TickDots(dt);
            TickOrbits(dt);
            TickFlashes(dt);
            ShowBuffs();
        }

        // ── 바 ──

        void OnBarHit(Harvester h)
        {
            if (h.Temporary) return;
            ApplyBarBuffs(h);
            if (s.SplitDuration > 0f && Chance(s.SplitChance)) Split(h);
            if (s.CloneBarDuration > 0f && Chance(s.CloneBarChance)) SpawnCloneBar();
        }

        void ApplyBarBuffs(Harvester h)
        {
            if (s.PowerAttack > 0) h.SetBuff(HarvesterBuff.Power, s.PowerDuration);
            h.SetBuff(HarvesterBuff.Haste, s.HasteDuration);
            h.SetBuff(HarvesterBuff.Pierce, s.PierceDuration);
            h.SetBuff(HarvesterBuff.Homing, s.HomingDuration);
            if (s.HealDuration > 0f)
            {
                h.SetBuff(HarvesterBuff.Heal, s.HealDuration);
                Healed?.Invoke(h);
            }
            if (s.FeverDuration > 0f) fever = s.FeverDuration;
            if (s.WisdomRate > 0f) wisdom = s.WisdomDuration;
        }

        // 전설의 할아버지: 첫 투척 때 보유한 바 버프마다 확률로 바로 건다
        void OnLaunched(Harvester h)
        {
            if (openingDone || h != field.Main) return;
            openingDone = true;
            if (s.OpeningChance <= 0f) return;
            if (s.PowerAttack > 0 && Roll()) h.SetBuff(HarvesterBuff.Power, s.PowerDuration);
            if (s.HasteDuration > 0f && Roll()) h.SetBuff(HarvesterBuff.Haste, s.HasteDuration);
            if (s.PierceDuration > 0f && Roll()) h.SetBuff(HarvesterBuff.Pierce, s.PierceDuration);
            if (s.HomingDuration > 0f && Roll()) h.SetBuff(HarvesterBuff.Homing, s.HomingDuration);
            if (s.HealDuration > 0f && Roll()) h.SetBuff(HarvesterBuff.Heal, s.HealDuration);
            if (s.FeverDuration > 0f && Roll()) fever = s.FeverDuration;
            if (s.WisdomRate > 0f && Roll()) wisdom = s.WisdomDuration;

            bool Roll() => Random.value < s.OpeningChance;
        }

        // 분열: 수확기 자리에서 양옆으로 갈라지는 작은 수확기들. 판 시작 때 최대 수만큼 만들어 둔 풀에서 꺼낸다
        void Split(Harvester h)
        {
            int alive = 0;
            foreach (var other in field.Harvesters) if (other.Temporary) alive++;
            for (int i = 0; i < s.Base.splitCount && alive < s.MaxSplit; i++, alive++)
            {
                var child = field.SpawnHarvester(h.Position);
                child.Temporary = true;
                child.Life = s.SplitDuration;
                child.Size = h.Size * s.Base.splitSize;
                child.DamageMul = splitDamage;
                child.Tint(splitTint);
                float angle = (i % 2 == 0 ? 1f : -1f) * splitSpread * (i / 2 + 1);
                field.Launch(child, Quaternion.Euler(0f, 0f, angle) * h.Velocity);
            }
        }

        // 분신 바: 본 바와 같은 높이의 왼쪽·가운데·오른쪽 중 본 바에서 가장 먼 빈 자리
        void SpawnCloneBar()
        {
            if (clonePool == null || clones.Count >= s.CloneBarMax) return;
            var main = (RectTransform)field.Bar.transform;
            float width = field.Rect.rect.width;
            float bestX = 0f, bestDistance = -1f;
            foreach (float k in new[] { -0.3f, 0f, 0.3f })
            {
                float x = k * width;
                bool used = false;
                foreach (var c in clones) if (Mathf.Abs(((RectTransform)c.bar.transform).anchoredPosition.x - x) < 1f) used = true;
                float distance = Mathf.Abs(main.anchoredPosition.x - x);
                if (used || distance <= bestDistance) continue;
                bestDistance = distance;
                bestX = x;
            }
            if (bestDistance < 0f) return;

            var bar = clonePool.Get();
            var rt = (RectTransform)bar.transform;
            rt.SetSiblingIndex(main.GetSiblingIndex());
            rt.sizeDelta = main.sizeDelta;
            rt.anchoredPosition = new Vector2(bestX, main.anchoredPosition.y);
            var graphic = bar.GetComponent<Graphic>();
            if (graphic != null) graphic.color = cloneBarTint;
            field.AddBar(bar);
            clones.Add(new CloneBar { bar = bar, life = s.CloneBarDuration });
        }

        // ── 확률 발동 ──

        void OnHarvested(Mushroom m, Harvester h)
        {
            // 이독제독: 단일 버섯을 캘 때 다음 타격 피해 증가
            if (dealing || h == null || m.IsCenter || s.PoisonChance <= 0f) return;
            if (Chance(s.PoisonChance)) h.NextHitMul = s.PoisonMul;
        }

        void OnHit(Mushroom m, Harvester h, int damage, bool crit)
        {
            if (dealing || h == null || !m.IsCenter) return;
            // 회오리: 그 군락지에 지속 피해
            if (s.WhirlChance > 0f && Chance(s.WhirlChance))
                dots.Add(new Dot { colony = m.Colony, life = s.WhirlDuration, tick = 0f });
            // 포자 폭발: 필드 전체 피해
            if (s.BurstChance > 0f && Chance(s.BurstChance))
            {
                int burst = SkillDamage(s.BurstDamage);
                scratch.Clear();
                scratch.AddRange(field.Mushrooms);
                Deal(scratch, burst);
                ShowFlash(m.Position, 260f, new Color(0.7f, 1f, 0.5f, 0.6f));
            }
        }

        // 연속 번개: 군락지를 다 캐면 다른 군락지(Lv.3부터는 아무 곳)에 번개
        void OnColonyCleared(Vector2 at)
        {
            if (dealing || s.LightningChance <= 0f || !Chance(s.LightningChance)) return;
            Vector2 target;
            if (s.LightningAnywhere)
            {
                var r = field.Rect.rect;
                target = new Vector2(Random.Range(r.xMin, r.xMax), Random.Range(r.yMin + r.height * 0.3f, r.yMax));
            }
            else
            {
                var colonies = field.Colonies;
                if (colonies.Count == 0) return;
                target = colonies[Random.Range(0, colonies.Count)].center;
            }
            float radius = s.Base.lightningRadius * Mathf.Max(0.5f, field.MushroomSize);
            scratch.Clear();
            foreach (var m in field.Mushrooms)
                if (m.Alive && (m.Position - target).sqrMagnitude < (radius + m.Radius) * (radius + m.Radius)) scratch.Add(m);
            Deal(scratch, SkillDamage(s.LightningDamage));
            ShowFlash(target, radius * 2f, new Color(1f, 1f, 0.6f, 0.8f));
        }

        void TickDots(float dt)
        {
            for (int i = dots.Count - 1; i >= 0; i--)
            {
                var d = dots[i];
                d.life -= dt;
                d.tick -= dt;
                bool alive = true;
                if (d.tick <= 0f)
                {
                    d.tick += 0.5f;
                    scratch.Clear();
                    foreach (var c in field.Colonies)
                        if (c.id == d.colony) scratch.AddRange(c.members);
                    alive = scratch.Count > 0;   // 군락지가 이미 사라졌으면 끝
                    Deal(scratch, SkillDamage(s.WhirlDamage));
                }
                if (d.life <= 0f || !alive) dots.RemoveAt(i);
            }
        }

        // ── 회전 꼬마 수확기 ──

        void TickOrbits(float dt)
        {
            var main = field.Main;
            bool show = main != null && main.IsMoving;
            orbitAngle += orbitSpeed * dt;
            int damage = SkillDamage(s.OrbitDamage);
            float hitRadius = orbitSize * 0.5f;

            // 쿨다운 줄이기 (끝났거나 사라진 버섯은 뺀다)
            scratch.Clear();
            scratch.AddRange(orbitCooldown.Keys);
            foreach (var m in scratch)
            {
                float left = orbitCooldown[m] - dt;
                if (left <= 0f || !m.Alive) orbitCooldown.Remove(m);
                else orbitCooldown[m] = left;
            }

            for (int i = 0; i < orbits.Count; i++)
            {
                var o = orbits[i];
                if (o.gameObject.activeSelf != show) o.gameObject.SetActive(show);
                if (!show) continue;
                float a = (orbitAngle + i * 360f / orbits.Count) * Mathf.Deg2Rad;
                Vector2 pos = main.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (main.Radius + orbitDistance);
                o.anchoredPosition = pos;
                o.localRotation = Quaternion.Euler(0f, 0f, -orbitAngle * 2f);

                var mushrooms = field.Mushrooms;
                for (int k = 0; k < mushrooms.Count; k++)
                {
                    var m = mushrooms[k];
                    if (!m.Alive || orbitCooldown.ContainsKey(m)) continue;
                    float min = hitRadius + m.Radius;
                    if ((m.Position - pos).sqrMagnitude >= min * min) continue;
                    orbitCooldown[m] = orbitHitCooldown;
                    dealing = true;
                    field.DealDamage(m, damage, main);
                    dealing = false;
                }
            }
        }

        // ── 공용 ──

        // 확률 스킬 발동 (지혜가 걸려 있으면 발동률을 더한다)
        bool Chance(float p) => Random.value < p + (wisdom > 0f ? s.WisdomRate : 0f);

        int SkillDamage(float ratio)
        {
            var main = field.Main;
            int attack = main != null ? main.Damage : 1;
            return Mathf.Max(1, Mathf.RoundToInt(attack * ratio));
        }

        void Deal(List<Mushroom> targets, int damage)
        {
            dealing = true;
            foreach (var m in targets) if (m.Alive) field.DealDamage(m, damage, field.Main);
            dealing = false;
        }

        public void StartAcornBonus() => acorn = s.Base.acornDuration;

        void ShowFlash(Vector2 at, float size, Color color)
        {
            if (freeFlashes.Count == 0) return;
            var image = freeFlashes.Pop();
            image.gameObject.SetActive(true);
            image.rectTransform.anchoredPosition = at;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.color = color;
            flashes.Add(new Flash { image = image, life = flashTime });
        }

        void TickFlashes(float dt)
        {
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                var f = flashes[i];
                f.life -= dt;
                var c = f.image.color;
                c.a = Mathf.Clamp01(f.life / flashTime) * 0.8f;
                f.image.color = c;
                if (f.life > 0f) continue;
                f.image.gameObject.SetActive(false);
                freeFlashes.Push(f.image);
                flashes.RemoveAt(i);
            }
        }

        // 임시 표시: 걸린 버프를 수확기 색과 글자로 보여 준다
        void ShowBuffs()
        {
            foreach (var h in field.Harvesters)
            {
                if (h.Temporary) continue;
                Color tint = Color.white;
                if (h.Has(HarvesterBuff.Power)) tint = powerTint;
                if (h.Has(HarvesterBuff.Haste)) tint = hasteTint;
                if (h.Has(HarvesterBuff.Homing)) tint = homingTint;
                if (h.Has(HarvesterBuff.Pierce)) tint = pierceTint;
                h.Tint(tint);
            }
            if (buffText == null) return;

            label.Clear();
            var main = field.Main;
            if (main != null)
            {
                Add("공격", main.Remaining(HarvesterBuff.Power));
                Add("가속", main.Remaining(HarvesterBuff.Haste));
                Add("관통", main.Remaining(HarvesterBuff.Pierce));
                Add("유도", main.Remaining(HarvesterBuff.Homing));
                Add("약손", main.Remaining(HarvesterBuff.Heal));
                Add("그루터기", main.Remaining(HarvesterBuff.Stump));
            }
            Add("피버", fever);
            Add("지혜", wisdom);
            Add("도토리", acorn);
            if (clones.Count > 0) Add("분신 바", clones[0].life);
            buffText.text = label.ToString();

            void Add(string name, float seconds)
            {
                if (seconds <= 0f) return;
                if (label.Length > 0) label.Append("  ");
                label.Append(name).Append(' ').Append(seconds.ToString("0.0"));
            }
        }

        static RectTransform MakeRect(string name, Sprite sprite, float size, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            go.SetActive(false);
            return rt;
        }
    }
}
