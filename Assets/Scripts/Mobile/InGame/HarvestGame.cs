using System.Collections;
using UnityEngine;

namespace MoreMush.Mobile
{
    // InGame 씬 진행. 판 시작 때 지금 스테이지(StageProgress, Stage.xlsx)와 배운 스킬로 수치(RoundStats)를 정해 필드·HUD에 넣는다.
    // 필드에서 버섯을 캐면 점수·수확량을 올리고, 제한 시간이 다 되면 필드와 입력을 멈추고 점수를 골드로 바꿔 결과 팝업을 연다.
    // 게임 오버 조건은 시간뿐이다. 콤보 = 마지막으로 바에 닿은 뒤 캔 버섯 수 (바에 닿으면 0부터).
    // 점수 = 버섯 기본 점수 × 등급 × 황금 × (1 + 콤보 × 콤보 계수) × 획득 점수 × (3배 확률) × (마지막 몇 초 보너스) × 스테이지 보상 배율
    // 수확량 = 버섯 기본 수확량 × 등급별 수확량 × 황금 × (마지막 몇 초 보너스) × 스테이지 보상 배율. 골드 = 점수 × 전환율.
    // 보스 스테이지: 군락지 대신 보스(Boss.xlsx)가 나오고, 보스에 준 피해만큼 점수를 받는다. 분신까지 모두 쓰러뜨리면
    // 처치 점수와 레전더리 버섯(은둔 고수 배율)을 받고 판이 바로 끝난다. 일반 스테이지는 판을 마치면, 보스 스테이지는 처치하면 다음 스테이지로
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
        public long Score => (long)score;
        public long Yield => (long)yield;
        public long Gold => (long)(score * Stats.GoldMul * (Stats.BossRound && Finished && !BossKilled && Stats.Boss.failGold > 0f ? Stats.Boss.failGold : 1f));
        public int HarvestCount { get; private set; }
        public bool Finished { get; private set; }
        public bool BossKilled { get; private set; }
        public int StageNumber { get; private set; }
        public bool FinalRush => Stats.FinalRushBonus > 0f && hud.TimerStarted && hud.Remaining <= Stats.FinalRushSeconds;
        public event System.Action<long> GameOver;   // 이번 판에 번 골드

        void Awake()
        {
            Stats = RoundStats.FromSkills(roundBase);
            StageNumber = StageProgress.Current;
            Stats.Stage = StageProgress.Get(StageNumber);
            Stats.Boss = Stats.Stage != null && Stats.Stage.boss != 0 ? GameData.Bosses.Get(Stats.Stage.boss) : null;
            if (Stats.Stage == null) Debug.LogWarning($"[HarvestGame] Stage 표에 {StageNumber}스테이지가 없다 — 계수 1로 진행");
            field.Configure(Stats);

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
        }

        void Start() => hud.SetDuration(Stats.Duration);

        // 보스는 맞힐 때마다 피해만큼 점수. 체력이 스테이지 계수만큼 커지므로 계수로 나눠 보스 판 점수가 튀지 않게 한다
        void OnHit(Mushroom m, Harvester h, int damage, bool crit)
        {
            if (Finished || !m.IsBoss) return;
            score += damage / Stats.HpRate * Stats.Base.bossHitScore * Stats.ScoreMul * Stats.RewardRate;
            hud.SetScore(Score);
        }

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

        void Finish()
        {
            if (Finished) return;
            Finished = true;
            StageProgress.Complete(!Stats.BossRound || BossKilled);
            field.Stop();
            input.Interactable = false;
            GameOver?.Invoke(Gold);
            StartCoroutine(ShowResult());
        }

        IEnumerator ShowResult()
        {
            yield return new WaitForSecondsRealtime(resultDelay);
            string note = Stats.BossRound ? (BossKilled ? "보스 처치" : "보스 실패") : null;
            resultPopup.Get(hud.transform).GetComponent<ResultPopup>().Show(Gold, Score, Yield, note);
        }
    }
}
