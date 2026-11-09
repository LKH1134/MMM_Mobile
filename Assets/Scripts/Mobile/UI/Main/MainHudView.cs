using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 전투 화면 위 가운데 HUD (프리팹 MainHud): 스테이지 제목 · 웨이브 글자 · 웨이브 점(보스전에는 보스 체력 막대) · 꼬마 체력 · 전투력.
    // 모양은 프리팹에서 고치고, 이 스크립트는 IdleBattle 상태를 읽어 값만 넣는다.
    // IdleBattle 오브젝트 아래에 두면 홈이 아닐 때 전투 화면과 같이 숨는다
    public class MainHudView : MonoBehaviour
    {
        [SerializeField] IdleBattle battle;

        [Header("제목")]
        [SerializeField] TMP_Text stageText;
        [SerializeField] TMP_Text waveText;

        [Header("웨이브 점")]
        [SerializeField] GameObject waveGroup;      // 점 줄 전체 (보스전에는 끈다)
        [SerializeField] RectTransform dotRoot;     // 점이 놓이는 줄 (폭 기준)
        [SerializeField] Image dotTemplate;         // 점 원본 (꺼 둔다)
        [SerializeField] RectTransform bossMark;    // 줄 오른쪽 끝 보스 표시
        [SerializeField] Sprite dotOn, dotOff;

        [Header("보스 체력 (보스전에만)")]
        [SerializeField] GameObject bossGroup;
        [SerializeField] RectTransform bossFill;    // 채움 비율 = anchorMax.x

        [Header("꼬마 체력")]
        [SerializeField] RectTransform hpFill;
        [SerializeField] TMP_Text hpText;

        [Header("전투력")]
        [SerializeField] Button powerButton;
        [SerializeField] TMP_Text powerText;
        [SerializeField] GameObject statsPanel;     // 전투력을 누르면 열고 닫는 세부 통계
        [SerializeField] TMP_Text statsText;

        [Header("왼쪽 아이콘 열 (퀘스트·도감 — 출석 버튼은 AttendanceEntry)")]
        [SerializeField] Button questButton;
        [SerializeField] GameObject questDot;       // 받을 보상이 있으면 켠다
        [SerializeField] Button dexButton;
        [SerializeField] GameObject dexScreen;      // 도감 화면 (없으면 버튼만 보인다)
        [SerializeField] GameObject questScreen;    // 일일 의뢰 화면 (없으면 예전 DailyQuestPopup)

        readonly List<Image> dots = new List<Image>();
        int shownStage = -1, shownWave = -1, shownWaves = -1, shownBossSec = -1;
        bool shownBoss;
        double shownHp = -1, shownMaxHp = -1, shownPower = double.NaN;
        float powerIn;
        float hpTextIn;   // 체력 글자는 0.2초마다 (막대는 매 프레임)
        const float HpTextEvery = 0.2f;

        void Start()
        {
            if (dotTemplate != null) dotTemplate.gameObject.SetActive(false);
            if (statsPanel != null) statsPanel.SetActive(false);
            if (powerButton != null) powerButton.onClick.AddListener(ToggleStats);
            if (statsPanel != null && statsPanel.TryGetComponent<Button>(out var close)) close.onClick.AddListener(() => statsPanel.SetActive(false));
            if (TabGroup.MainBar != null) TabGroup.MainBar.Changed += OnTabChanged;
            RefreshPower(true);

            // 퀘스트·도감 버튼 (예전 코드 버튼 QuestButton·DexButton 대신)
            if (questButton != null) questButton.onClick.AddListener(() => { if (questScreen != null) questScreen.SetActive(true); else DailyQuestPopup.Open(); });
            if (dexButton != null) dexButton.onClick.AddListener(() => { if (dexScreen != null) dexScreen.SetActive(true); });
            DailyQuests.Changed += RefreshQuestDot;
            RefreshQuestDot();
        }

        void OnDestroy()
        {
            if (TabGroup.MainBar != null) TabGroup.MainBar.Changed -= OnTabChanged;
            DailyQuests.Changed -= RefreshQuestDot;
        }

        void RefreshQuestDot()
        {
            if (questDot != null) questDot.SetActive(DailyQuests.ClaimableCount > 0);
        }

        void OnTabChanged(int tab)
        {
            if (statsPanel != null) statsPanel.SetActive(false);   // 통계 창은 홈 전용
            // HUD도 홈 전용: 편성·던전·뽑기 화면에서는 숨긴다 (책 판 탭과 스테이지 제목이 겹치지 않게)
            if (!TryGetComponent<CanvasGroup>(out var group)) group = gameObject.AddComponent<CanvasGroup>();
            bool home = tab < 0;
            group.alpha = home ? 1f : 0f;
            group.blocksRaycasts = home;
            group.interactable = home;
        }

        void ToggleStats()
        {
            if (statsPanel == null) return;
            statsPanel.SetActive(!statsPanel.activeSelf);
            RefreshPower(true);
        }

        void Update()
        {
            if (battle == null) return;

            int stage = battle.HudStage;
            if (stage != shownStage)
            {
                shownStage = stage;
                if (stageText != null) stageText.text = IdleBattle.HudStageLabel(stage);
            }

            bool isBoss = battle.HudIsBoss;
            if (isBoss != shownBoss)
            {
                shownBoss = isBoss;
                if (waveGroup != null) waveGroup.SetActive(!isBoss);
                if (bossGroup != null) bossGroup.SetActive(isBoss);
                shownWave = shownBossSec = -1;
            }

            if (isBoss)
            {
                if (bossFill != null) bossFill.anchorMax = new Vector2(battle.HudBossHp, 1f);
                int sec = Mathf.CeilToInt(battle.HudBossTimeLeft);
                if (sec != shownBossSec)
                {
                    shownBossSec = sec;
                    if (waveText != null) waveText.text = $"보스 {sec}초";
                }
            }
            else
            {
                int waves = battle.HudWaves;
                int w = Mathf.Clamp(battle.HudWave, 1, waves);
                if (w != shownWave || waves != shownWaves)
                {
                    shownWave = w;
                    shownWaves = waves;
                    if (waveText != null) waveText.text = $"웨이브 {w}/{waves}";
                    ShowDots(waves, w);
                }
            }

            double hp = battle.HudPartyHp, maxHp = battle.HudPartyMaxHp;
            if (hpFill != null) hpFill.anchorMax = new Vector2(maxHp <= 0 ? 0f : Mathf.Clamp01((float)(hp / maxHp)), 1f);
            hpTextIn -= Time.unscaledDeltaTime;
            if ((hp != shownHp || maxHp != shownMaxHp) && (hpTextIn <= 0f || maxHp != shownMaxHp))
            {
                hpTextIn = HpTextEvery;
                shownHp = hp;
                shownMaxHp = maxHp;
                if (hpText != null) hpText.text = $"체력 {IdleProgress.FormatNumber(hp)} / {IdleProgress.FormatNumber(maxHp)}";
            }

            powerIn -= Time.unscaledDeltaTime;
            if (powerIn <= 0f) RefreshPower(false);
        }

        // 웨이브 점 waves개를 줄 위에 고르게 두고, current번째(1부터)까지 노란 점. 보스 표시는 줄 오른쪽 끝
        void ShowDots(int waves, int current)
        {
            if (dotRoot == null || dotTemplate == null) return;
            while (dots.Count < waves)
            {
                var d = Instantiate(dotTemplate, dotRoot);
                d.name = "Dot";
                dots.Add(d);
            }
            for (int i = 0; i < dots.Count; i++)
            {
                bool show = i < waves;
                dots[i].gameObject.SetActive(show);
                if (!show) continue;
                var rt = dots[i].rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2((float)i / waves, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                dots[i].sprite = i < current ? dotOn : dotOff;
            }
            if (bossMark != null) bossMark.SetAsLastSibling();
        }

        void RefreshPower(bool force)
        {
            powerIn = 0.5f;
            double total = CombatPower.Total;
            if (force || total != shownPower)
            {
                shownPower = total;
                if (powerText != null) powerText.text = "전투력 " + CombatPower.Format(total);
            }
            if (statsPanel != null && statsPanel.activeSelf && statsText != null) statsText.text = CombatPower.Describe();
        }
    }
}
