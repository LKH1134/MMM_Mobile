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
    //   못 잡으면 그 판 골드 × failGold(지금 표는 1 = 감액 없음), 결과 팝업에서 재도전·파밍을 고른다. 스테이지 진행·파밍은 StageProgress가 맡는다.
    // 일반 판 목표(Stats.Goal, 버섯 n개): 채우면 HUD에 "목표 달성!", 판은 시간 끝까지 계속. 못 채우면 같은 스테이지를 다시 (골드는 다 받음).
    // 보스·★ 스테이지를 처음 넘으면 균사석(StageProgress.FirstClearGem)
    // Simulating이면 지갑·스테이지 저장과 결과 팝업을 건너뛴다 (밸런스 측정기 BalanceBot이 쓴다)
    public class HarvestGame : MonoBehaviour
    {
        [SerializeField] StageHud hud;
        [SerializeField] HarvestField field;
        [SerializeField] FieldInput input;
        [SerializeField] SkillEffects effects;       // 피버(콤보 계수)·도토리(점수) 배율. 비워도 된다
        [SerializeField] PopupSlot resultPopup;      // 프리팹. 시간이 다 되면 HUD 캔버스 아래에 만든다
        [SerializeField] float resultDelay = 0.6f;   // 멈춘 화면을 잠깐 보여 준 뒤 결과를 연다
        [SerializeField] RoundBase roundBase = new RoundBase();

        double score;
        double yield;

        public RoundStats Stats { get; private set; }
        public RoundBase Base => roundBase;
        public long Score => (long)score;
        public long Yield => (long)yield;
        // 점수를 바꾼 골드 (보스를 못 잡았으면 failGold배)
        public long Gold => (long)(score * Stats.GoldMul * (Stats.BossRound && Finished && !BossKilled && Stats.Boss.failGold > 0f ? Stats.Boss.failGold : 1f));
        public long BonusGold { get; private set; }  // 보스 처치 보상 골드
        public long TotalGold => Gold + BonusGold;
        public long ReferenceScore { get; set; }     // 직전 일반 판 점수 (보스 타격 점수 기준)
        public long ReferenceGold { get; set; }      // 직전 일반 판 골드 (처치 보상 기준)
        public int HarvestCount { get; private set; }
        public bool Finished { get; private set; }
        public bool BossKilled { get; private set; }
        public bool GoalMet => !Stats.HasGoal || HarvestCount >= Stats.Goal;
        public bool FarmRound => !Stats.BossRound && Stats.Stage != null && Stats.Stage.boss != 0;   // 보스 스테이지의 일반 판
        // 스테이지를 넘었는지: 보스 판은 처치, 일반 판은 목표 달성 (파밍 판은 넘는 판이 아니다)
        public bool StageCleared => Stats.BossRound ? BossKilled : !FarmRound && GoalMet;
        public int MyceliteEarned { get; private set; }   // 이번 판 첫 클리어 균사석 (측정기는 스스로 센다)
        public int StageNumber { get; private set; }
        public bool Simulating { get; set; }
        public bool FinalRush => Stats.FinalRushBonus > 0f && hud.TimerStarted && hud.Remaining <= Stats.FinalRushSeconds;
        public event System.Action<long> GameOver;   // 이번 판에 번 골드 (보상 포함)

        void Awake()
        {
            field.Harvested += OnHarvested;
            field.Hit += OnHit;
            field.Launched += h =>
            {
                if (h != field.Main) return;
                hud.StartTimer();   // 시간은 첫 투척부터 흐른다
            };
            field.ComboChanged += hud.SetCombo;
            hud.TimeUp += Finish;

            Prepare(StageProgress.Current, StageProgress.BossActive, StageProgress.LastNormalScore, StageProgress.LastNormalGold, StageProgress.FailStreak);
        }

        // 판 준비 (Awake에서 한 번, 측정기는 판마다 다시 부른다). bossRound면 그 스테이지의 보스, 아니면 일반·파밍 판.
        // failStreak = 이 스테이지 목표를 연속으로 못 채운 수 (목표 완화)
        public void Prepare(int stageNumber, bool bossRound, long referenceScore, long referenceGold, int failStreak = 0)
        {
            score = yield = 0;
            bossDealt = 0f;
            HarvestCount = 0;
            Finished = BossKilled = goalShown = false;
            BonusGold = 0;
            MyceliteEarned = 0;
            ReferenceScore = referenceScore;
            ReferenceGold = referenceGold;
            StageNumber = stageNumber;
            Stats = RoundStats.FromSkills(roundBase);
            Stats.Stage = StageProgress.Get(StageNumber);
            Stats.Boss = bossRound && Stats.Stage != null && Stats.Stage.boss != 0 ? GameData.Bosses.Get(Stats.Stage.boss) : null;
            Stats.Goal = bossRound ? 0 : RoundStats.GoalFor(Stats.Stage, failStreak, roundBase);
            if (Stats.Stage == null) Debug.LogWarning($"[HarvestGame] Stage 표에 {StageNumber}스테이지가 없다 — 계수 1로 진행");
            field.Configure(Stats);
            hud.SetGoal(0, Stats.Goal);
        }

        bool goalShown;

        void Start() => hud.SetDuration(Stats.Duration);

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
                yield += Stats.Boss.drop * Stats.YieldMul(MushroomTier.Legendary) * Stats.RewardRate;
                hud.SetScore(Score);
                Finish();
                return;
            }
            float rush = FinalRush ? 1f + Stats.FinalRushBonus : 1f;
            int combo = h != null ? field.Combo : 0;   // 스킬 피해로 주인 없이 캔 버섯은 콤보 배율 없음 (전과 같음)
            if (Stats.ComboCap > 0) combo = Mathf.Min(combo, Stats.ComboCap);   // 배율은 상한까지만 (HUD 콤보는 그대로)
            float comboFactor = Stats.ComboFactor * (effects != null ? effects.ComboMul : 1f);
            float skillMul = effects != null ? effects.ScoreMul : 1f;

            double gain = m.Reward * field.ScoreMul(m.Tier) * (1f + combo * comboFactor) * Stats.ScoreMul * rush * skillMul * Stats.RewardRate;
            if (m.Golden) gain *= Stats.GoldenScore;
            if (Stats.TripleChance > 0f && Random.value < Stats.TripleChance) gain *= 3f;
            score += gain;

            double items = m.Drop * Stats.YieldMul(m.Tier) * rush * Stats.RewardRate;
            if (m.Golden) items *= Stats.GoldenYield;
            yield += items;

            HarvestCount++;
            hud.SetScore(Score);
            if (Stats.HasGoal && !goalShown)
            {
                goalShown = GoalMet;
                hud.SetGoal(HarvestCount, Stats.Goal);   // 채운 순간 "목표 달성!", 그 뒤로는 바꾸지 않는다
            }
        }

        // 측정기(죽지 않는 보스로 잰 판): 처치로 친다. 시간이 다 돼 이미 끝났어도 처치 보상을 다시 셈한다
        public void ForceBossKill()
        {
            if (!Stats.BossRound || BossKilled) return;
            BossKilled = true;
            score += Stats.Boss.reward * Stats.ScoreMul * Stats.RewardRate;
            yield += Stats.Boss.drop * Stats.YieldMul(MushroomTier.Legendary) * Stats.RewardRate;
            if (Finished) BonusGold = (long)(System.Math.Max(Gold, ReferenceGold) * Stats.Base.bossRewardMul);
        }

        // 측정기가 시간 끝을 직접 부를 때도 쓴다
        public void Finish()
        {
            if (Finished) return;
            Finished = true;
            if (BossKilled) BonusGold = (long)(System.Math.Max(Gold, ReferenceGold) * Stats.Base.bossRewardMul);
            field.Stop();
            input.Interactable = false;
            if (!Simulating)
            {
                Wallet.Add(Currency.Gold, TotalGold);
                MyceliteEarned = StageProgress.FirstClearGem(Stats.Stage, Stats.Boss, StageCleared, StageProgress.Cleared);
                if (MyceliteEarned > 0) Wallet.Add(Currency.Mycelite, MyceliteEarned);
                StageProgress.Complete(Stats.BossRound, StageCleared, Score, Gold);
            }
            GameOver?.Invoke(TotalGold);
            if (!Simulating) StartCoroutine(ShowResult());
        }

        IEnumerator ShowResult()
        {
            yield return new WaitForSecondsRealtime(resultDelay);
            string note = Stats.BossRound ? (BossKilled ? "보스 처치" : "보스 실패")
                : Stats.HasGoal ? (GoalMet ? $"목표 {Stats.Goal}개 달성" : $"목표 {HarvestCount}/{Stats.Goal} · 다시 도전") : null;
            var popup = resultPopup.Get(hud.transform).GetComponent<ResultPopup>();
            popup.Show(TotalGold, Score, Yield, note, MyceliteEarned, Stats.BossRound && !BossKilled);
        }
    }
}
