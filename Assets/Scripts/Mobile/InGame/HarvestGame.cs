using System.Collections;
using UnityEngine;

namespace MoreMush.Mobile
{
    // InGame 씬 진행. 판 시작 때 지금 스테이지(StageProgress, Stage.xlsx)와 배운 스킬로 수치(RoundStats)를 정해 필드·HUD에 넣는다.
    // 필드에서 버섯을 캐면 점수·수확량을 올리고, 제한 시간이 다 되면 필드와 입력을 멈추고 점수를 골드로 바꿔 지갑에 넣고 결과 팝업을 연다.
    // 게임 오버 조건은 시간뿐이다. 콤보 = 판 전체 하나(HarvestField.Combo): 수확마다 +1, 수확기가 바를 놓쳐 바닥 벽에 닿으면 0.
    // 점수 = 버섯 기본 점수 × 등급 × 황금 × (1 + min(콤보, 콤보 상한) × 콤보 계수) × 획득 점수 × (3배 확률) × (마지막 몇 초 보너스) × 스테이지 보상 배율
    // 수확량 = 버섯 기본 수확량 × 등급별 수확량 × 황금 × (마지막 몇 초 보너스) × 스테이지 보상 배율. 골드 = 점수 × 전환율.
    // 보스 판: 보스 체력을 다 깎는 만큼 때리면 직전 일반 판 점수의 bossHitShare만큼 점수를 받는다 (못 잡은 판은 일반 판보다 손해).
    //   분신까지 모두 쓰러뜨리면 처치: 추가 골드 max(이번 판, 직전 일반 판) × bossRewardMul + 균사석 + 레전드리 버섯, 판이 바로 끝난다.
    //   못 잡으면 그 판 골드 × failGold(지금 표는 1 = 감액 없음), 결과 팝업의 "한 번 더하기"로 다시 도전한다. 스테이지 진행·파밍은 StageProgress가 맡는다.
    // 일반 판은 목표가 없다: 시간 끝까지 돌면 다음 스테이지로 넘어간다 (보스 스테이지의 일반 판 = 파밍은 넘는 판이 아니다).
    // 보스·★ 스테이지를 처음 넘으면 균사석(StageProgress.FirstClearGem)
    // Simulating이면 지갑·스테이지 저장과 결과 팝업을 건너뛴다 (밸런스 측정기 BalanceBot이 쓴다)
    public class HarvestGame : MonoBehaviour
    {
        [SerializeField] StageHud hud;
        [SerializeField] HarvestField field;
        [SerializeField] FieldInput input;
        [SerializeField] SkillEffects effects;       // 피버(콤보 계수)·도토리(점수) 배율. 비워도 된다
        [SerializeField] PopupSlot resultPopup;      // 씬 HUD에 꺼 둔 결과 창. 시간이 다 되면 연다
        [SerializeField] InGameTutorial tutorial;    // 첫 입장 안내 (HUD에 꺼 둔 채 배치)
        [SerializeField] float resultDelay = 0.6f;   // 멈춘 화면을 잠깐 보여 준 뒤 결과를 연다
        [SerializeField] RoundBase roundBase = new RoundBase();

        double score;

