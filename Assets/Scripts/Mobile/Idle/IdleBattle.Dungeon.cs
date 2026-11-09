using UnityEngine;

namespace MoreMush.Mobile
{
    // 방치 화면 던전 (던전 시트 mode BossRush·BossLevel). DungeonSession.BeginIdle이 알리면 지금 웨이브를 거두고(보상 없음) 던전 보스와 싸운다.
    //   보스 연속전(던전 4): 방치 스테이지 보스 bossRushCount마리가 하나씩 나온다 — 하나를 잡으면 바로 다음 보스. 보스마다 그 스테이지 보스 시간 안에 잡아야 한다.
    //                       잡을 때마다 레벨 보상의 1/n을 받고, 다 잡으면 다음 레벨. 시간 초과·패배면 거기서 끝 (이미 받은 보상은 그대로).
    //   레벨업 보스(던전 5): 보스 한 마리. 체력이 0이 되면 레벨이 오르고 체력이 다시 가득 찬다. 레벨마다 체력·공격력·방어력·공격 속도가 오르고 보상도 커진다.
    //                       제한 시간(bossLvSeconds)이 끝나거나 꼬마 무리가 쓰러지면 끝 — 잡은 레벨들의 보상을 모아 받는다.
    // 끝나면 결과 창을 띄우고 같은 방치 스테이지 1웨이브부터 다시 진행한다. 수치는 DungeonRewards (던전·상수 시트)
    public partial class IdleBattle
    {
        int dungeon;                // 진행 중인 방치 던전 (0 = 없음)
        int dungeonLevel;           // 보스 연속전: 이번 레벨 / 레벨업 보스: 지금 보스 레벨
        int rushIndex;              // 보스 연속전: 지금 보스 번호 (0부터)
        float nextBossIn;           // 보스 연속전: 다음 보스가 나오기까지
        float bossTimeTotal = 1f;   // 지금 보스 시간 막대의 전체 길이
        double dungeonBossDps;      // 던전 보스의 초당 공격
        double dungeonDefense;      // 던전 보스 방어력 (받는 피해 × (1 − 방어력))
        long dungeonGold, dungeonMycelite;          // 이번 던전에서 받은 보상
        double levelGold, levelMycelite;            // 레벨업 보스: 모아 둔 보상 (끝날 때 지급)
        bool dungeonSubscribed;
        DungeonMode dungeonMode;    // 시작할 때 한 번 읽어 둔다 (매 프레임 표를 찾고 문자열을 읽지 않게)

        bool InDungeon => dungeon != 0;
        bool IsRush => dungeonMode == DungeonMode.BossRush;

        void SubscribeDungeon()
        {
            if (dungeonSubscribed) return;
            dungeonSubscribed = true;
            DungeonSession.IdleStarted += BeginDungeon;
            DungeonSession.IdleReady = IdleReady;
            DungeonSession.FinishIdle = FinishIdleByUser;
        }

        // 레이드 도중 나가기 (확인 창 "예"): 모은 보상을 받고 끝낸다
        void FinishIdleByUser(bool showResult)
        {
            if (this == null || !InDungeon) return;
            FinishDungeon(true, showResult);
        }

        void UnsubscribeDungeon()
        {
            if (!dungeonSubscribed) return;
            dungeonSubscribed = false;
            DungeonSession.IdleStarted -= BeginDungeon;
            if (DungeonSession.IdleReady == IdleReady) DungeonSession.IdleReady = null;
            if (DungeonSession.FinishIdle == FinishIdleByUser) DungeonSession.FinishIdle = null;
            // 던전 중에 씬을 떠났다: 이미 받은 보상은 그대로, 레벨업 보스는 모아 둔 보상을 준다
            AbortDungeon();
        }

        bool IdleReady() => this != null && isActiveAndEnabled && !InDungeon;

        // 전투가 꺼지거나(OnDisable·프레임 예외로 멈춤) 사라질 때: 던전을 결과 창 없이 끝낸다 — ActiveIdle이 남아 다른 던전이 막히지 않게
        void AbortDungeon()
        {
            if (!InDungeon) return;
            try { FinishDungeon(false, false); }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                dungeon = 0;
                DungeonSession.EndIdle();
            }
        }

