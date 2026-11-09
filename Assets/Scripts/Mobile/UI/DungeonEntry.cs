using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 던전 목록 카드 하나: 지금 레벨과 오늘 남은 입장 횟수 "n/2"를 보여 주고, 입장하기를 누르면 하나 쓰고 씬을 연다.
    // 입장 횟수를 다 쓰면 버튼이 "광고 보고 입장 (n/2)"로 바뀌고, 광고를 끝까지 보면 광고 입장을 하나 쓰고 연다 (하루 DungeonTickets.AdPerDay번).
    // 소탕은 없다 — 레벨과 상관없이 매번 직접 플레이한다.
    // 던전 시트 mode: Harvest = 씬(벽돌깨기)을 연다 · BossRush·BossLevel = 씬 없이 방치 화면에서 싸운다 (DungeonSession.TryEnterIdle, 던전 탭을 닫고 홈으로).
    // 둘 다 없거나 아직 해금 전이면 입장 버튼을 막는다. scene이 비어 있으면(아직 없는 던전) 횟수만 보여 준다.
    // 해금 조건은 던전 시트(스테이지 n 진입). 카드 안의 LockOverlay를 해금 전에만 켜고 LockText에 조건을 적는다.
    // 레벨 문구는 카드 안에 배치한 "LevelText"(LevelPanel)에 쓴다 — 모양·자리는 프리팹 DungeonUI에서 고친다
    public class DungeonEntry : MonoBehaviour
    {
        [SerializeField] int dungeon = 1;           // 던전 번호 (DungeonTickets 저장 키)
        [SerializeField] string scene = "";         // 던전 시트 scene이 덮어쓴다 (implemented가 아니면 비운다)
        [SerializeField] Button enterButton;
        [SerializeField] TMP_Text countText;

        GameObject lockOverlay;     // 카드 안에서 이름으로 찾는다 (프리팹을 고치지 않아도 되게)
        TMP_Text lockText;
        TMP_Text levelText;
        TMP_Text buttonLabel;

        void Awake()
        {
            if (enterButton != null)
            {
                enterButton.onClick.AddListener(Enter);
                buttonLabel = enterButton.GetComponentInChildren<TMP_Text>(true);
                if (buttonLabel != null)
                {
                    FitToWidth(buttonLabel);    // "광고 보고 입장 (2/2)"가 버튼보다 길다
                }
            }
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (lockOverlay == null && t.name == "LockOverlay") lockOverlay = t.gameObject;
                else if (lockText == null && t.name == "LockText") lockText = t.GetComponent<TMP_Text>();
                else if (levelText == null && t.name == "LevelText") levelText = t.GetComponent<TMP_Text>();
            }
            ApplyTable();
        }

        // 던전 시트: 입장 씬·이름. 이름은 카드의 제목 글자(입장·횟수·잠금·레벨 글자가 아닌 것)에 넣는다
        void ApplyTable()
        {
            var row = GameBalance.Dungeon(dungeon);
            if (row == null) return;
            scene = row.implemented ? row.scene : "";
            idle = row.implemented && DungeonSession.IsIdle(dungeon);
            if (string.IsNullOrEmpty(row.name)) return;
            foreach (var t in GetComponentsInChildren<TMP_Text>(true))
            {
                if (t == buttonLabel || t == countText || t == lockText || t == levelText) continue;
                if (t.transform.IsChildOf(enterButton != null ? enterButton.transform : transform) && enterButton != null) continue;
                t.text = row.Name;
                break;
            }
        }

        void OnEnable()
        {
            DungeonTickets.Changed += Refresh;
            IdleProgress.Changed += Refresh;        // 스테이지를 깨면 해금이 바뀐다
            Loc.Changed += OnLanguageChanged;
            Refresh();
        }

        void OnDisable()
        {
            DungeonTickets.Changed -= Refresh;
            IdleProgress.Changed -= Refresh;
            Loc.Changed -= OnLanguageChanged;
        }

        // 언어가 바뀌면 던전 이름과 버튼·잠금 글자를 다시 쓴다
        void OnLanguageChanged()
        {
            ApplyTable();
            Refresh();
        }

        bool idle;      // 방치 화면 던전 (씬 없음)

        void Enter()
        {
            if (SceneLoader.IsLoading) return;
            if (idle)
            {
                if (!DungeonSession.TryEnterIdle(dungeon) && buttonLabel != null)
                    buttonLabel.text = Loc.T(DungeonSession.ActiveIdle != 0 ? "dungeon.other_active" : "ad.preparing");
                return;
            }
            if (string.IsNullOrEmpty(scene)) return;
            // 방치 던전 중에는 벽돌깨기 판을 열지 않는다 (LoadHarvest도 막는다) — 광고부터 보고 못 들어가는 일이 없게 먼저 알린다
            if (DungeonSession.ActiveIdle != 0)
            {
                if (buttonLabel != null) buttonLabel.text = Loc.T("dungeon.other_active");
                return;
            }
            if (DungeonTickets.CanEnter(dungeon))
            {
                // 횟수는 판이 끝나 보상을 받을 때 쓴다 (HarvestGame.Finish)
                if (DungeonSession.LoadHarvest(dungeon, scene)) DungeonSession.MarkEntry(dungeon, false);
            }
            else if (DungeonTickets.CanEnterWithAd(dungeon) && !EnterWithAd(dungeon, scene) && buttonLabel != null)
                buttonLabel.text = Loc.T("ad.preparing");
        }

        // 긴 문구는 줄이고, 짧은 문구는 원래 크기 그대로
        public static void FitToWidth(TMP_Text label)
        {
            if (label.enableAutoSizing) return;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = Mathf.Max(18f, label.fontSize * 0.5f);
            label.enableWordWrapping = false;
            label.enableAutoSizing = true;
        }

        // 광고를 끝까지 보면 광고 입장을 하나 쓰고 씬을 연다. 광고가 준비되지 않았으면 false (AdService가 다음 광고를 불러 둔다).
        // 광고 하나는 한 번만 보여 주므로 두 번 눌러도 두 번 들어가지 않는다. 결과 팝업도 같은 길로 들어간다
        public static bool EnterWithAd(int dungeon, string scene)
        {
            string credit = "Dungeon." + dungeon;
            if (AdCredits.Has(credit) && !SceneLoader.IsLoading)   // 지난번에 광고를 다 보고도 못 들어간 입장
            {
                if (DungeonSession.LoadHarvest(dungeon, scene)) { AdCredits.TryUse(credit); DungeonSession.MarkEntry(dungeon, true); }
                return true;
            }
            return AdService.ShowRewarded(AdPlacement.Dungeon, () =>
            {
                if (SceneLoader.IsLoading || !DungeonSession.LoadHarvest(dungeon, scene)) { AdCredits.Add(credit); return; }   // 지금 못 들어간다 → 다음 한 번 광고 없이
                DungeonSession.MarkEntry(dungeon, true);     // 광고 입장 횟수는 판이 끝나 보상을 받을 때 쓴다
            });
        }

        void Refresh()
        {
            bool unlocked = DungeonTickets.IsUnlocked(dungeon);
            int left = DungeonTickets.Remaining(dungeon);
            bool byAd = DungeonTickets.CanEnterWithAd(dungeon);
            if (lockOverlay != null) lockOverlay.SetActive(!unlocked);
            if (lockText != null && !unlocked) lockText.text = UnlockMessage();
            if (levelText != null) levelText.text = !unlocked ? "" : LevelLabel();
            if (countText != null) countText.text = $"{left}/{DungeonTickets.PerDay}";
            if (buttonLabel != null)
                buttonLabel.text = byAd ? Loc.F("dungeon.ad_enter", DungeonTickets.AdRemaining(dungeon), DungeonTickets.AdPerDay) : Loc.T("dungeon.enter");
            if (enterButton != null) enterButton.interactable = unlocked && (left > 0 || byAd) && (idle || !string.IsNullOrEmpty(scene));
        }

        // 레벨업 보스는 매번 레벨 1부터라 최고 기록을 보여 준다
        string LevelLabel()
        {
            if (DungeonSession.Mode(dungeon) != DungeonMode.BossLevel) return Loc.F("dungeon.level", DungeonTickets.Level(dungeon));
            int best = DungeonTickets.BestLevel(dungeon);
            return best > 0 ? Loc.F("dungeon.best_level", best) : Loc.T("dungeon.no_record");
        }

        string UnlockMessage()
        {
            int stage = DungeonTickets.UnlockStage(dungeon);
            return stage < 0 ? Loc.T("dungeon.locked") : Loc.F("dungeon.unlock_at_stage", stage);
        }
    }
}
