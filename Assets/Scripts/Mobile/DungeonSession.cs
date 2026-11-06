using System;

namespace MoreMush.Mobile
{
    // 던전 진행 방식 (던전 시트 mode)
    public enum DungeonMode
    {
        Harvest,    // 벽돌깨기 판 (InGame 씬). 던전 1·2·3 — 같은 씬을 쓰고 보상·레벨만 던전마다 다르다
        BossRush,   // 방치 화면에서 방치 스테이지 보스 bossRushCount마리 연속전 (던전 4)
        BossLevel,  // 방치 화면에서 보스 한 마리를 계속 잡는다 — 잡을 때마다 레벨이 오르고 체력이 다시 찬다 (던전 5)
    }

    // 지금 들어가 있는(또는 마지막으로 들어간) 던전 번호.
    //   벽돌깨기 던전(1~3)은 같은 InGame 씬을 쓰므로, 씬을 열기 전에 Current를 정해 두면 HarvestGame·ResultPopup·StageProgress·HarvestTuning이 읽는다.
    //   방치 화면 던전(4·5)은 IdleBattle이 싸운다: Begin으로 시작을 알리고, 끝나면 IdleBattle이 End를 부른다 (ActiveIdle = 진행 중인 던전, 없으면 0)
    public static class DungeonSession
    {
        public static int Current { get; private set; } = 1;
        public static int ActiveIdle { get; private set; }
        static int beforeIdle = 1;      // 방치 화면 던전 전의 Current (끝나면 되돌린다 — 4·5 번호가 벽돌깨기 진행 키에 남지 않게)

        // 방치 전투가 지금 던전을 시작할 수 있나 (IdleBattle이 등록). 입장 횟수·광고 입장을 쓰기 전에 본다 — 쓰고 나서 시작을 못 하면 횟수만 날아간다
        public static Func<bool> IdleReady;
        static bool CanStartIdle => ActiveIdle == 0 && IdleStarted != null && (IdleReady == null || IdleReady());

        public static event Action<int> IdleStarted;      // 방치 화면 던전 시작 (던전 번호)
        public static event Action IdleEnded;

        public static DungeonMode Mode(int dungeon)
        {
            var row = GameBalance.Dungeon(dungeon);
            return row != null && Enum.TryParse<DungeonMode>(row.mode, true, out var m) ? m : DungeonMode.Harvest;
        }

        public static bool IsIdle(int dungeon) => Mode(dungeon) != DungeonMode.Harvest;

        // 벽돌깨기 던전 씬을 연다. 씬을 열 수 있을 때만 Current를 바꾸고 true
        public static bool LoadHarvest(int dungeon, string scene)
        {
            // 방치 던전이 진행 중이면 막는다 — 씬이 바뀌며 그 던전이 끝날 때 EndIdle이 Current를 되돌려 엉뚱한 던전 번호로 판이 열린다
            if (string.IsNullOrEmpty(scene) || SceneLoader.IsLoading || ActiveIdle != 0) return false;
            int before = Current;
            Current = dungeon;
            if (SceneLoader.Load(scene)) return true;
            Current = before;
            return false;
        }

        // 방치 화면 던전 시작 (입장 횟수는 부르는 쪽이 이미 썼다). 다른 던전이 진행 중이면 false
        public static bool BeginIdle(int dungeon)
        {
            if (!CanStartIdle) return false;     // 방치 전투(IdleBattle)가 없거나 멈춘 씬이면 시작하지 않는다
            beforeIdle = Current;
            Current = dungeon;
            ActiveIdle = dungeon;
            TabGroup.MainBar?.Select(MainNavigation.Home);     // 던전 탭을 닫고 홈 전투 화면을 보여 준다
            IdleStarted?.Invoke(dungeon);
            return ActiveIdle == dungeon;       // 전투 쪽에서 시작에 실패하면 바로 EndIdle을 불러 0이 된다
        }

        public static void EndIdle()
        {
            if (ActiveIdle == 0) return;
            if (Current == ActiveIdle) Current = beforeIdle;     // 그 사이 다른 던전으로 바뀌었으면(벽돌깨기 입장) 건드리지 않는다
            ActiveIdle = 0;
            IdleEnded?.Invoke();
        }

        // 방치 화면 던전에 입장한다: 입장 횟수가 있으면 쓰고, 없으면 광고를 끝까지 본 뒤 광고 입장을 쓴다.
        // 광고가 준비되지 않았거나 시작할 수 없으면 false
        public static bool TryEnterIdle(int dungeon)
        {
            if (!CanStartIdle) return false;
            if (DungeonTickets.CanEnter(dungeon))
            {
                if (!DungeonTickets.TryUse(dungeon)) return false;
                return BeginIdle(dungeon);
            }
            if (!DungeonTickets.CanEnterWithAd(dungeon)) return false;
            string credit = "Dungeon." + dungeon;
            if (AdCredits.TryUse(credit))   // 지난번에 광고를 다 보고도 못 들어간 입장
            {
                if (!DungeonTickets.TryUseAd(dungeon)) return false;
                return BeginIdle(dungeon);
            }
            return AdService.ShowRewarded(AdPlacement.Dungeon, () =>
            {
                // 광고를 보는 사이 씬이 바뀌어 방치 전투가 없으면 → 다음 한 번 광고 없이 들어가게 남긴다
                if (!CanStartIdle) { AdCredits.Add(credit); return; }
                // 광고를 보는 사이 00시가 지나 오늘 입장 횟수가 다시 찼으면 그 횟수로 들어간다
                if (!DungeonTickets.TryUseAd(dungeon) && !DungeonTickets.TryUse(dungeon)) { AdCredits.Add(credit); return; }
                BeginIdle(dungeon);
            });
        }
    }
}