        public RoundStats Stats { get; private set; }
        public RoundBase Base => roundBase;
        // 무한 스테이지에서 점수가 long을 넘으면 (long) 변환이 음수가 돼 보상이 사라진다 — 9e18에서 멈춘다
        public long Score => IdleProgress.ClampToLong(score);
        // 점수를 바꾼 골드 (보스를 못 잡았으면 failGold배)
        public long Gold => IdleProgress.ClampToLong(score * Stats.GoldMul * (Stats.BossRound && Finished && !BossKilled && Stats.Boss.failGold > 0f ? Stats.Boss.failGold : 1f));
        public long BonusGold { get; private set; }  // 보스 처치 보상 골드
        public long TotalGold => IdleProgress.SaturatingAdd(Gold, BonusGold);
        public long ReferenceScore { get; set; }     // 직전 일반 판 점수 (보스 타격 점수 기준)
        public long ReferenceGold { get; set; }      // 직전 일반 판 골드 (처치 보상 기준)
        public int HarvestCount { get; private set; }
        public bool Finished { get; private set; }
        public bool BossKilled { get; private set; }
        public bool FarmRound => !Stats.BossRound && StageProgress.HasBoss(Stats.Stage);   // 보스 스테이지의 일반 판
        // 스테이지를 넘었는지: 보스 판은 처치, 일반 판은 늘 넘는다 (파밍 판은 넘는 판이 아니다)
        public bool StageCleared => Stats.BossRound ? BossKilled : !FarmRound;
        public int MyceliteEarned { get; private set; }   // 이번 판 첫 클리어 균사석 (측정기는 스스로 센다)
        public int StageNumber { get; private set; }      // = 던전 1 레벨 (DungeonTickets.Level)
        public long DungeonGold { get; private set; }     // 판 골드에 더해 주는 던전 레벨 보상 (DungeonRewards: 깨면 그 레벨, 못 깨면 × dungeonFailMul)
        public long DungeonMycelite { get; private set; } // 던전 레벨 보상 균사석
        public long PaidGold { get; private set; }        // 이번 판에 실제로 지갑에 넣은 판 골드 (균사석 던전은 0)
        // 지금 던전 (벽돌깨기 던전 1~3이 이 씬을 같이 쓴다, DungeonSession)
        static int Dungeon => DungeonSession.Current;
        // 던전 시트에 골드 보상이 없는 던전(균사석 던전)은 판 점수를 골드로 주지 않고, 균사석 보상이 없는 던전(골드 던전)은 첫 클리어 균사석을 주지 않는다
        static bool GivesGold => GameBalance.Dungeon(Dungeon) is DungeonRow r ? r.rewardKills > 0 : true;
        static bool GivesMycelite => GameBalance.Dungeon(Dungeon) is DungeonRow r ? r.rewardMyc > 0 : true;
        public bool Simulating { get; set; }
        public bool FinalRush => Stats.FinalRushBonus > 0f && hud.TimerStarted && hud.Remaining <= Stats.FinalRushSeconds;
        public event System.Action<long> GameOver;   // 이번 판에 번 골드 (보상 포함)

        void Awake()
        {
            HarvestTuning.Apply(roundBase, "RoundBase");    // 수확전투 시트 (인스펙터 값은 표에 행이 없을 때만)
            field.Harvested += OnHarvested;
            field.Hit += OnHit;
            field.Launched += h =>
            {
                if (h != field.Main) return;
                hud.StartTimer();   // 시간은 첫 투척부터 흐른다
            };
            field.ComboChanged += hud.SetCombo;
            hud.TimeUp += Finish;

            Prepare(StageProgress.Current, StageProgress.BossActive, StageProgress.LastNormalScore, StageProgress.LastNormalGold);
        }

        // 판 준비 (Awake에서 한 번, 측정기는 판마다 다시 부른다). bossRound면 그 스테이지의 보스, 아니면 일반·파밍 판
        public void Prepare(int stageNumber, bool bossRound, long referenceScore, long referenceGold)
        {
            score = 0;
            bossDealt = 0f;
            HarvestCount = 0;
            Finished = BossKilled = false;
            BonusGold = 0;
            MyceliteEarned = 0;
            DungeonGold = DungeonMycelite = PaidGold = 0;
            ReferenceScore = referenceScore;
            ReferenceGold = referenceGold;
            StageNumber = stageNumber;
            Stats = RoundStats.FromSkills(roundBase);
            Stats.Stage = StageProgress.Get(StageNumber);
            Stats.Boss = bossRound && Stats.Stage != null && Stats.Stage.boss != 0 ? GameData.Bosses.Get(Stats.Stage.boss) : null;
            if (Stats.Stage == null) Debug.LogWarning($"[HarvestGame] Stage 표에 {StageNumber}스테이지가 없다 — 계수 1로 진행");
            field.Configure(Stats);
            hud.SetStage(StageNumber, Stats.BossRound);
        }

        void Start()
        {
            hud.SetDuration(Stats.Duration);
            if (!Simulating && tutorial != null) tutorial.Begin(field);
        }

        // 보스 타격 점수는 보스 체력만큼까지만 (넘친 피해는 점수가 없다)
        void OnHit(Mushroom m, Harvester h, int damage, bool crit)
        {
            if (Finished || !m.IsBoss) return;
            float bossHp = Mathf.Max(1f, Stats.BossHp);
            float counted = Mathf.Min(damage, Mathf.Max(0f, bossHp - bossDealt));
            bossDealt += damage;
            if (counted <= 0f) return;
            if (ReferenceScore > 0) score += counted / bossHp * Stats.Base.bossHitShare * ReferenceScore;
            else score += counted / Stats.HpRate * Stats.Base.bossHitScore * Stats.ScoreMul * Stats.RewardRate;
            hud.SetScore(Score);
        }

