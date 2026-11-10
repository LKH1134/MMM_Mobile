using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    public enum GuideKind { Kill, BossKill, UpgradeLevel, UpgradeCount, Gacha, OpenFormation, Dungeon, StageEnter }

    // 가이드 퀘스트: Main 화면 전투력 위 한 줄 바(GuideBarView)에 목표를 하나씩 보여 준다. 달성하면 균사석을 받고 다음 목표가 이어진다.
    // 처음 목표는 가이드 시트에 적은 순서대로(고정), 그 뒤는 가이드반복 시트 행을 돌며 지금 진행에 맞춰 만든다 (스테이지·강화 레벨이 높을수록 목표 수가 크다).
    // 진행도는 게임 쪽에서 Report로 올린다 — 앱 밖(방치 보상)에서 센 것은 세지 않는다.
    // 저장: PlayerPrefs("Guide.Step" = 받은 목표 수, "Guide.Prog" = 진행도, 만든 목표는 "Guide.Kind/Stat/Target/Reward")
    public static class GuideQuests
    {
        const string StepKey = "Guide.Step", ProgKey = "Guide.Prog", KindKey = "Guide.Kind", StatKey = "Guide.Stat", TargetKey = "Guide.Target", RewardKey = "Guide.Reward";

        public static event Action Changed;

        static bool loaded;
        static int step;
        static GuideKind kind;
        static IdleStat stat;
        static int target, reward, progress;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload() => loaded = false;

        static void Ensure()
        {
            if (loaded) return;
            step = Mathf.Max(0, PlayerPrefs.GetInt(StepKey, 0));
            progress = Mathf.Max(0, PlayerPrefs.GetInt(ProgKey, 0));
            var fixedRows = GameBalance.Guides;
            if (step < fixedRows.Count) SetFromFixed(fixedRows[step]);
            else if (PlayerPrefs.HasKey(KindKey)) LoadGenerated();
            else Generate();
            loaded = true;
        }

        static void SetFromFixed(GuideRow r)
        {
            if (!Enum.TryParse(r.kind, true, out kind)) Debug.LogError($"[GuideQuests] 모르는 가이드 종류 '{r.kind}'");
            if (!Enum.TryParse(r.stat, true, out stat)) stat = IdleStat.Attack;
            target = Mathf.Max(1, r.target);
            reward = Mathf.Max(0, r.mycelite);
        }

        static void LoadGenerated()
        {
            kind = (GuideKind)PlayerPrefs.GetInt(KindKey, 0);
            stat = (IdleStat)PlayerPrefs.GetInt(StatKey, 0);
            target = Mathf.Max(1, PlayerPrefs.GetInt(TargetKey, 1));
            reward = Mathf.Max(0, PlayerPrefs.GetInt(RewardKey, 0));
        }

        // 고정 순서 뒤의 목표: 반복 시트 행을 차례로 돌되, 지금 할 수 없는 종류(해금된 던전·강화가 없을 때)는 건너뛴다
        static void Generate()
        {
            var rows = GameBalance.GuideLoops;
            int loopIndex = step - GameBalance.Guides.Count;
            for (int j = 0; j < rows.Count; j++)
            {
                var row = rows[(loopIndex + j) % rows.Count];
                if (!Enum.TryParse(row.kind, true, out GuideKind k)) continue;
                var s = IdleStat.Attack;
                if (k == GuideKind.UpgradeCount && !PickStat(loopIndex, out s)) continue;
                if (k == GuideKind.Dungeon && !AnyDungeonOpen()) continue;
                kind = k;
                stat = s;
                reward = Mathf.Max(0, row.mycelite);
                target = ScaledTarget(row, k == GuideKind.UpgradeCount ? IdleProgress.Level(s) : 0);
                PlayerPrefs.SetInt(KindKey, (int)kind);
                PlayerPrefs.SetInt(StatKey, (int)stat);
                PlayerPrefs.SetInt(TargetKey, target);
                PlayerPrefs.SetInt(RewardKey, reward);
                return;
            }
            // 할 수 있는 반복 목표가 하나도 없으면 몬스터 처치로
            kind = GuideKind.Kill;
            target = 50;
            reward = 50;
        }

        static int ScaledTarget(GuideLoopRow row, int statLevel)
        {
            double n = row.baseTarget + row.perStage * (IdleProgress.Stage - 1) + row.perLevel * statLevel;
            double unit = Math.Max(1, row.step);
            n = Math.Max(unit, Math.Round(n / unit) * unit);
            return (int)Math.Min(n, 1000000);
        }

        // 올릴 수 있는 강화(열려 있고, 만렙·단계 제한이 아닌 것)를 번호 순서대로 돌려 가며 고른다
        static bool PickStat(int loopIndex, out IdleStat picked)
        {
            var list = new List<IdleStat>();
            foreach (IdleStat s in Enum.GetValues(typeof(IdleStat)))
                if (IdleProgress.Unlocked(s) && !IdleProgress.IsMax(s) && !IdleProgress.IsCapped(s)) list.Add(s);
            picked = list.Count > 0 ? list[loopIndex % list.Count] : IdleStat.Attack;
            return list.Count > 0;
        }

        static bool AnyDungeonOpen()
        {
            foreach (var d in GameBalance.Dungeons)
                if (d.implemented && d.unlockStage <= IdleProgress.Stage) return true;
            return false;
        }

        public static GuideKind Kind { get { Ensure(); return kind; } }
        public static IdleStat Stat { get { Ensure(); return stat; } }
        public static int Target { get { Ensure(); return target; } }
        public static long Reward { get { Ensure(); return reward; } }

        // 강화 레벨 목표는 지금 레벨이 곧 진행도 (이미 넘겼으면 바로 달성)
        public static int Progress
        {
            get
            {
                Ensure();
                return kind == GuideKind.UpgradeLevel ? Mathf.Min(target, IdleProgress.Level(stat)) : Mathf.Min(target, progress);
            }
        }

        public static bool IsDone => Progress >= Target;

        // 이 종류의 진행도를 올린다 (목표 종류가 같을 때만)
        public static void Report(GuideKind reported, int amount = 1)
        {
            if (amount <= 0) return;
            Ensure();
            if (reported != kind || kind == GuideKind.UpgradeLevel || progress >= target) return;
            progress = Mathf.Min(target, progress + amount);
            PlayerPrefs.SetInt(ProgKey, progress);
            SaveScheduler.MarkDirty();      // 처치마다 불린다 — 파일 쓰기는 SaveScheduler가 모아서
            Changed?.Invoke();
        }

        // 강화 한 번: UpgradeCount는 목표 강화일 때만 세고, UpgradeLevel은 레벨이 바뀌었다고 알리기만 한다
        public static void ReportUpgrade(IdleStat upgraded)
        {
            Ensure();
            if (kind == GuideKind.UpgradeLevel) { if (upgraded == stat) Changed?.Invoke(); return; }
            if (kind == GuideKind.UpgradeCount && upgraded == stat) Report(GuideKind.UpgradeCount);
        }

        // 달성한 목표의 보상(균사석)을 받고 다음 목표로. 받았으면 true
        public static bool Claim()
        {
            Ensure();
            if (!IsDone) return false;
            Wallet.Add(Currency.Mycelite, reward);
            step++;
            progress = 0;
            PlayerPrefs.SetInt(StepKey, step);
            PlayerPrefs.SetInt(ProgKey, 0);
            PlayerPrefs.DeleteKey(KindKey);
            var fixedRows = GameBalance.Guides;
            if (step < fixedRows.Count) SetFromFixed(fixedRows[step]);
            else Generate();
            SaveScheduler.MarkDirty();
            Changed?.Invoke();
            return true;
        }

        public static string Describe()
        {
            Ensure();
            int n = target;
            switch (kind)
            {
                case GuideKind.Kill: return Loc.F("guide.kill", n);
                case GuideKind.BossKill: return Loc.F("guide.boss", n);
                case GuideKind.UpgradeLevel: return Loc.F("guide.upgrade_level", IdleProgress.DisplayName(stat), n);
                case GuideKind.UpgradeCount: return Loc.F("guide.upgrade_count", IdleProgress.DisplayName(stat), n);
                case GuideKind.Gacha: return n <= 1 ? Loc.T("guide.gacha_once") : Loc.F("guide.gacha", n);
                case GuideKind.OpenFormation: return Loc.T("guide.formation");
                case GuideKind.Dungeon: return Loc.F("guide.dungeon", n);
                default: return n <= 1 ? Loc.T("guide.stage_once") : Loc.F("guide.stage", n);
            }
        }
    }
}
