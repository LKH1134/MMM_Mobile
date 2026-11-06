using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // Main 씬 방치형 전투. 꼬마(GgomaArea의 칸마다 1명)가 알아서 수확기를 던지고, 적은 EnemyArea 안 아무 데나 웨이브로 나온다.
    // 웨이브 IdleProgress.WavesPerStage(stage)개를 넘기면 보스 1마리 — 제한 시간 안에 잡으면 다음 스테이지, 못 잡으면 같은 스테이지 1웨이브부터.
    // 살아 있는 적은 꼬마 무리(체력 하나를 같이 쓴다)를 초당 공격하고, 체력은 초마다 재생된다. 체력이 0이 되면 패배 — 같은 스테이지 1웨이브부터.
    // 체력은 스테이지를 넘기거나 패배·보스 실패 뒤에 가득 찬다.
    // 적·수확기·숫자 팝업은 전부 코드로 만들고 풀에 돌려 쓴다. 그림은 임시 리소스(Resources 경로)를 쓴다.
    // 전투는 홈(하단바 TabGroup에 열린 탭이 없을 때)에서만 보인다. 다른 탭을 여는 동안에도 보이지 않게 계속 진행하고,
    // 홈으로 돌아오면 그동안(앱 밖에 있던 시간 포함, IdleAway) 번 것을 정산 팝업으로 보여 준다
    public partial class IdleBattle : MonoBehaviour
    {
        [Header("배치 (씬)")]
        [SerializeField] RectTransform enemyArea;
        [SerializeField] RectTransform[] ggomaSlots;
        [SerializeField] RectTransform effectLayer;         // 수확기·팝업·HUD가 올라가는 판 (비우면 이 오브젝트)
        [SerializeField] TMP_FontAsset font;
        [SerializeField] bool hidePlaceholders = true;      // 칸·영역에 임시로 넣어 둔 그림을 숨긴다
        [SerializeField] TabGroup homeTabs;                 // Main 하단바 — 열린 탭이 없을 때(홈)만 전투를 보인다 (비우면 늘 보인다)

        [Header("리소스 (Resources 경로)")]
        [SerializeField] string kindFolder = "Art/Dream/Characters/Enumy";     // 식용·독버섯·약용버섯 군락 시트 (적 시트 mushroom_ed·tx·md행 = 세계수 창고)
        [SerializeField] string harvesterFolder = "Art/Dream/Harvesters";

        [Header("크기·연출")]
        [SerializeField] float ggomaSize = 169f;
        [SerializeField] float enemySize = 121f;
        [SerializeField] float bossSize = 560f;
        [SerializeField] float harvesterSize = 64f;
        // flightTime·waveDelay·enemyAttackDelay는 방치전투 시트 값을 쓴다 (Awake에서 덮어씀, 인스펙터 값은 표가 없을 때만)
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
            public MushroomKind kind;   // 그림 이름으로 정한다 (꼬마 스킬 식용·독성·약용 피해)
            public float bob, flash, age, deadT;
            public Vector2 home;
            public long gold;
            public int gen;             // 풀에서 다시 꺼낼 때마다 +1 — 날아가던 수확기가 다른 적을 맞히지 않게
        }

        class Shot { public RectTransform rt; public Image img; public Enemy target; public int gen; public Vector2 from, to; public float t, spin; public double dmg, splash; public bool crit; }
        class Popup { public TMP_Text text; public Vector2 pos; public float t, life; }

        readonly List<Ggoma> ggomas = new List<Ggoma>();
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<Popup> popups = new List<Popup>();
        readonly Stack<Enemy> freeEnemies = new Stack<Enemy>();
        readonly Stack<Shot> freeShots = new Stack<Shot>();
        readonly Stack<Popup> freePopups = new Stack<Popup>();
        readonly List<Enemy> pick = new List<Enemy>();

        Sprite[] enemySprites, harvesterSprites;
        readonly Dictionary<int, Sprite> bossSprites = new Dictionary<int, Sprite>();   // 보스.index → 그림 (표: 스테이지.bossId → 보스.resourceId 시트의 cell 조각)
        Sprite[] toxicSprites, medicinalSprites;    // 일반 적 중 독성·약용 버섯 (상수 enemyToxicRate·enemyMedicinalRate 비율로 나온다)
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
        TMP_Text stageText, timerText, hpText, powerText, statsText;
        RectTransform barFill, timerRoot, timerFill, hpFill;
        Image barFillImage, bossMark;
        readonly List<RectTransform> ticks = new List<RectTransform>();     // 진행 막대의 웨이브 구분 눈금
        int shownTickWaves = -1;
        GameObject statsPanel;

        // 스테이지 배너: "스테이지 챕터-번호" (방치전투 시트 stagesPerChapter. 시트의 chapterName은 배너에 쓰지 않는다)
        static int StagesPerChapter => Mathf.Max(1, GameBalance.IdleBattle.stagesPerChapter);
        const string MapIcon = "UI/Icon/quest_scroll";                 // 임시 그림
        const string PowerIcon = "UI/Icon/workshop_hammer";       // 임시 그림
        const float PowerPillY = -967f;     // 강화 목록(UpgradePanel) 위쪽 가장자리 — 전투 화면과 강화 목록 사이
        static readonly Color NormalFill = new Color(0.2f, 0.85f, 0.8f);
        static readonly Color BossFill = new Color(0.95f, 0.35f, 0.35f);
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
            var rule = GameBalance.IdleBattle;
            flightTime = Mathf.Max(0.01f, (float)rule.flightTime);
            waveDelay = Mathf.Max(0f, (float)rule.waveDelay);
            enemyAttackDelay = Mathf.Max(0f, (float)rule.enemyAttackDelay);
            if (enemyArea == null)
            {
                // 적이 나올 자리가 없으면 매 프레임 NullReference가 난다 — 전투를 끄고 한 번만 알린다
                Debug.LogError("[IdleBattle] enemyArea가 비어 있어 방치 전투를 끈다 (씬에서 EnemyArea를 연결한다)");
                enabled = false;
                return;
            }
            if (effectLayer == null) effectLayer = (RectTransform)transform;

            // 전투 그림은 매 프레임 움직인다 → 따로 캔버스를 둬서 같은 캔버스의 Main UI(강화 목록·하단바·재화 바…)가
            // 프레임마다 다시 묶이지 않게 한다. HUD의 전투력 버튼 때문에 이 오브젝트만 입력(GraphicRaycaster)을 받는다.
            // 아래에서 글자·그림을 만들기 전에 붙인다 — 만든 뒤에 붙이면 그 전에 만든 HUD 글자가 새 캔버스에서 빠져 안 보이는 일이 있었다
            AddSubCanvas(transform, true);
            AddSubCanvas(enemyArea, false);
            if (ggomaSlots.Length > 0 && ggomaSlots[0] != null) AddSubCanvas(ggomaSlots[0].parent, false);

            hitFx = new HitEffects(effectLayer);
            white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            // 보스로 쓰는 조각(보스 시트 resourceId + cell)은 일반 적으로 나오지 않게 뺀다
            var kinds = System.Array.FindAll(Resources.LoadAll<Sprite>(kindFolder), sp => !GameBalance.IsBossSprite(sp.name));
            enemySprites = System.Array.FindAll(kinds, sp => GgomaSkills.KindOf(sp.name) == MushroomKind.Edible);
            toxicSprites = System.Array.FindAll(kinds, sp => GgomaSkills.KindOf(sp.name) == MushroomKind.Toxic);
            medicinalSprites = System.Array.FindAll(kinds, sp => GgomaSkills.KindOf(sp.name) == MushroomKind.Medicinal);
            harvesterSprites = Resources.LoadAll<Sprite>(harvesterFolder);
            if (enemySprites.Length == 0) Debug.LogWarning($"[IdleBattle] 식용 적 그림 없음: Resources/{kindFolder}");

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
            // [UI 개편 2026-11-04] HUD·퀘스트·도감 버튼은 프리팹 MainHud(MainHudView)가 그린다 — 여기서 만들지 않는다 (UI_REWORK_LOG.md)
            AddView(transform);
            AddView(enemyArea);
            if (ggomaSlots.Length > 0 && ggomaSlots[0] != null) AddView(ggomaSlots[0].parent);
        }

        static void AddSubCanvas(Transform t, bool raycast)
        {
            if (t == null || t.GetComponent<Canvas>() != null) return;
            var parent = t.parent != null ? t.parent.GetComponentInParent<Canvas>() : null;
            var canvas = t.gameObject.AddComponent<Canvas>();
            if (parent != null) canvas.additionalShaderChannels = parent.rootCanvas.additionalShaderChannels;
            if (raycast) t.gameObject.AddComponent<GraphicRaycaster>();
        }

        void Start()
        {
            stage = IdleProgress.Stage;
            partyMaxHp = partyHp = IdleProgress.FinalHp;

            // 앱 밖 보상·탭 연결은 첫 웨이브보다 먼저 한다 — 첫 웨이브에서 예외가 나도 보상과 홈/탭 전환은 살아 있게
            IdleAway.ApplyOffline();
            if (homeTabs != null)
            {
                homeTabs.Changed += OnTabChanged;
                SetHome(homeTabs.Current < 0);
            }
            else SetHome(true);

            SubscribeDungeon();     // 방치 화면 던전 (IdleBattle.Dungeon.cs)
            StartWave(1);
        }

        void OnDestroy()
        {
            UnsubscribeDungeon();
            if (homeTabs != null) homeTabs.Changed -= OnTabChanged;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                BankLevelReward();      // 던전 5 중이면 모은 보상을 먼저 지급 (앱이 이대로 꺼져도 남게)
                FlushGold();
                IdleAway.MarkSeen();
                SaveScheduler.Flush();      // SaveScheduler의 OnApplicationPause가 이보다 먼저 불렸어도 여기 값까지 저장되게
                return;
            }
            IdleAway.ApplyOffline();
            if (atHome) ShowReport();
        }

        void OnApplicationQuit()
        {
            BankLevelReward();      // 던전 5 보상을 저장 전에 지급
            FlushGold();
            IdleAway.MarkSeen();
            SaveScheduler.Flush();
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
            if (reportPopup != null && reportPopup.IsOpen) return;     // 던전 결과·정산 창이 떠 있으면 정산은 다음에 (번 것은 IdleAway가 계속 모은다)
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
            AbortDungeon();         // 꺼지면(프레임 예외로 멈춤 포함) 던전을 끝낸다 — 그대로 두면 다른 던전에 들어갈 수 없다
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

        // 한 프레임에서 예외가 나도 다음 프레임은 다시 돈다. 계속 나면(표가 틀림 등) 로그가 쏟아져 기기가 버벅이므로
        // 오류 점수가 MaxFrameErrors에 닿으면 전투를 멈춘다 (앱은 계속 쓸 수 있다). 점수는 예외마다 +1, 정상 프레임마다 조금씩 준다 —
        // 한 프레임 걸러 나는 오류도 쌓이게
        const float MaxFrameErrors = 30f, ErrorDecayPerFrame = 0.05f;
        float frameErrors;

        void Update()
        {
            try
            {
                Tick();
                if (frameErrors > 0f) frameErrors = Mathf.Max(0f, frameErrors - ErrorDecayPerFrame);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                if (++frameErrors < MaxFrameErrors) return;
                Debug.LogError("[IdleBattle] 매 프레임 예외가 나서 방치 전투를 멈춘다");
                enabled = false;
            }
        }

        void Tick()
        {
            float dt = Time.deltaTime;
            if (!InDungeon) IdleAway.Tick(dt);     // 던전 중에는 스테이지 골드를 벌지 않는다 — 시간만 쌓여 앱 밖 골드 속도가 깎이지 않게
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
            if (wave > IdleProgress.WavesPerStage(stage))
            {
                waveTotal = 1;
                bossTimeLeft = bossTimeTotal = IdleProgress.BossTime(stage);
                boss = Spawn(true, Vector2.zero);
                DailyQuests.Add(QuestKind.Stage);   // 보스 도전 — 벽에서 막혀도 하루 퀘스트는 깬다
                WorldTree.QuestStage();              // 세계수 의뢰 '보스 도전'도 같은 지점
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
            if (InDungeon) { DungeonFlow(dt); return; }
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
            if (InDungeon && e.boss) { OnDungeonBossKilled(e); return; }
            waveKilled++;
            pendingGold = IdleProgress.SaturatingAdd(pendingGold, e.gold);     // 아주 높은 스테이지에서 넘쳐 음수가 되면 골드가 사라진다
            IdleAway.AddKill();
            DailyQuests.Add(QuestKind.Kill);
            if (!e.boss && e.img.sprite != null) WorldTree.AddHarvest(e.img.sprite.name);     // 세계수 창고 버섯
            ShowText(e.rt.position, "+" + Wallet.Format(e.gold), new Color(1f, 0.85f, 0.25f), 34f, 0.8f);
            if (e.boss)
            {
                boss = null;
                // 세계수 보스 균사석 배율: 소수 부분은 확률로 (보스 균사석이 작아도 효과가 기대값으로 남게)
                double myceliteRaw = IdleProgress.BossMycelite(stage) * WorldTree.Mul(TreeEffect.BossMycelite);
                long mycelite = IdleProgress.ClampToLong(System.Math.Floor(myceliteRaw));
                if (Random.value < myceliteRaw - System.Math.Floor(myceliteRaw)) mycelite++;
                Wallet.Add(Currency.Mycelite, mycelite);
                IdleAway.AddMycelite(mycelite);
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
                if (!e.dead && e.age > enemyAttackDelay) enemyDps += e.boss ? (InDungeon ? dungeonBossDps : bossAttack) : enemyAttack;
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
            if (InDungeon) { FinishDungeon(!IsRush, true); return; }   // 레벨업 보스는 잡은 만큼 받는다
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
            // 체력이 가득 찬 채로 있는 때가 많다 — 보이는 값이 그대로면 글자를 다시 만들지 않는다
            double shownHp = System.Math.Max(0, partyHp);
            if (shownHp == lastHpShown && partyMaxHp == lastMaxHpShown) return;
            lastHpShown = shownHp;
            lastMaxHpShown = partyMaxHp;
            hpText.text = $"체력 {IdleProgress.FormatNumber(shownHp)} / {IdleProgress.FormatNumber(partyMaxHp)}";
        }

        double lastHpShown = -1, lastMaxHpShown = -1;

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
            float rate = (float)IdleProgress.FinalSpeed;
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
                g.timer += 1f / Mathf.Max(0.05f, rate * (float)GgomaSkills.SpeedMul(Formation.Get(g.slot)));   // 꼬마 스킬 공격 속도
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
            string id = Formation.Get(g.slot);
            double dmg = IdleProgress.FinalAttack * Formation.AttackMultiplier(id);   // 꼬마마다 공격 배율
            dmg *= GgomaSkills.AttackMul(id, GgomaSkills.TeamAttack()) * GgomaSkills.TargetMul(id, target.boss, target.kind);   // 꼬마 스킬
            bool crit = Random.value < IdleProgress.FinalCritChance + GgomaSkills.Self(id, GgomaSkillEffect.CritChance);
            if (crit) dmg *= IdleProgress.FinalCritDamage;
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
            s.splash = GgomaSkills.Self(id, GgomaSkillEffect.Splash);
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

                if (same)
                {
                    Vector2 at = ToLayer(s.target.rt.position);
                    Hit(s.target, s.dmg, s.crit);
                    if (s.splash > 0) Splash(s.target, at, s.dmg * s.splash);
                }
                s.rt.gameObject.SetActive(false);
                freeShots.Push(s);
                shots.RemoveAt(i);
            }
        }

        // 꼬마 스킬 범위 피해: 맞은 자리 둘레의 다른 적에게 (보스·일반 같이). Hit이 다음 웨이브를 부를 수 있어 대상을 먼저 모은다
        readonly List<Enemy> splashTargets = new List<Enemy>();

        void Splash(Enemy center, Vector2 at, double dmg)
        {
            float r = (float)GameBalance.Const("ggomaSplashRadius");
            splashTargets.Clear();
            foreach (var e in enemies)
                if (e != center && !e.dead && (ToLayer(e.rt.position) - at).sqrMagnitude <= r * r) splashTargets.Add(e);
            foreach (var e in splashTargets) Hit(e, dmg, false);
            splashTargets.Clear();
        }

        // ── 적 ──

        Enemy Spawn(bool isBoss, Vector2 local)
        {
            var e = freeEnemies.Count > 0 ? freeEnemies.Pop() : NewEnemy();
            if (isBoss)
            {
                // 보스 그림 = 스테이지.bossId → 보스.resourceId (표). 못 찾으면 식용 적 그림
                var sp = BossSprite(stage);
                e.img.sprite = sp != null ? sp : enemySprites.Length > 0 ? enemySprites[0] : white;
            }
            else
            {
                var sprites = PickEnemyPool();
                e.img.sprite = sprites.Length > 0 ? sprites[Random.Range(0, sprites.Length)] : white;
            }
            float size = isBoss ? bossSize : enemySize;
            e.rt.sizeDelta = new Vector2(size, size);
            e.hpRoot.gameObject.SetActive(!isBoss);         // 보스 체력은 HUD 막대로 본다
            e.boss = isBoss;
            // 보스는 그림이 독버섯 군락이어도 식용으로 친다 (꼬마 스킬의 독·약용 배율을 보스에 걸지 않는다 — 옛 보스 그림과 같은 밸런스)
            e.kind = isBoss ? MushroomKind.Edible : GgomaSkills.KindOf(e.img.sprite != null ? e.img.sprite.name : null);
            e.maxHp = e.hp = isBoss ? IdleProgress.BossHp(stage) : IdleProgress.EnemyHp(stage, wave);
            e.gold = IdleProgress.ClampToLong((isBoss ? IdleProgress.BossGold(stage) : IdleProgress.EnemyGold(stage)) * WorldTree.Mul(TreeEffect.KillGold), 1);
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

        // 스테이지의 보스 그림 (스테이지.bossId → 보스.resourceId 시트의 cell 조각). 보스마다 한 번만 읽는다
        Sprite BossSprite(int stageNumber)
        {
            int bossId = GameBalance.Stage(stageNumber).bossId;
            if (bossSprites.TryGetValue(bossId, out var cached)) return cached;
            var boss = GameBalance.Boss(bossId);
            string path = boss != null ? GameBalance.ResourcePath(boss.resourceId) : null;
            string cell = GameBalance.BossSpriteName(boss);
            Sprite sp = null;
            if (path != null) foreach (var s in Resources.LoadAll<Sprite>(path)) if (s.name == cell) { sp = s; break; }
            if (sp == null) Debug.LogWarning($"[IdleBattle] 보스 그림 없음: 스테이지 {stageNumber} bossId {bossId} → '{path}' 조각 '{cell}'");
            bossSprites[bossId] = sp;
            return sp;
        }

        // 일반 적 그림 묶음: 독성·약용은 상수 비율대로, 나머지는 식용 (그림이 없으면 식용)
        Sprite[] PickEnemyPool()
        {
            float r = Random.value;
            float toxic = (float)GameBalance.Const("enemyToxicRate"), medicinal = (float)GameBalance.Const("enemyMedicinalRate");
            if (r < toxic && toxicSprites != null && toxicSprites.Length > 0) return toxicSprites;
            if (r >= toxic && r < toxic + medicinal && medicinalSprites != null && medicinalSprites.Length > 0) return medicinalSprites;
            return enemySprites;
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
            if (e.boss && InDungeon) dmg *= 1 - dungeonDefense;     // 던전 보스 방어력
            e.hp -= dmg;
            e.flash = 1f;
            // 공격 속도가 높으면 초당 수십 번 맞는다 — 이펙트·숫자는 동시에 보이는 수를 묶어 둔다 (치명타는 조금 더 허용)
            if (hitFx.Playing < MaxHitFx) hitFx.Play(ToLayer(e.rt.position), Mathf.Min(e.rt.sizeDelta.x, enemySize * 2f), crit);   // 보스는 화면을 덮지 않게 일반 적 2배까지
            if (popups.Count < (crit ? MaxPopups + MaxPopups / 2 : MaxPopups))
                // 흔들림은 레이어(UI) 단위로 — 월드 단위로 20을 더하면 캔버스 배율 때문에 수천 px 밖(화면 가장자리·바깥)에 찍힌다
                ShowText(e.rt.position + effectLayer.TransformVector(Random.insideUnitCircle * 20f),
                    IdleProgress.FormatNumber(dmg) + (crit ? "!" : ""),
                    crit ? new Color(1f, 0.55f, 0.15f) : Color.white, crit ? 46f : 34f, 0.6f);
            SetHp(e);
            if (e.hp <= 0) Kill(e, true);
        }

        const int MaxHitFx = 16, MaxPopups = 24;

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
            p.text.canvasRenderer.SetAlpha(1f);
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
                // 흐려지기는 CanvasRenderer 알파로 — TMP color를 바꾸면 글자 메시를 매 프레임 새로 만든다
                if (k >= 0.6f) p.text.canvasRenderer.SetAlpha(Mathf.Clamp01(1f - (k - 0.6f) / 0.4f));
                if (k < 1f) continue;
                p.text.gameObject.SetActive(false);
                freePopups.Push(p);
                popups.RemoveAt(i);
            }
        }

        // ── HUD ──
        // [UI 개편 2026-11-04] 예전 코드 HUD(BuildHud: 배너·진행 막대·체력·전투력·통계 창·퀘스트·도감 버튼)는 지웠다.
        // 화면은 프리팹 MainHud가 맡고, 값은 IdleBattle.Hud.cs의 Hud* 속성으로 읽는다. 아래 Refresh*는 HUD 필드가 비어 있으면 아무것도 하지 않는다

        static string StageLabel(int stage)
        {
            int s = Mathf.Max(1, stage) - 1;
            return $"<color=#BA8CF2>스테이지</color> {s / StagesPerChapter + 1}-{s % StagesPerChapter + 1}";
        }

        // 진행 막대에 웨이브 사이 눈금을 (waves - 1)개 둔다
        void ShowTicks(int waves)
        {
            if (waves == shownTickWaves) return;
            shownTickWaves = waves;
            var track = (RectTransform)barFill.parent;
            for (int i = 0; i < waves - 1; i++)
            {
                if (i >= ticks.Count)
                {
                    var tick = MakeImage("Tick", track, white, new Vector2(6f, track.sizeDelta.y + 8f));
                    tick.preserveAspect = false;
                    tick.color = new Color(0.15f, 0.12f, 0.2f);
                    var trt = tick.rectTransform;
                    trt.anchorMin = trt.anchorMax = new Vector2(0f, 0.5f);
                    ticks.Add(trt);
                }
                ticks[i].gameObject.SetActive(true);
                ticks[i].anchoredPosition = new Vector2(track.sizeDelta.x * (i + 1) / waves, 0f);
            }
            for (int i = Mathf.Max(0, waves - 1); i < ticks.Count; i++) ticks[i].gameObject.SetActive(false);
        }

        void RefreshPower()
        {
            if (powerText == null) return;
            double total = CombatPower.Total;
            if (total != shownPower)
            {
                shownPower = total;
                powerText.text = CombatPower.Format(total);
            }
            if (statsPanel.activeSelf) statsText.text = CombatPower.Describe();
        }

        double shownPower = double.NaN;

        void RefreshHud()
        {
            if (stageText == null) return;
            stageText.text = InDungeon ? DungeonLabel() : StageLabel(stage);
            bool isBoss = boss != null && !boss.dead;
            bool bossUi = isBoss || InDungeon;      // 던전은 보스와 보스 사이에도 보스 막대 모양을 유지한다 (일반 웨이브 칸이 깜빡이지 않게)
            timerRoot.gameObject.SetActive(bossUi);
            barFillImage.color = bossUi ? BossFill : NormalFill;
            bossMark.color = bossUi ? BossFill : new Color(0.55f, 0.2f, 0.25f);
            if (isBoss)
            {
                ShowTicks(1);
                shownBossSec = -1;
                RefreshBossBars();
            }
            else if (bossUi)
            {
                // 다음 보스를 기다리는 중: 체력 막대는 비우고 시간 막대는 가득 (다음 보스가 나오면 RefreshBossBars가 채운다)
                ShowTicks(1);
                barFill.anchorMax = new Vector2(0f, 1f);
                timerFill.anchorMax = Vector2.one;
            }
            else
            {
                int waves = IdleProgress.WavesPerStage(stage);
                int w = Mathf.Clamp(wave, 1, waves);
                ShowTicks(waves);
                float inWave = waveTotal <= 0 ? 0f : Mathf.Clamp01((float)waveKilled / waveTotal);
                barFill.anchorMax = new Vector2(Mathf.Clamp01((w - 1 + inWave) / waves), 1f);
            }
        }

        // 보스전 중 매 프레임: 막대만 움직이고, 남은 초 글자는 초가 바뀔 때만 새로 쓴다 (매 프레임 문자열을 만들지 않게)
        void RefreshBossBars()
        {
            if (stageText == null || boss == null) return;
            barFill.anchorMax = new Vector2(boss.maxHp > 0 ? Mathf.Clamp01((float)(boss.hp / boss.maxHp)) : 0f, 1f);
            timerFill.anchorMax = new Vector2(Mathf.Clamp01(bossTimeLeft / Mathf.Max(0.01f, bossTimeTotal)), 1f);
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