        float bossDealt;

        void OnHarvested(Mushroom m, Harvester h)
        {
            if (Finished) return;
            if (m.IsBoss)
            {
                HarvestCount++;
                if (field.AnyBossAlive()) return;   // 분신이 남았다
                BossKilled = true;
                score += Stats.Boss.reward * Stats.ScoreMul * Stats.RewardRate;
                hud.SetScore(Score);
                Finish();
                return;
            }
            float rush = FinalRush ? 1f + Stats.FinalRushBonus : 1f;
            int combo = h != null ? field.Combo : 0;   // 스킬 피해로 주인 없이 캔 버섯은 콤보 배율 없음 (전과 같음)
            if (Stats.ComboCap > 0) combo = Mathf.Min(combo, Stats.ComboCap);   // 배율은 상한까지만 (HUD 콤보는 그대로)
            float comboFactor = Stats.ComboFactor * (effects != null ? effects.ComboMul : 1f);
            float skillMul = effects != null ? effects.ScoreMul : 1f;

            double gain = m.Reward * (1f + combo * comboFactor) * Stats.ScoreMul * rush * skillMul * Stats.RewardRate;
            if (m.Golden) gain *= Stats.GoldenScore;
            if (Stats.TripleChance > 0f && Random.value < Stats.TripleChance) gain *= 3f;
            score += gain;


            HarvestCount++;
            hud.SetScore(Score);
        }

        // 측정기(죽지 않는 보스로 잰 판): 처치로 친다. 시간이 다 돼 이미 끝났어도 처치 보상을 다시 셈한다
        public void ForceBossKill()
        {
            if (!Stats.BossRound || BossKilled) return;
            BossKilled = true;
            score += Stats.Boss.reward * Stats.ScoreMul * Stats.RewardRate;
            if (Finished) BonusGold = IdleProgress.ClampToLong((double)System.Math.Max(Gold, ReferenceGold) * Stats.Base.bossRewardMul);
        }

        // 측정기가 시간 끝을 직접 부를 때도 쓴다
        public void Finish()
        {
            if (Finished) return;
            Finished = true;
            if (BossKilled) BonusGold = IdleProgress.ClampToLong((double)System.Math.Max(Gold, ReferenceGold) * Stats.Base.bossRewardMul);
            field.Stop();
            input.Interactable = false;
            if (!Simulating)
            {
                DungeonSession.PayEntry(Dungeon);      // 입장 횟수는 보상을 받는 지금 쓴다
                PaidGold = GivesGold ? TotalGold : 0;
                Wallet.Add(Currency.Gold, PaidGold);
                MyceliteEarned = GivesMycelite ? StageProgress.FirstClearGem(Stats.Stage, Stats.Boss, StageCleared, StageProgress.Cleared) : 0;
                if (MyceliteEarned > 0) Wallet.Add(Currency.Mycelite, MyceliteEarned);
                var reward = StageCleared ? DungeonRewards.GrantClear(Dungeon, StageNumber) : DungeonRewards.GrantFail(Dungeon, StageNumber);
                DungeonGold = reward.gold;
                DungeonMycelite = reward.mycelite;
                StageProgress.Complete(Stats.BossRound, StageCleared, Score, Gold);
                GuideQuests.Report(GuideKind.Dungeon);
                try { WorldTree.QuestDungeon(); }
                catch (System.Exception e) { Debug.LogException(e); }   // 세계수 의뢰가 실패해도 결과 창은 뜨게
            }
            try { GameOver?.Invoke(TotalGold); }
            catch (System.Exception e) { Debug.LogException(e); }
            if (!Simulating) StartCoroutine(ShowResult());   // 결과 창이 안 뜨면 입력이 꺼진 채 나갈 길이 없다 — 위 단계 예외와 상관없이 띄운다
        }

        // 판 도중 앱이 뒤로 가면(전화·알림) 옵션 창을 열어 멈춘다 — 돌아왔을 때 배속 그대로 흘러가 있지 않게
        void OnApplicationPause(bool paused)
        {
            if (!paused || Finished || Simulating || hud == null) return;
            hud.OpenOption();
        }

        IEnumerator ShowResult()
        {
            yield return new WaitForSecondsRealtime(resultDelay);
            var popup = resultPopup.Get(hud.transform).GetComponent<ResultPopup>();
            popup.Show(IdleProgress.SaturatingAdd(PaidGold, DungeonGold), DungeonMycelite + MyceliteEarned, StageCleared);
        }
    }
}
