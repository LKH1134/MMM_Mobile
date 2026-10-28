using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 밸런스 측정기 (PC판 BalanceSim 방식, 밸런스 지표 이론 문서 5절·9-3절).
    // InGame 씬 플레이 모드에서 실제 판 엔진(HarvestField·SkillEffects·BossGimmicks·HarvestGame)을 헤드리스로 돌려
    // 1스테이지부터 캠페인을 흉내 낸다. 저장값은 건드리지 않는다: 스킬 레벨은 SkillProgress.SimLevels, 지갑·스테이지는 이 클래스 안에서 센다.
    //   조작: 첫 투척은 위로(±약간), 바는 가장 낮게 내려오는 영구 수확기가 닿을 자리를 따라가되 0.5초마다 바뀌는 오차
    //         (오차 폭 = 2 × SKILL × 바 반길이 → 바 받을 확률 ≈ min(1, 0.5/SKILL). 0.6 = 보수적 기준, 1.0 ≈ 사람 쪽)
    //   구매: 판이 끝날 때마다 가장 싼 것부터 살 수 있는 만큼 전부 (골드·균사석 따로). schedule을 주면 그 판에 정해진 것을 돈과 상관없이 산다
    //   진행: 일반 판은 마치면 다음 스테이지,
    //         보스는 처치하면 다음·못 잡으면 바로 재도전 (farmRounds를 주면 그만큼 파밍 뒤) — 게임 규칙과 같음
    //   균사석: 보스·★ 스테이지 첫 클리어만 (StageProgress.FirstClearGem, 다시 넘거나 파밍·재도전은 0)
    //   시간: 판마다 (판 경과 초 + menuSeconds) / 60분
    // 사용: Begin(config) → Run(n)을 끝날 때까지 여러 번 → Save(path) → End()
    public static class BalanceBot
    {
        public class Config
        {
            public int seed = 1;
            public float skill = 0.6f;
            public float menuSeconds = 60f;     // 판 밖 시간 (결과·트리·이동). PC판 사람 한 판 1.35분 ≈ 판 시간 + 약 60초
            public int maxRounds = 300;
            public int stopStage = 50;          // 이 스테이지 보스를 처치하면 끝
            public float incomeMul = 1f;        // 사람 수입 보정 (봇 수입 × 이 값)
            public float bossDamageMul = 1f;    // 사람 보스 피해 보정 (봇 피해 × 이 값. 실제 보스는 체력 ÷ 이 값으로 같은 효과)
            public int farmRounds = 0;          // 보스에 지면 파밍할 판 수. 0 = 결과 팝업에서 "재도전"을 고른 것 (기본)
            public Dictionary<int, List<int>> schedule;   // 판 번호 → 그 판 끝에 살 스킬 index (레벨 하나씩). 있으면 돈 무시
            // 보스 판을 '죽지 않는 보스'로 돌려 판 전체 피해를 잰다. 처치 여부 = 피해 ≥ 실효 체력
            // (실효 체력: 환상 버섯 ×3.27 — 분신 (모바일 실측), 청소부 × (1 + 0.01 × (판 시간 − 2) × 0.7) — 회복. 이론 문서 4-4)
            public bool probeBoss;
            public bool noBuy;                  // 판이 끝나도 사지 않는다 (보스 반복 측정)
        }

        public class RoundLog
        {
            public int round, stage, buys, gemBuys, harvested, harvesters, levels;
            public bool boss, killed;
            public float duration, elapsed, minutes, hpRate, bossHp, bossDamage, attack;
            public long score, gold, bonus, gems, spent, goldLeft, gemsLeft, nextPrice;
        }

        static Config cfg;
        static HarvestField field;
        static HarvestGame game;
        static SkillEffects effects;
        static BossGimmicks gimmicks;
        static StageHud hud;
        static readonly Dictionary<int, int> levels = new Dictionary<int, int>();
        static readonly List<RoundLog> logs = new List<RoundLog>();
        static readonly List<(int round, int index, int level, long price, Currency currency)> purchases = new List<(int, int, int, long, Currency)>();
        static long gold, gems, lastScore, lastGold;
        static int stage, farmLeft, round, cleared;
        static float minutes, bossDamage, errTimer, err;
        static bool done;
        static System.Random rng;

        public static bool Done => done;
        public static IReadOnlyList<RoundLog> Logs => logs;

        public static string Begin(Config config)
        {
            cfg = config ?? new Config();
            field = Object.FindFirstObjectByType<HarvestField>();
            game = Object.FindFirstObjectByType<HarvestGame>();
            effects = Object.FindFirstObjectByType<SkillEffects>();
            gimmicks = Object.FindFirstObjectByType<BossGimmicks>();
            hud = Object.FindFirstObjectByType<StageHud>();
            if (field == null || game == null || hud == null) return "InGame 씬 플레이 모드에서 불러야 한다";
            levels.Clear();
            logs.Clear();
            purchases.Clear();
            SkillProgress.SimLevels = levels;
            game.Simulating = true;
            Random.InitState(cfg.seed);
            rng = new System.Random(cfg.seed);
            gold = gems = lastScore = lastGold = 0;
            stage = 1;
            farmLeft = round = cleared = 0;
            minutes = 0f;
            done = false;
            field.Hit -= OnHit;
            field.Hit += OnHit;
            return $"[BalanceBot] seed={cfg.seed} SKILL={cfg.skill} menu={cfg.menuSeconds}s income×{cfg.incomeMul} boss×{cfg.bossDamageMul} farm={cfg.farmRounds} stop={cfg.stopStage} schedule={(cfg.schedule != null)} stages={StageProgress.TableMaxStage}+ skills={GameData.Skills.Count}";
        }

        public static void End()
        {
            SkillProgress.SimLevels = null;
            if (field != null) field.Hit -= OnHit;
            if (game != null) game.Simulating = false;
        }

        static void OnHit(Mushroom m, Harvester h, int damage, bool crit)
        {
            if (m.IsBoss) bossDamage += damage;
        }

        // n판 진행. 끝났거나 n판을 돌면 돌아온다
        public static string Run(int n)
        {
            for (int i = 0; i < n && !done; i++) PlayRound();
            var last = logs.Count > 0 ? logs[logs.Count - 1] : null;
            return last == null ? "no rounds" :
                $"round {last.round} stage {last.stage} {(last.boss ? (last.killed ? "보스 처치" : "보스 실패") : "일반")} min {last.minutes:0.0} gold {gold} gems {gems} levels {LevelCount()} done={done}";
        }

        static void PlayRound()
        {
            round++;
            var stageData = StageProgress.Get(stage);
            bool bossRound = stageData != null && stageData.boss != 0 && farmLeft == 0;
            game.Prepare(stage, bossRound, lastScore, lastGold);
            // 체력은 int라 1e9를 넘지 않게 (넘으면 넘쳐서 1이 된다)
            if (bossRound && cfg.probeBoss) game.Stats.BossHpMul = Mathf.Max(1f, 1e9f / Mathf.Max(1f, game.Stats.BossHp));
            else if (bossRound) game.Stats.BossHpMul = 1f / Mathf.Max(0.01f, cfg.bossDamageMul);
            field.Restart();
            if (effects != null) effects.Restart();
            if (gimmicks != null) gimmicks.Restart();
            hud.ResetTimer(game.Stats.Duration);
            bossDamage = 0f;
            errTimer = 0f;

            var log = new RoundLog
            {
                round = round, stage = stage, boss = bossRound, duration = game.Stats.Duration, hpRate = game.Stats.HpRate,
                bossHp = game.Stats.BossHp, attack = field.Main != null ? field.Main.Damage : 0, harvesters = field.Harvesters.Count,
            };

            field.LaunchMain(new Vector2((float)(rng.NextDouble() - 0.5) * 0.6f, 1f));
            const float dt = 1f / 60f;
            int maxSteps = Mathf.CeilToInt((game.Stats.Duration + 1f) / dt);
            float elapsed = 0f;
            for (int s = 0; s < maxSteps && !game.Finished; s++)
            {
                MoveBar(dt);
                field.Step(dt);
                if (effects != null) effects.Step(dt);
                if (gimmicks != null) gimmicks.Step(dt);
                hud.Tick(dt);
                elapsed += dt;
            }
            bool probeKill = false;
            if (bossRound && cfg.probeBoss)
            {
                // 죽지 않는 보스로 잰 피해로 처치를 판정하고, 처치면 보상을 게임 규칙대로 셈한다
                probeKill = bossDamage * cfg.bossDamageMul >= EffectiveBossHp(game.Stats);
                if (probeKill) game.ForceBossKill();
            }
            if (!game.Finished) game.Finish();

            log.killed = game.BossKilled || probeKill;
            log.score = game.Score;
            log.gold = game.Gold;
            log.bonus = game.BonusGold;
            log.harvested = game.HarvestCount;
            log.elapsed = elapsed;
            log.bossDamage = bossDamage;
            bool stageCleared = bossRound ? log.killed : game.StageCleared;

            // 정산 (균사석은 처음 넘은 스테이지만)
            gold += (long)(game.TotalGold * cfg.incomeMul);
            log.gems = StageProgress.FirstClearGem(stageData, game.Stats.Boss, stageCleared, cleared);
            gems += log.gems;

            // 진행
            int stageBefore = stage;
            if (stageCleared)
            {
                cleared = Mathf.Max(cleared, stage);
                stage++;
                farmLeft = 0;
            }
            else if (bossRound) farmLeft = cfg.farmRounds;
            else if (stageData != null && stageData.boss != 0) farmLeft = Mathf.Max(0, farmLeft - 1);   // 파밍 판
            if (!bossRound)
            {
                lastScore = game.Score;
                lastGold = game.Gold;
            }
            minutes += (elapsed + cfg.menuSeconds) / 60f;
            log.minutes = minutes;

            // 구매
            Buy(log);
            log.goldLeft = gold;
            log.gemsLeft = gems;
            log.levels = LevelCount();
            log.nextPrice = CheapestPrice(Currency.Gold);
            logs.Add(log);

            if ((bossRound && log.killed && stageBefore >= cfg.stopStage) || round >= cfg.maxRounds) done = true;
        }

        // 기믹을 반영한 보스 체력 (죽지 않는 보스로 잴 때 비교 기준)
        public static float EffectiveBossHp(RoundStats s)
        {
            float hp = s.BossHp;
            switch (s.Boss.gimmick)
            {
                case BossGimmick.Split: return hp * 3.27f;   // PC 추정 ×1.97 → 모바일 실측(실제 보스 실패율로 역산) ×3.27
                case BossGimmick.Heal: return hp * (1f + 0.01f * (s.Duration - 2f) * 0.7f);
                default: return hp;
            }
        }

        // ── 구매 ──
        static void Buy(RoundLog log)
        {
            if (cfg.noBuy) return;
            if (cfg.schedule != null)
            {
                if (cfg.schedule.TryGetValue(round, out var list))
                    foreach (int index in list)
                    {
                        var s = GameData.Skills.Get(index);
                        if (s == null || SkillProgress.IsMaxed(s)) continue;
                        levels[index] = SkillProgress.GetLevel(index) + 1;
                        log.buys++;
                    }
                // 균사석 노드는 일정과 별개로 가장 싼 것부터
                while (BuyCheapest(Currency.Mycelite, log)) { }
                return;
            }
            while (BuyCheapest(Currency.Gold, log)) { }
            while (BuyCheapest(Currency.Mycelite, log)) { }
        }

        static bool BuyCheapest(Currency currency, RoundLog log)
        {
            SkillData best = null;
            long bestPrice = long.MaxValue;
            foreach (var s in GameData.Skills.All)
            {
                if (!SkillProgress.CanLevelUp(s)) continue;
                long p = SkillProgress.NextPrice(s, out var c);
                if (c != currency || p >= bestPrice) continue;
                best = s;
                bestPrice = p;
            }
            if (best == null) return false;
            long have = currency == Currency.Mycelite ? gems : gold;
            if (have < bestPrice) return false;
            if (currency == Currency.Mycelite) { gems -= bestPrice; log.gemBuys++; }
            else { gold -= bestPrice; log.spent += bestPrice; log.buys++; }
            int level = SkillProgress.GetLevel(best.index) + 1;
            levels[best.index] = level;
            purchases.Add((round, best.index, level, bestPrice, currency));
            return true;
        }

        static long CheapestPrice(Currency currency)
        {
            long best = 0;
            foreach (var s in GameData.Skills.All)
            {
                if (!SkillProgress.CanLevelUp(s)) continue;
                long p = SkillProgress.NextPrice(s, out var c);
                if (c == currency && (best == 0 || p < best)) best = p;
            }
            return best;
        }

        static int LevelCount()
        {
            int n = 0;
            foreach (var v in levels.Values) n += v;
            return n;
        }

        // ── 바 조작 ──
        static void MoveBar(float dt)
        {
            if (field.Bar == null) return;
            float half = field.Bar.GetRect(field.Rect).width * 0.5f;
            errTimer -= dt;
            if (errTimer <= 0f)
            {
                errTimer = 0.5f;
                err = (float)(rng.NextDouble() * 2 - 1) * 2f * cfg.skill * half;
            }

            if (AimX(field, out float x)) field.MoveBarTo(x + err, 5000f * dt);
        }

        // 바를 둘 x: 가장 낮게 내려오는 영구 수확기가 바 윗면에 닿을 자리 (옆벽 반사를 접어서). 움직이는 수확기가 없으면 false.
        // 벤치마크(Benchmark)의 자동 조작도 쓴다
        public static bool AimX(HarvestField field, out float x)
        {
            x = 0f;
            if (field.Bar == null) return false;
            Rect r = field.Rect.rect;
            Rect barRect = field.Bar.GetRect(field.Rect);
            Harvester target = null;
            foreach (var h in field.Harvesters)
            {
                if (h.Temporary || !h.IsMoving) continue;
                bool down = h.Velocity.y < 0f;
                if (target == null) { target = h; continue; }
                bool targetDown = target.Velocity.y < 0f;
                if (down && !targetDown) target = h;
                else if (down == targetDown && h.Position.y < target.Position.y) target = h;
            }
            if (target == null) return false;

            x = target.Position.x;
            if (target.Velocity.y < 0f)
            {
                float t = (target.Position.y - barRect.yMax - target.Radius) / -target.Velocity.y;
                float raw = target.Position.x + target.Velocity.x * Mathf.Max(0f, t);
                float lo = r.xMin + target.Radius, hi = r.xMax - target.Radius, w = hi - lo;
                float u = Mathf.Repeat(raw - lo, 2f * w);
                x = lo + (u <= w ? u : 2f * w - u);
            }
            return true;
        }

        // 여러 시드를 한 번에: outPrefix + seed + ".csv"로 저장. bossDamageMul을 비우면 수입 보정과 같은 값.
        // 문서 사람 모델(이론 9-R4): 수입 0.6, 보스 피해 0.234 schedulePath = "판:index,index,…" 줄로 된 파일 (비우면 가장 싼 것부터)
        public static string RunCampaigns(int[] seeds, float skill, float incomeMul, bool probeBoss, string schedulePath, string outPrefix, int maxRounds = 300, float bossDamageMul = -1f)
        {
            Dictionary<int, List<int>> schedule = null;
            if (!string.IsNullOrEmpty(schedulePath))
            {
                schedule = new Dictionary<int, List<int>>();
                foreach (var line in System.IO.File.ReadAllLines(schedulePath))
                {
                    var p = line.Split(':');
                    if (p.Length < 2) continue;
                    var list = new List<int>();
                    foreach (var s in p[1].Split(',')) if (s.Length > 0) list.Add(int.Parse(s));
                    schedule[int.Parse(p[0])] = list;
                }
            }
            var sb = new StringBuilder();
            foreach (int seed in seeds)
            {
                var head = Begin(new Config { seed = seed, skill = skill, incomeMul = incomeMul, bossDamageMul = bossDamageMul >= 0f ? bossDamageMul : incomeMul, probeBoss = probeBoss, schedule = schedule, maxRounds = maxRounds });
                string r = Run(maxRounds);
                sb.AppendLine($"{head}\n  {r}  {Save(outPrefix + seed + ".csv")}");
            }
            End();
            return sb.ToString();
        }

        // 보스 반복 측정: 정해진 스킬 레벨("index:level;…")로 bossStage 보스 판을 죽지 않는 보스로 tries번 돌려 판마다 피해를 한 줄씩 outPath에 쓴다.
        // 체력 맞춤(목표 실패율 = 피해 분포의 분위수)에 쓴다
        public static string ProbeBoss(int bossStage, string levelSpec, long refScore, long refGold, int tries, float skill, int seed, string outPath)
        {
            var head = Begin(new Config { seed = seed, skill = skill, probeBoss = true, noBuy = true, maxRounds = int.MaxValue });
            foreach (var part in levelSpec.Split(';'))
            {
                var kv = part.Split(':');
                if (kv.Length == 2) levels[int.Parse(kv[0])] = int.Parse(kv[1]);
            }
            var sb = new StringBuilder("damage,duration,effectiveHp\n");
            var ci = CultureInfo.InvariantCulture;
            for (int i = 0; i < tries; i++)
            {
                stage = bossStage;
                farmLeft = 0;
                lastScore = refScore;
                lastGold = refGold;
                PlayRound();
                var l = logs[logs.Count - 1];
                sb.AppendLine($"{l.bossDamage.ToString("0", ci)},{l.duration.ToString(ci)},{EffectiveBossHp(game.Stats).ToString("0", ci)}");
            }
            End();
            System.IO.File.WriteAllText(outPath, sb.ToString());
            return $"{head}\n  probed {tries} → {outPath}";
        }

        // ── 기록 ──
        public static string Save(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("round,stage,boss,killed,duration,elapsed,minutes,hpRate,attack,harvesters,score,gold,bonus,gems,buys,gemBuys,spent,goldLeft,gemsLeft,levels,nextPrice,bossHp,bossDamage,harvested");
            var ci = CultureInfo.InvariantCulture;
            foreach (var l in logs)
                sb.AppendLine(string.Join(",", l.round, l.stage, l.boss ? 1 : 0, l.killed ? 1 : 0, l.duration.ToString(ci), l.elapsed.ToString("0.##", ci),
                    l.minutes.ToString("0.##", ci), l.hpRate.ToString(ci), l.attack.ToString(ci), l.harvesters, l.score, l.gold, l.bonus, l.gems,
                    l.buys, l.gemBuys, l.spent, l.goldLeft, l.gemsLeft, l.levels, l.nextPrice, l.bossHp.ToString("0", ci), l.bossDamage.ToString("0", ci), l.harvested));
            System.IO.File.WriteAllText(path, sb.ToString());
            // 구매 기록: <path>_buys.csv
            var bs = new StringBuilder("round,index,level,price,currency\n");
            foreach (var p in purchases) bs.AppendLine($"{p.round},{p.index},{p.level},{p.price},{p.currency}");
            System.IO.File.WriteAllText(path.Replace(".csv", "_buys.csv"), bs.ToString());
            return $"saved {logs.Count} rounds → {path}";
        }
    }
}
