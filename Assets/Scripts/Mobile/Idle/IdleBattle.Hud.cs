using UnityEngine;

namespace MoreMush.Mobile
{
    // 프리팹 HUD(MainHudView)가 읽는 전투 상태. IdleBattle 본문은 건드리지 않고 partial로 연다
    public partial class IdleBattle
    {
        public int HudStage => stage;
        public int HudWave => wave;                                  // 1..WavesPerStage = 일반, 그 다음 = 보스
        public int HudWaves => IdleProgress.WavesPerStage(stage);
        public bool HudIsBoss => boss != null && !boss.dead;
        public float HudBossHp => boss != null && boss.maxHp > 0 ? Mathf.Clamp01((float)(boss.hp / boss.maxHp)) : 0f;
        public float HudBossTimeLeft => bossTimeLeft;
        public double HudPartyHp => System.Math.Max(0, partyHp);
        public double HudPartyMaxHp => partyMaxHp;

        public static string HudStageLabel(int stage)
        {
            int s = Mathf.Max(1, stage) - 1;
            return $"스테이지 {s / StagesPerChapter + 1}-{s % StagesPerChapter + 1}";
        }
    }
}