        void BeginDungeon(int d)
        {
            if (InDungeon) return;
            // 전투가 꺼져 있으면(프레임 예외로 멈춤 등) 끝까지 갈 수 없다 — 바로 끝내 다른 던전이 막히지 않게
            if (!isActiveAndEnabled) { DungeonSession.EndIdle(); return; }
            try { StartDungeon(d); }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                dungeon = 0;
                DungeonSession.EndIdle();
            }
        }

        void StartDungeon(int d)
        {
            foreach (var e in enemies) if (!e.dead) Kill(e, false);
            boss = null;
            dungeon = d;
            dungeonMode = DungeonSession.Mode(d);
            dungeonGold = dungeonMycelite = 0;
            levelGold = levelMycelite = 0;
            rushIndex = 0;
            nextBossIn = 0f;
            dungeonLevel = IsRush ? DungeonTickets.Level(d) : 1;
            partyHp = partyMaxHp;
            pendingHurt = 0;
            var row = GameBalance.Dungeon(d);
            ShowText(Center(), row != null && !string.IsNullOrEmpty(row.name) ? row.name : $"던전 {d}", new Color(1f, 0.85f, 0.35f), 72f, 1.4f);
            if (IsRush) SpawnRushBoss();
            else
            {
                bossTimeLeft = bossTimeTotal = DungeonRewards.LevelSeconds;
                SpawnLevelBoss();
            }
            RefreshHud();
        }

        // ── 보스 만들기 ──

        Enemy SpawnDungeonBoss(double hp, int spriteStage)
        {
            var e = Spawn(true, Vector2.zero);
            var sp = BossSprite(spriteStage);
            if (sp != null) e.img.sprite = sp;
            e.maxHp = e.hp = System.Math.Max(1, hp);
            e.gold = 0;     // 던전 보상은 DungeonRewards가 따로 준다
            SetHp(e);
            return e;
        }

        void SpawnRushBoss()
        {
            int n = DungeonRewards.RushCount;
            // 그림은 10스테이지씩 떨어진 보스로 골라 마리마다 다르게 보이게 한다 (표의 보스는 10스테이지마다 바뀐다)
            int spriteStage = Mathf.Max(1, DungeonRewards.Stage(dungeon, dungeonLevel) + 10 * rushIndex);
            boss = SpawnDungeonBoss(DungeonRewards.RushHp(dungeon, dungeonLevel, rushIndex), spriteStage);
            dungeonBossDps = DungeonRewards.RushAttack(dungeon, dungeonLevel, rushIndex);
            dungeonDefense = 0;
            bossTimeLeft = bossTimeTotal = DungeonRewards.RushTime(dungeon, dungeonLevel, rushIndex);
            ShowText(Center(), $"보스 {rushIndex + 1}/{n}", Color.white, 64f, 1.1f);
        }

        void SpawnLevelBoss()
        {
            boss = SpawnDungeonBoss(DungeonRewards.LevelHp(dungeon, dungeonLevel), IdleProgress.Stage);
            ApplyLevelStats();
        }

        void ApplyLevelStats()
        {
            dungeonBossDps = DungeonRewards.LevelAttack(dungeon, dungeonLevel) * DungeonRewards.LevelSpeed(dungeonLevel);
            dungeonDefense = DungeonRewards.LevelDefense(dungeonLevel);
        }

        // ── 진행 (UpdateFlow 대신) ──

        void DungeonFlow(float dt)
        {
            if (IsRush)
            {
                if (boss == null || boss.dead)
                {
                    nextBossIn -= dt;
                    if (nextBossIn <= 0f) { SpawnRushBoss(); RefreshHud(); }
                    return;
                }
                bossTimeLeft -= dt;
                if (bossTimeLeft <= 0f) { FinishDungeon(false, true); return; }
                RefreshBossBars();
                return;
            }

            bossTimeLeft -= dt;
            if (bossTimeLeft <= 0f) { FinishDungeon(true, true); return; }
            RefreshBossBars();
        }

        // 던전 보스를 잡았다 (OnEnemyKilled가 부른다)
        void OnDungeonBossKilled(Enemy e)
        {
            if (IsRush)
            {
                DungeonSession.PayEntry(dungeon);      // 첫 보상을 받는 지금 입장 횟수를 쓴다
                var r = DungeonRewards.GrantRushBoss(dungeon, dungeonLevel, rushIndex);
                AddDungeonReward(r.gold, r.mycelite, e);
                rushIndex++;
                boss = null;
                if (rushIndex >= DungeonRewards.RushCount) { FinishDungeon(true, true); return; }
                nextBossIn = waveDelay;
                RefreshHud();
                return;
            }

            // 레벨업 보스: 보상을 모으고, 그 자리에서 레벨을 올려 체력을 다시 채운다 (날아가던 수확기도 그대로 맞는다)
            var reward = DungeonRewards.LevelReward(dungeon, dungeonLevel);
            levelGold += reward.gold;
            levelMycelite += reward.mycelite;
            dungeonLevel++;
            e.dead = false;
            e.incoming = 0;
            e.maxHp = e.hp = System.Math.Max(1, DungeonRewards.LevelHp(dungeon, dungeonLevel));
            e.flash = 1f;
            SetHp(e);
            boss = e;
            ApplyLevelStats();
            ShowText(Center(), $"레벨 {dungeonLevel}!", new Color(1f, 0.85f, 0.35f), 64f, 1f);
            RefreshHud();
        }

        void AddDungeonReward(long gold, long mycelite, Enemy at)
        {
            dungeonGold = IdleProgress.SaturatingAdd(dungeonGold, gold);
            dungeonMycelite = IdleProgress.SaturatingAdd(dungeonMycelite, mycelite);
            if (gold > 0) ShowText(at.rt.position, "+" + Wallet.Format(gold), new Color(1f, 0.85f, 0.25f), 40f, 1f);
        }

        // 던전 끝. cleared = 보스 연속전을 다 잡았다 (레벨업 보스는 늘 true — 잡은 만큼 받는다), showResult = 결과 창
        void FinishDungeon(bool cleared, bool showResult)
        {
            if (!InDungeon) return;
            int d = dungeon;
            bool rush = IsRush;
            int killed = rushIndex, level = dungeonLevel;
            // 보상·레벨 기록을 먼저 끝낸 뒤 상태를 지운다. 중간에 예외가 나도 dungeon은 이미 0이라 다음 프레임에 다시 끝내지(보상·레벨을 두 번 올리지) 않는다
            try
            {
                if (!rush) BankLevelReward();
            }
            finally
            {
                dungeon = 0;
                DungeonSession.EndIdle();
            }
            if (rush && cleared) DungeonTickets.LevelUp(d);
            try { WorldTree.QuestDungeon(); }
            catch (System.Exception e) { Debug.LogException(e); }

            foreach (var e in enemies) if (!e.dead) Kill(e, false);
            boss = null;
            wave = 0;
            nextWaveIn = waveDelay * 2f;
            partyHp = partyMaxHp;
            pendingHurt = 0;
            if (stageText != null) RefreshHud();
            if (showResult) ShowDungeonResult(d, rush, cleared, killed, level);
        }

        void ShowDungeonResult(int d, bool rush, bool cleared, int killed, int level)
        {
            var row = GameBalance.Dungeon(d);
            string title = row != null && !string.IsNullOrEmpty(row.name) ? row.name : $"던전 {d}";
            var sb = new System.Text.StringBuilder();
            if (rush)
            {
                sb.Append(cleared ? $"레벨 {level} 클리어!" : $"레벨 {level} 도전 실패").Append('\n');
                sb.Append($"보스 처치 {killed}/{DungeonRewards.RushCount}\n\n");
            }
            else
            {
                sb.Append(level - 1 > 0 ? $"레벨 {level - 1}까지 처치" : "처치한 레벨 없음").Append('\n');
                sb.Append($"최고 기록 레벨 {DungeonTickets.BestLevel(d)}\n\n");
            }
            if (dungeonGold > 0) sb.Append("<color=#FFD54A>골드 +").Append(Wallet.Format(dungeonGold)).Append("</color>\n");
            if (dungeonMycelite > 0) sb.Append("<color=#8FE3FF>균사석 +").Append(Wallet.Format(dungeonMycelite)).Append("</color>\n");
            if (dungeonGold <= 0 && dungeonMycelite <= 0) sb.Append("받은 보상 없음\n");
            if (reportPopup == null) reportPopup = IdleReportPopup.Create(font);
            reportPopup.ShowMessage(title, sb.ToString().TrimEnd('\n'));
        }

        // 레벨업 보스: 지금까지 모은 보상을 지갑에 넣고 최고 기록을 남긴다. 앱을 내릴 때도 부른다 (강제 종료되면 OnDestroy가 오지 않는다)
        void BankLevelReward()
        {
            if (!InDungeon || IsRush) return;
            if (levelGold > 0 || levelMycelite > 0) DungeonSession.PayEntry(dungeon);   // 모은 보상을 받는 지금 입장 횟수를 쓴다 (앱을 내릴 때도)
            var r = DungeonRewards.GrantRaw(levelGold, levelMycelite);
            levelGold = levelMycelite = 0;
            dungeonGold = IdleProgress.SaturatingAdd(dungeonGold, r.gold);
            dungeonMycelite = IdleProgress.SaturatingAdd(dungeonMycelite, r.mycelite);
            if (dungeonLevel - 1 > 0) DungeonTickets.RecordBest(dungeon, dungeonLevel - 1);
        }

        string DungeonLabel()
        {
            var row = GameBalance.Dungeon(dungeon);
            string name = row != null && !string.IsNullOrEmpty(row.name) ? row.name : $"던전 {dungeon}";
            return IsRush
                ? $"<color=#FFD54A>{name}</color> {Mathf.Min(rushIndex + 1, DungeonRewards.RushCount)}/{DungeonRewards.RushCount}"
                : $"<color=#FFD54A>{name}</color> Lv.{dungeonLevel}";
        }
    }
}
