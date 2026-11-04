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

        // 코드로 만든 예전 HUD(배너·진행 막대·체력·전투력·통계 창)를 숨긴다. 퀘스트·도감 버튼은 그대로 둔다
        public void HideLegacyHud()
        {
            if (stageText != null && stageText.transform.parent != null && stageText.transform.parent.parent != null)
                stageText.transform.parent.parent.gameObject.SetActive(false);   // IdleHud (배너·진행 막대·보스 시간·체력)
            if (powerText != null) powerText.transform.parent.gameObject.SetActive(false);
            if (statsPanel != null) statsPanel.SetActive(false);
        }
    }
}
