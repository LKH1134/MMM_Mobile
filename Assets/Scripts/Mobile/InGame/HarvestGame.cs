using System.Collections;
using UnityEngine;

namespace MoreMush.Mobile
{
    // InGame 씬 진행. 판 시작 때 지금 스테이지(StageProgress, Stage.xlsx)와 배운 스킬로 수치(RoundStats)를 정해 필드·HUD에 넣는다.
    // 필드에서 버섯을 캐면 점수·수확량을 올리고, 제한 시간이 다 되면 필드와 입력을 멈추고 점수를 골드로 바꿔 지갑에 넣고 결과 팝업을 연다.
    // 게임 오버 조건은 시간뿐이다. 콤보 = 마지막으로 바에 닿은 뒤 캔 버섯 수 (바에 닿으면 0부터).
    // 점수 = 버섯 기본 점수 × 등급 × 황금 × (1 + 콤보 × 콤보 계수) × 획득 점수 × (3배 확률) × (마지막 몇 초 보너스) × 스테이지 보상 배율
    // 수확량 = 버섯 기본 수확량 × 등급별 수확량 × 황금 × (마지막 몇 초 보너스) × 스테이지 보상 배율. 골드 = 점수 × 전환율.
    // 보스 판: 보스 체력을 다 깎는 만큼 때리면 직전 일반 판 점수의 bossHitShare만큼 점수를 받는다 (못 잡은 판은 일반 판보다 손해).
    //   분신까지 모두 쓰러뜨리면 처치: 추가 골드 max(이번 판, 직전 일반 판) × bossRewardMul + 균사석 + 레전드리 버섯, 판이 바로 끝난다.
    //   못 잡으면 그 판 골드 × failGold. 스테이지 진행·파밍은 StageProgress가 맡는다.
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
                hud.SetCombo(0);
                hud.StartTimer();   // 시간은 첫 투척부터 흐른다
            };
            field.BarHit += h => { if (h == field.Main) hud.SetCombo(0); };
            hud.TimeUp += Finish;

            Prepare(StageProgress.Current, StageProgress.BossActive, StageProgress.LastNormalScore, StageProgress.LastNormalGold);
        }

        // 판 준비 (Awake에서 한 번, 측정기는 판마다 다시 부른다). bossRound면 그 스테이지의 보스, 아니면 일반·파밍 판
        public void Prepare(int stageNumber, bool bossRound, long referenceScore, long referenceGold)
        {
            score = yield = 0;
            bossDealt = 0f;
            HarvestCount = 0;
            Finished = BossKilled = false;
            BonusGold = 0;
            ReferenceScore = referenceScore;
            ReferenceGold = referenceGold;
            StageNumber = stageNumber;
            Stats = RoundStats.FromSkills(roundBase);
            Stats.Stage = StageProgress.Get(StageNumber);
            Stats.Boss = bossRound && Stats.Stage != null && Stats.Stage.boss != 0 ? GameData.Bosses.Get(Stats.Stage.boss) : null;
            if (Stats.Stage == null) Debug.LogWarning($"[HarvestGame] Stage 표에 {StageNumber}스테이지가 없다 — 계수 1로 진행");
            field.Configure(Stats);
        }

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
            int combo = h != null ? h.Combo : 0;
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
            if (h != null && h == field.Main) hud.SetCombo(h.Combo);
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
                if (BossKilled && Stats.Boss.gem > 0) Wallet.Add(Currency.Mycelite, Stats.Boss.gem);
                StageProgress.Complete(Stats.BossRound, BossKilled, Score, Gold);
            }
            GameOver?.Invoke(TotalGold);
            if (!Simulating) StartCoroutine(ShowResult());
        }

        IEnumerator ShowResult()
        {
            yield return new WaitForSecondsRealtime(resultDelay);
            string note = Stats.BossRound ? (BossKilled ? $"보스 처치 · 균사석 {Stats.Boss.gem}" : "보스 실패") : null;
            resultPopup.Get(hud.transform).GetComponent<ResultPopup>().Show(TotalGold, Score, Yield, note);
        }
    }
}
