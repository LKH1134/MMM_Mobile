using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // Main 씬 방치형 전투. 꼬마(GgomaArea의 칸마다 1명)가 알아서 수확기를 던지고, 적은 EnemyArea 안 아무 데나 웨이브로 나온다.
    // 웨이브 IdleProgress.WavesPerStage개를 넘기면 보스 1마리 — 제한 시간 안에 잡으면 다음 스테이지, 못 잡으면 같은 스테이지 1웨이브부터.
    // 살아 있는 적은 꼬마 무리(체력 하나를 같이 쓴다)를 초당 공격하고, 체력은 초마다 재생된다. 체력이 0이 되면 패배 — 같은 스테이지 1웨이브부터.
    // 체력은 스테이지를 넘기거나 패배·보스 실패 뒤에 가득 찬다.
    // 적·수확기·숫자 팝업은 전부 코드로 만들고 풀에 돌려 쓴다. 그림은 임시 리소스(Resources 경로)를 쓴다.
    // 전투는 홈(하단바 TabGroup에 열린 탭이 없을 때)에서만 보인다. 다른 탭을 여는 동안에도 보이지 않게 계속 진행하고,
    // 홈으로 돌아오면 그동안(앱 밖에 있던 시간 포함, IdleAway) 번 것을 정산 팝업으로 보여 준다
    public class IdleBattle : MonoBehaviour
    {
        [Header("배치 (씬)")]
        [SerializeField] RectTransform enemyArea;
        [SerializeField] RectTransform[] ggomaSlots;
        [SerializeField] RectTransform effectLayer;         // 수확기·팝업·HUD가 올라가는 판 (비우면 이 오브젝트)
        [SerializeField] TMP_FontAsset font;
        [SerializeField] bool hidePlaceholders = true;      // 칸·영역에 임시로 넣어 둔 그림을 숨긴다
        [SerializeField] TabGroup homeTabs;                 // Main 하단바 — 열린 탭이 없을 때(홈)만 전투를 보인다 (비우면 늘 보인다)

        [Header("임시 리소스 (Resources 경로)")]
        [SerializeField] string enemyFolder = "Art/Dream/Characters/Critters/Mushrooms";
        [SerializeField] string bossFolder = "Art/Dream/Characters/Critters/Bosses";
        [SerializeField] string[] extraBossSprites = { "Art/Dream/Characters/Grandpa/grandpa_ref" };   // 폴더 밖 보스 그림 (Resources 경로, 확장자 없이)
        [SerializeField] string harvesterFolder = "Art/Dream/Harvesters";

        [Header("크기·연출")]
        [SerializeField] float ggomaSize = 130f;
        [SerializeField] float enemySize = 110f;
        [SerializeField] float bossSize = 560f;
        [SerializeField] float harvesterSize = 64f;
        [SerializeField] float flightTime = 0.45f;          // 수확기가 날아가는 시간
        [SerializeField] float arcHeight = 140f;            // 포물선 높이
        [SerializeField] float waveDelay = 0.6f;            // 웨이브 사이 쉬는 시간
        [SerializeField] float hudTop = -230f;              // HUD 위치 (캔버스 위쪽 기준)
        [SerializeField] float enemyAttackDelay = 0.4f;     // 적이 나타나고 이만큼 지나야 공격한다

        class Ggoma { public RectTransform rt; public Image img; public int slot; public float timer, punch, phase; public Sprite harvester; }

        class Enemy
        {
            public RectTransform rt, hpRoot, hpFill;
            public Image img;
            public double hp, maxHp, incoming;
            public bool boss, dead;
            public float bob, flash, age, deadT;
            public Vector2 home;
            public long gold;
            public int gen;             // 풀에서 다시 꺼낼 때마다 +1 — 날아가던 수확기가 다른 적을 맞히지 않게
        }

        class Shot { public RectTransform rt; public Image img; public Enemy target; public int gen; public Vector2 from, to; public float t, spin; public double dmg; public bool crit; }
        class Popup { public TMP_Text text; public Vector2 pos; public float t, life; }

        readonly List<Ggoma> ggomas = new List<Ggoma>();
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<Popup> popups = new List<Popup>();
        readonly Stack<Enemy> freeEnemies = new Stack<Enemy>();
        readonly Stack<Shot> freeShots = new Stack<Shot>();
        readonly Stack<Popup> freePopups = new Stack<Popup>();
        readonly List<Enemy> pick = new List<Enemy>();

        Sprite[] enemySprites, bossSprites, harvesterSprites;
        Sprite white;

        int stage, wave;            // wave: 1..WavesPerStage = 일반, WavesPerStage+1 = 보스
        int waveTotal, waveKilled;
        float nextWaveIn, bossTimeLeft;
        Enemy boss;
        long pendingGold;
        float goldFlushIn;

        // 꼬마 무리 체력
        double partyHp, partyMaxHp, pendingHurt;
        float hurtShowIn, hurtFlash, hpTextIn;

        // HUD
        TMP_Text stageText, waveText, timerText, hpText, powerText, statsText;
        RectTransform barFill, timerRoot, timerFill, hpFill;
        GameObject statsPanel;
        float powerRefreshIn;

        // 홈 밖에서는 숨긴 채 진행
        readonly List<CanvasGroup> views = new List<CanvasGroup>();
        bool atHome = true;
        IdleReportPopup reportPopup;
        HitEffects hitFx;           // 타격 이펙트 (MoreMustMush FX/hit 프레임)
        float markSeenIn;
        const float MarkSeenInterval = 5f;      // 앱이 갑자기 꺼져도 이만큼 이내 시각부터 앱 밖 보상을 셈한다

        void Awake()
        {
            if (effectLayer == null) effectLayer = (RectTransform)transform;
            hitFx = new HitEffects(effectLayer);
            white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            enemySprites = Resources.LoadAll<Sprite>(enemyFolder);
            var bosses = new List<Sprite>(Resources.LoadAll<Sprite>(bossFolder));
            foreach (var path in extraBossSprites)
            {
                var sp = string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path);
                if (sp != null) bosses.Add(sp);
                else Debug.LogWarning($"[IdleBattle] 보스 그림 없음: Resources/{path}");
            }
            bossSprites = bosses.ToArray();
            harvesterSprites = Resources.LoadAll<Sprite>(harvesterFolder);
            if (enemySprites.Length == 0) Debug.LogWarning($"[IdleBattle] 적 그림 없음: Resources/{enemyFolder}");
            if (bossSprites.Length == 0) bossSprites = enemySprites;

            if (hidePlaceholders)
            {
                HideImage(enemyArea);
                if (ggomaSlots.Length > 0 && ggomaSlots[0] != null) HideImage(ggomaSlots[0].parent);
            }
            for (int i = 0; i < ggomaSlots.Length; i++)
            {
                var slot = ggomaSlots[i];
                if (slot == null) continue;
                if (hidePlaceholders) HideImage(slot);
                // 칸 i의 꼬마 = 편성 칸 i (Formation)
                var img = MakeImage("Ggoma", slot, Formation.Icon(Formation.Get(i % Formation.SlotCount)), new Vector2(ggomaSize, ggomaSize));
                ggomas.Add(new Ggoma
                {
                    rt = img.rectTransform,
                    img = img,
                    slot = i % Formation.SlotCount,
                    timer = Random.Range(0f, 0.8f),     // 다 같이 던지지 않게 처음만 어긋나게
                    phase = i * 1.3f,
                    harvester = harvesterSprites.Length > 0 ? harvesterSprites[i % harvesterSprites.Length] : white,
                });
            }
            RefreshGgomas();
            BuildHud();
            AddView(transform);
            AddView(enemyArea);
            if (ggomaSlots.Length > 0 && ggomaSlots[0] != null) AddView(ggomaSlots[0].parent);
        }

        void Start()
        {
            stage = IdleProgress.Stage;
            partyMaxHp = partyHp = IdleProgress.FinalHp;
            StartWave(1);

            IdleAway.ApplyOffline();
            if (homeTabs != null)
            {
                homeTabs.Changed += OnTabChanged;
                SetHome(homeTabs.Current < 0);
            }
            else SetHome(true);
        }

        void OnDestroy()
        {
            if (homeTabs != null) homeTabs.Changed -= OnTabChanged;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                FlushGold();
                IdleAway.MarkSeen();
                return;
            }
            IdleAway.ApplyOffline();
            if (atHome) ShowReport();
        }

        void OnApplicationQuit()
        {
            FlushGold();
            IdleAway.MarkSeen();
        }

        // ── 홈 / 다른 탭 ──

        void AddView(Transform t)
        {
            if (t == null) return;
            if (!t.TryGetComponent<CanvasGroup>(out var g)) g = t.gameObject.AddComponent<CanvasGroup>();
            if (!views.Contains(g)) views.Add(g);
        }

        void OnTabChanged(int tab) => SetHome(tab < 0);

        void SetHome(bool home)
        {
            atHome = home;
            foreach (var g in views)
            {
                g.alpha = home ? 1f : 0f;
                g.blocksRaycasts = home;
                g.interactable = home;
            }
            if (!home)
            {
                if (statsPanel != null) statsPanel.SetActive(false);   // 전투력 통계는 홈 전용 — 다른 탭에서 돌아왔을 때 열린 채로 남지 않게
                IdleAway.Begin();
                return;
            }
            FlushGold();
            ShowReport();
        }

        void ShowReport()
        {
            var report = IdleAway.End();
            if (report == null || report.IsEmpty) return;
            if (reportPopup == null) reportPopup = IdleReportPopup.Create(font);
            reportPopup.Show(report);
        }

        void OnEnable()
        {
            Formation.Changed += RefreshGgomas;
            Equipment.Weapon.Changed += RefreshGgomas;   // 장착한 수확기(무기)가 바뀌면 던지는 그림을 바꾼다
        }

        void OnDisable()
        {
            Formation.Changed -= RefreshGgomas;
            Equipment.Weapon.Changed -= RefreshGgomas;
            FlushGold();
        }

        // 편성대로 그림을 바꾼다. 빈 칸은 숨기고 던지지 않는다.
        // 던지는 수확기 = 장착한 무기 (없으면 처음 그림 그대로)
        void RefreshGgomas()
        {
            var weapon = Equipment.Weapon.Sprite(Equipment.Weapon.Equipped);
            for (int i = 0; i < ggomas.Count; i++)
            {
                var g = ggomas[i];
                g.img.sprite = Formation.Icon(Formation.Get(g.slot));
                g.img.enabled = g.img.sprite != null;
                if (weapon != null) g.harvester = weapon;
                else g.harvester = harvesterSprites.Length > 0 ? harvesterSprites[i % harvesterSprites.Length] : white;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            IdleAway.Tick(dt);
            markSeenIn -= dt;
            if (markSeenIn <= 0f) { markSeenIn = MarkSeenInterval; IdleAway.MarkSeen(); }
            UpdateGgomas(dt);
            UpdateShots(dt);
            UpdateEnemies(dt);
            UpdateParty(dt);
            hitFx.Tick(dt);
            UpdatePopups(dt);
            UpdateFlow(dt);

            powerRefreshIn -= dt;
            if (powerRefreshIn <= 0f) { powerRefreshIn = 0.5f; RefreshPower(); }

            goldFlushIn -= dt;
            if (goldFlushIn <= 0f) { goldFlushIn = 1f; FlushGold(); }   // 처치마다 저장하지 않고 1초에 한 번 모아서 넣는다
        }

        // ── 진행 ──

        void StartWave(int w)
        {
            wave = w;
            waveKilled = 0;
            if (wave > IdleProgress.WavesPerStage)
            {
                waveTotal = 1;
                bossTimeLeft = IdleProgress.BossTime;
                boss = Spawn(true, Vector2.zero);
            }
            else
            {
                waveTotal = IdleProgress.EnemyCount(stage, wave);
                for (int i = 0; i < waveTotal; i++) Spawn(false, FreeSpot());
            }
            RefreshHud();
        }

        void UpdateFlow(float dt)
        {
            if (boss != null && !boss.dead)
            {
                bossTimeLeft -= dt;
                if (bossTimeLeft <= 0f)
                {
                    // 시간 초과: 보스를 거두고 같은 스테이지 처음부터
                    Kill(boss, false);
                    boss = null;
                    ShowText(Center(), "보스 실패!", new Color(1f, 0.45f, 0.4f), 64f, 1.4f);
                    nextWaveIn = waveDelay * 2f;
                    wave = 0;
                    partyHp = partyMaxHp;
                    RefreshHud();
                }
                else RefreshBossBars();
                return;
            }

            if (AliveCount() > 0) return;
            if (nextWaveIn > 0f)
            {
                nextWaveIn -= dt;
                if (nextWaveIn > 0f) return;
                StartWave(wave + 1);
                return;
            }
            nextWaveIn = waveDelay;
        }

        void OnEnemyKilled(Enemy e)
        {
            waveKilled++;
            pendingGold += e.gold;
            IdleAway.AddKill();
            ShowText(e.rt.position, "+" + Wallet.Format(e.gold), new Color(1f, 0.85f, 0.25f), 34f, 0.8f);
            if (e.boss)
            {
                boss = null;
                Wallet.Add(Currency.Mycelite, IdleProgress.BossMycelite(stage));
                IdleAway.AddMycelite(IdleProgress.BossMycelite(stage));
                IdleAway.AddStage();
                partyHp = partyMaxHp;
                stage++;
                IdleProgress.Stage = stage;
                ShowText(Center(), $"스테이지 {stage}", Color.white, 72f, 1.4f);
                wave = 0;
                nextWaveIn = waveDelay * 2f;
            }
            RefreshHud();
        }

        // ── 꼬마 무리 체력: 적 공격·재생·패배 ──

        void UpdateParty(float dt)
        {
            // 체력 강화·보유 효과로 최대 체력이 오르면 늘어난 만큼 바로 채운다
            double max = IdleProgress.FinalHp;
            if (max > partyMaxHp) partyHp += max - partyMaxHp;
            partyMaxHp = max;

            // 적 공격력은 스테이지에만 달렸다 — 스테이지가 바뀔 때만 다시 셈한다
            if (attackStage != stage)
            {
                attackStage = stage;
                enemyAttack = IdleProgress.EnemyAttack(stage);
                bossAttack = IdleProgress.BossAttack(stage);
            }
            double enemyDps = 0;
            foreach (var e in enemies)
                if (!e.dead && e.age > enemyAttackDelay) enemyDps += e.boss ? bossAttack : enemyAttack;
            double hurt = enemyDps * dt;
            partyHp = System.Math.Min(partyMaxHp, partyHp + IdleProgress.FinalHpRegen * dt - hurt);

            // 받은 피해는 0.5초씩 모아서 꼬마 쪽에 띄운다
            pendingHurt += hurt;
            hurtShowIn -= dt;
            if (hurtShowIn <= 0f && pendingHurt >= 1)
            {
                hurtShowIn = 0.5f;
                hurtFlash = 1f;
                ShowText(PartyCenter(), "-" + IdleProgress.FormatNumber(pendingHurt), new Color(1f, 0.35f, 0.35f), 32f, 0.7f);
                pendingHurt = 0;
            }
            hurtFlash = Mathf.Max(0f, hurtFlash - dt * 4f);
            foreach (var g in ggomas) g.img.color = Color.Lerp(Color.white, new Color(1f, 0.5f, 0.5f), hurtFlash);

            if (partyHp <= 0) Defeat();
            RefreshHp(dt);
        }

        int attackStage = -1;
        double enemyAttack, bossAttack;

        // 패배: 적을 거두고(보상 없음) 체력을 채운 뒤 같은 스테이지 1웨이브부터
        void Defeat()
        {
            foreach (var e in enemies) if (!e.dead) Kill(e, false);
            boss = null;
            wave = 0;
            nextWaveIn = waveDelay * 2f;
            partyHp = partyMaxHp;
            pendingHurt = 0;
            ShowText(Center(), "패배! 다시 도전", new Color(1f, 0.45f, 0.4f), 64f, 1.4f);
            RefreshHud();
        }

        void RefreshHp(float dt)
        {
            if (hpFill == null) return;
            hpFill.anchorMax = new Vector2(partyMaxHp <= 0 ? 0f : Mathf.Clamp01((float)(partyHp / partyMaxHp)), 1f);
            hpTextIn -= dt;
            if (hpTextIn > 0f) return;
            hpTextIn = 0.2f;
            hpText.text = $"체력 {IdleProgress.FormatNumber(System.Math.Max(0, partyHp))} / {IdleProgress.FormatNumber(partyMaxHp)}";
        }

        Vector3 PartyCenter()
        {
            if (ggomaSlots.Length > 0 && ggomaSlots[0] != null && ggomaSlots[0].parent is RectTransform area) return area.TransformPoint(area.rect.center);
            return Center();
        }

        void FlushGold()
        {
            if (pendingGold <= 0) return;
            Wallet.Add(Currency.Gold, pendingGold);
            IdleAway.AddGold(pendingGold);
            pendingGold = 0;
        }

        // ── 꼬마 ──

        void UpdateGgomas(float dt)
        {
            float rate = (float)IdleProgress.Value(IdleStat.Speed);
            foreach (var g in ggomas)
            {
                // 숨쉬듯 살짝 위아래 + 던질 때 움찔
                g.punch = Mathf.Max(0f, g.punch - dt * 5f);
                float s = 1f + 0.18f * Mathf.Sin(g.punch * Mathf.PI);
                g.rt.localScale = new Vector3(s, s, 1f);
                g.rt.anchoredPosition = new Vector2(0f, Mathf.Sin(Time.time * 2.4f + g.phase) * 4f);

                if (!g.img.enabled) continue;
                g.timer -= dt;
                if (g.timer > 0f) continue;
                var target = PickTarget();
                if (target == null) { g.timer = 0.1f; continue; }
                g.timer += 1f / Mathf.Max(0.05f, rate);
                g.punch = 1f;
                Throw(g, target);
            }
        }

        // 아직 죽을 만큼 맞지 않은 적 중 아무나. 다 맞을 예정이면 살아 있는 아무나
        Enemy PickTarget()
        {
            pick.Clear();
            foreach (var e in enemies) if (!e.dead && e.age > 0.15f && e.hp - e.incoming > 0) pick.Add(e);
            if (pick.Count == 0) foreach (var e in enemies) if (!e.dead && e.age > 0.15f) pick.Add(e);
            return pick.Count == 0 ? null : pick[Random.Range(0, pick.Count)];
        }

        void Throw(Ggoma g, Enemy target)
        {
            double dmg = IdleProgress.FinalAttack * Formation.AttackMultiplier(Formation.Get(g.slot));   // 꼬마마다 공격 배율
            bool crit = Random.value < IdleProgress.Value(IdleStat.CritChance);
            if (crit) dmg *= IdleProgress.Value(IdleStat.CritDamage);
            if (!target.boss) dmg *= IdleProgress.Value(IdleStat.NormalEnemyDamage);   // 후반 강화: 보스가 아닌 적에게만

            var s = freeShots.Count > 0 ? freeShots.Pop() : NewShot();
            s.img.sprite = g.harvester;
            s.rt.gameObject.SetActive(true);
            s.rt.SetAsLastSibling();
            s.from = ToLayer(g.rt.position);
            s.target = target;
            s.gen = target.gen;
            s.to = ToLayer(target.rt.position);
            s.t = 0f;
            s.spin = Random.Range(540f, 900f);
            s.dmg = dmg;
            s.crit = crit;
            target.incoming += dmg;
            shots.Add(s);
        }

        Shot NewShot()
        {
            var img = MakeImage("Harvester", effectLayer, white, new Vector2(harvesterSize, harvesterSize));
            img.raycastTarget = false;
            return new Shot { rt = img.rectTransform, img = img };
        }

        void UpdateShots(float dt)
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var s = shots[i];
                s.t += dt / flightTime;
                bool same = s.target.gen == s.gen;
                if (same && !s.target.dead) s.to = ToLayer(s.target.rt.position);
                Vector2 to = s.to;
                float k = Mathf.Clamp01(s.t);
                s.rt.anchoredPosition = Vector2.Lerp(s.from, to, k) + Vector2.up * (Mathf.Sin(k * Mathf.PI) * arcHeight);
                s.rt.localEulerAngles = new Vector3(0f, 0f, -s.spin * s.t * flightTime);
                if (s.t < 1f) continue;

                if (same) Hit(s.target, s.dmg, s.crit);
                s.rt.gameObject.SetActive(false);
                freeShots.Push(s);
                shots.RemoveAt(i);
            }
        }

        // ── 적 ──

        Enemy Spawn(bool isBoss, Vector2 local)
        {
            var e = freeEnemies.Count > 0 ? freeEnemies.Pop() : NewEnemy();
            var sprites = isBoss ? bossSprites : enemySprites;
            // 보스는 스테이지 순서대로 돌아가며 나온다 (같은 스테이지를 다시 해도 같은 보스). 일반 적은 무작위
            int pickIndex = isBoss ? (stage - 1) % Mathf.Max(1, sprites.Length) : Random.Range(0, Mathf.Max(1, sprites.Length));
            e.img.sprite = sprites.Length > 0 ? sprites[pickIndex] : white;
            float size = isBoss ? bossSize : enemySize;
            e.rt.sizeDelta = new Vector2(size, size);
            e.hpRoot.gameObject.SetActive(!isBoss);         // 보스 체력은 HUD 막대로 본다
            e.boss = isBoss;
            e.maxHp = e.hp = isBoss ? IdleProgress.BossHp(stage) : IdleProgress.EnemyHp(stage, wave);
            e.gold = isBoss ? IdleProgress.BossGold(stage) : IdleProgress.EnemyGold(stage);
            e.incoming = 0;
            e.gen++;
            e.dead = false;
            e.age = 0f;
            e.flash = 0f;
            e.bob = Random.Range(0f, 6.28f);
            e.home = local;
            e.img.color = Color.white;
            e.rt.anchoredPosition = local;
            e.rt.localScale = Vector3.zero;
            e.rt.gameObject.SetActive(true);
            SetHp(e);
            enemies.Add(e);
            return e;
        }

        Enemy NewEnemy()
        {
            var img = MakeImage("Enemy", enemyArea, white, new Vector2(enemySize, enemySize));
            var rt = img.rectTransform;
            // 적 위치는 영역 가운데 기준 좌표로 둔다
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

            var bg = MakeImage("Hp", rt, white, new Vector2(80f, 10f));
            bg.preserveAspect = false;
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            var bgRt = bg.rectTransform;
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0f);
            bgRt.anchoredPosition = new Vector2(0f, -8f);
            var fill = MakeImage("Fill", bgRt, white, Vector2.zero);
            fill.preserveAspect = false;
            fill.color = new Color(0.95f, 0.3f, 0.3f);
            var fillRt = fill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
            return new Enemy { rt = rt, img = img, hpRoot = bgRt, hpFill = fillRt };
        }

        void Hit(Enemy e, double dmg, bool crit)
        {
            e.incoming = System.Math.Max(0, e.incoming - dmg);
            if (e.dead) return;
            e.hp -= dmg;
            e.flash = 1f;
            hitFx.Play(ToLayer(e.rt.position), Mathf.Min(e.rt.sizeDelta.x, enemySize * 2f), crit);   // 보스는 화면을 덮지 않게 일반 적 2배까지
            ShowText(e.rt.position + (Vector3)(Random.insideUnitCircle * 20f),
                IdleProgress.FormatNumber(dmg) + (crit ? "!" : ""),
                crit ? new Color(1f, 0.55f, 0.15f) : Color.white, crit ? 46f : 34f, 0.6f);
            SetHp(e);
            if (e.hp <= 0) Kill(e, true);
        }

        void Kill(Enemy e, bool reward)
        {
            e.dead = true;
            e.deadT = 0f;
            e.hp = 0;
            if (reward) OnEnemyKilled(e);
        }

        void SetHp(Enemy e)
        {
            float r = e.maxHp <= 0 ? 0f : Mathf.Clamp01((float)(e.hp / e.maxHp));
            e.hpFill.anchorMax = new Vector2(r, 1f);
        }

        void UpdateEnemies(float dt)
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];
                if (e.dead)
                {
                    // 작아지며 사라짐
                    e.deadT += dt / 0.25f;
                    float k = Mathf.Clamp01(1f - e.deadT);
                    e.rt.localScale = new Vector3(k, k, 1f);
                    e.img.color = new Color(1f, 1f, 1f, k);
                    if (e.deadT < 1f) continue;
                    e.rt.gameObject.SetActive(false);
                    freeEnemies.Push(e);
                    enemies.RemoveAt(i);
                    continue;
                }

                e.age += dt;
                float pop = Mathf.Clamp01(e.age / 0.25f);
                float scale = pop < 1f ? Mathf.Sin(pop * Mathf.PI * 0.5f) * 1.1f : 1f;
                e.flash = Mathf.Max(0f, e.flash - dt * 6f);
                scale *= 1f - 0.08f * e.flash;
                e.rt.localScale = new Vector3(scale, scale, 1f);
                e.rt.anchoredPosition = e.home + new Vector2(0f, Mathf.Abs(Mathf.Sin(Time.time * 3f + e.bob)) * 6f);
                e.img.color = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.55f), e.flash);
            }
        }

        int AliveCount()
        {
            int n = 0;
            foreach (var e in enemies) if (!e.dead) n++;
            return n;
        }

        // 영역 안에서 다른 적과 가장 덜 겹치는 자리 (몇 군데 찍어 보고 고른다). 영역 가운데 기준 좌표
        Vector2 FreeSpot()
        {
            var half = enemyArea.rect.size * 0.5f - Vector2.one * (enemySize * 0.5f);
            half = Vector2.Max(half, Vector2.zero);
            Vector2 best = Vector2.zero;
            float bestD = -1f;
            for (int t = 0; t < 12; t++)
            {
                var p = new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
                float d = float.MaxValue;
                foreach (var e in enemies) if (!e.dead) d = Mathf.Min(d, (e.home - p).sqrMagnitude);
                if (d > bestD) { bestD = d; best = p; }
            }
            return best;
        }

        // ── 숫자 팝업 ──

        void ShowText(Vector3 worldPos, string text, Color color, float size, float life)
        {
            var p = freePopups.Count > 0 ? freePopups.Pop() : NewPopup();
            p.text.text = text;
            p.text.color = color;
            p.text.fontSize = size;
            p.pos = ToLayer(worldPos);
            p.t = 0f;
            p.life = life;
            p.text.gameObject.SetActive(true);
            p.text.transform.SetAsLastSibling();
            popups.Add(p);
        }

        Popup NewPopup()
        {
            var t = MakeText("Popup", effectLayer, 34f);
            t.rectTransform.sizeDelta = new Vector2(400f, 80f);
            t.outlineWidth = 0.2f;
            t.outlineColor = new Color32(40, 25, 15, 255);
            return new Popup { text = t };
        }

        void UpdatePopups(float dt)
        {
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                p.t += dt;
                float k = p.t / p.life;
                p.text.rectTransform.anchoredPosition = p.pos + Vector2.up * (60f * Mathf.Sqrt(Mathf.Clamp01(k)));
                var c = p.text.color;
                c.a = k < 0.6f ? 1f : Mathf.Clamp01(1f - (k - 0.6f) / 0.4f);
                p.text.color = c;
                if (k < 1f) continue;
                p.text.gameObject.SetActive(false);
                freePopups.Push(p);
                popups.RemoveAt(i);
            }
        }

        // ── HUD: 스테이지 · 웨이브(또는 보스) · 진행 막대 · 보스 시간 ──

        void BuildHud()
        {
            var root = new GameObject("IdleHud", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(effectLayer, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, hudTop);
            root.sizeDelta = new Vector2(560f, 170f);

            stageText = MakeText("Stage", root, 52f);
            Place(stageText.rectTransform, new Vector2(0f, 0f), new Vector2(560f, 64f));
            waveText = MakeText("Wave", root, 34f);
            Place(waveText.rectTransform, new Vector2(0f, -62f), new Vector2(560f, 44f));
            barFill = MakeBar("Bar", root, new Vector2(0f, -112f), new Vector2(460f, 22f), new Color(0.4f, 0.85f, 0.45f));

            timerRoot = new GameObject("BossTime", typeof(RectTransform)).GetComponent<RectTransform>();
            timerRoot.SetParent(root, false);
            Place(timerRoot, new Vector2(0f, -142f), new Vector2(460f, 40f));
            timerFill = MakeBar("Bar", timerRoot, Vector2.zero, new Vector2(360f, 14f), new Color(0.35f, 0.75f, 1f));
            timerFill.parent.GetComponent<RectTransform>().anchoredPosition = new Vector2(-40f, -12f);
            timerText = MakeText("Sec", timerRoot, 30f);
            Place(timerText.rectTransform, new Vector2(190f, 0f), new Vector2(100f, 40f));

            // 꼬마 무리 체력 막대 (보스 시간 아래)
            hpFill = MakeBar("Hp", root, new Vector2(0f, -192f), new Vector2(460f, 30f), new Color(0.35f, 0.85f, 0.4f));
            hpText = MakeText("HpText", hpFill.parent, 24f);
            var hpTextRt = hpText.rectTransform;
            hpTextRt.anchorMin = Vector2.zero;
            hpTextRt.anchorMax = Vector2.one;
            hpTextRt.offsetMin = hpTextRt.offsetMax = Vector2.zero;

            // 전투력 (체력 막대 아래). 누르면 세부 통계 창을 열고 닫는다
            var power = MakeImage("Power", root, white, new Vector2(460f, 46f));
            power.preserveAspect = false;
            power.color = new Color(0f, 0f, 0f, 0.45f);
            power.raycastTarget = true;
            Place(power.rectTransform, new Vector2(0f, -232f), new Vector2(460f, 46f));
            power.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                statsPanel.SetActive(!statsPanel.activeSelf);
                RefreshPower();
            });
            powerText = MakeText("Text", power.rectTransform, 30f);
            var prt = powerText.rectTransform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = prt.offsetMax = Vector2.zero;
            powerText.color = new Color(1f, 0.85f, 0.35f);

            var panel = MakeImage("Stats", root, white, new Vector2(620f, 330f));
            panel.preserveAspect = false;
            panel.color = new Color(0.08f, 0.06f, 0.05f, 0.82f);
            panel.raycastTarget = true;
            Place(panel.rectTransform, new Vector2(0f, -286f), new Vector2(620f, 330f));
            panel.gameObject.AddComponent<Button>().onClick.AddListener(() => statsPanel.SetActive(false));   // 창을 눌러도 닫힌다
            statsPanel = panel.gameObject;
            statsText = MakeText("Text", panel.rectTransform, 26f);
            statsText.alignment = TextAlignmentOptions.TopLeft;
            var srt = statsText.rectTransform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(20f, 16f);
            srt.offsetMax = new Vector2(-20f, -16f);
            statsPanel.SetActive(false);
            RefreshPower();
        }

        void RefreshPower()
        {
            if (powerText == null) return;
            powerText.text = "전투력 " + CombatPower.Format(CombatPower.Total);
            if (statsPanel.activeSelf) statsText.text = CombatPower.Describe();
        }

        void RefreshHud()
        {
            if (stageText == null) return;
            stageText.text = $"스테이지 {stage}";
            bool isBoss = boss != null && !boss.dead;
            timerRoot.gameObject.SetActive(isBoss);
            if (isBoss)
            {
                waveText.text = "보스 등장!";
                waveText.color = new Color(1f, 0.6f, 0.45f);
                shownBossSec = -1;
                RefreshBossBars();
            }
            else
            {
                int w = Mathf.Clamp(wave, 1, IdleProgress.WavesPerStage);
                waveText.text = $"웨이브 {w}/{IdleProgress.WavesPerStage}";
                waveText.color = Color.white;
                barFill.anchorMax = new Vector2(waveTotal <= 0 ? 0f : Mathf.Clamp01((float)waveKilled / waveTotal), 1f);
            }
        }

        // 보스전 중 매 프레임: 막대만 움직이고, 남은 초 글자는 초가 바뀔 때만 새로 쓴다 (매 프레임 문자열을 만들지 않게)
        void RefreshBossBars()
        {
            if (stageText == null || boss == null) return;
            barFill.anchorMax = new Vector2(Mathf.Clamp01((float)(boss.hp / boss.maxHp)), 1f);
            timerFill.anchorMax = new Vector2(Mathf.Clamp01(bossTimeLeft / IdleProgress.BossTime), 1f);
            int sec = Mathf.CeilToInt(bossTimeLeft);
            if (sec == shownBossSec) return;
            shownBossSec = sec;
            timerText.text = sec + "초";
        }

        int shownBossSec = -1;

        // ── 만들기 도우미 ──

        RectTransform MakeBar(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var bg = MakeImage(name, parent, white, size);
            bg.preserveAspect = false;
            bg.color = new Color(0f, 0f, 0f, 0.5f);
            Place(bg.rectTransform, pos, size);
            var fill = MakeImage("Fill", bg.rectTransform, white, Vector2.zero);
            fill.preserveAspect = false;
            fill.color = color;
            var rt = fill.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static Image MakeImage(string name, Transform parent, Sprite sprite, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.rectTransform.sizeDelta = size;
            return img;
        }

        TMP_Text MakeText(string name, Transform parent, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.outlineWidth = 0.25f;
            t.outlineColor = new Color32(40, 25, 15, 255);
            return t;
        }

        static void HideImage(Component c)
        {
            if (c != null && c.TryGetComponent<Image>(out var img)) img.enabled = false;
        }

        Vector2 ToLayer(Vector3 world) => effectLayer.InverseTransformPoint(world) - (Vector3)effectLayer.rect.center;

        Vector3 Center() => enemyArea.TransformPoint(enemyArea.rect.center);
    }
}
