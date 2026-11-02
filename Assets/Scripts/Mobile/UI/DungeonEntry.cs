using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 던전 목록 카드 하나: 지금 레벨과 오늘 남은 입장 횟수 "n/2"를 보여 주고, 입장하기를 누르면 하나 쓰고 씬을 연다.
    // 입장 횟수를 다 쓰면 버튼이 "광고 보고 입장 (n/2)"로 바뀌고, 광고를 끝까지 보면 광고 입장을 하나 쓰고 연다 (하루 DungeonTickets.AdPerDay번).
    // 둘 다 없거나 아직 해금 전이면 입장 버튼을 막는다. scene이 비어 있으면(아직 없는 던전) 횟수만 보여 준다.
    // 해금 조건은 던전 시트(스테이지 n 진입). 카드 안의 LockOverlay를 해금 전에만 켜고 LockText에 조건을 적는다.
    // 레벨 문구는 카드 안 "LevelText"가 있으면 거기, 없으면 남은 횟수 위에 하나 만들어 쓴다 (프리팹을 고치지 않아도 되게)
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
        string enterLabel;          // 프리팹에 적힌 입장 문구 (광고 입장에서 돌아올 때 되돌린다)

        void Awake()
        {
            if (enterButton != null)
            {
                enterButton.onClick.AddListener(Enter);
                buttonLabel = enterButton.GetComponentInChildren<TMP_Text>(true);
                if (buttonLabel != null)
                {
                    enterLabel = buttonLabel.text;
                    FitToWidth(buttonLabel);    // "광고 보고 입장 (2/2)"가 버튼보다 길다
                }
            }
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (lockOverlay == null && t.name == "LockOverlay") lockOverlay = t.gameObject;
                else if (lockText == null && t.name == "LockText") lockText = t.GetComponent<TMP_Text>();
                else if (levelText == null && t.name == "LevelText") levelText = t.GetComponent<TMP_Text>();
            }
            if (levelText == null && countText != null) levelText = CreateLevelText(countText);
            ApplyTable();
        }

        // 던전 시트: 입장 씬·이름. 이름은 카드의 제목 글자(입장·횟수·잠금·레벨 글자가 아닌 것)에 넣는다
        void ApplyTable()
        {
            var row = GameBalance.Dungeon(dungeon);
            if (row == null) return;
            scene = row.implemented ? row.scene : "";
            if (string.IsNullOrEmpty(row.name)) return;
            foreach (var t in GetComponentsInChildren<TMP_Text>(true))
            {
                if (t == buttonLabel || t == countText || t == lockText || t == levelText) continue;
                if (t.transform.IsChildOf(enterButton != null ? enterButton.transform : transform) && enterButton != null) continue;
                t.text = row.name;
                break;
            }
        }

        // 남은 횟수 칸(배경 + 문구)을 본떠 이름 칸 아래(카드 오른쪽 위)에 레벨 칸을 만든다
        static readonly Vector2 LevelPanelPos = new Vector2(170f, 26f);
        static readonly Vector2 LevelPanelSize = new Vector2(300f, 56f);

        TMP_Text CreateLevelText(TMP_Text like)
        {
            var source = like.transform.parent != null && like.transform.parent != transform ? like.transform.parent : like.transform;
            var go = Instantiate(source.gameObject, transform, false);
            go.name = "LevelPanel";
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = LevelPanelPos;
            rt.sizeDelta = LevelPanelSize;
            if (lockOverlay != null) go.transform.SetSiblingIndex(lockOverlay.transform.GetSiblingIndex());   // 잠금 가림막 아래
            var text = go.GetComponentInChildren<TMP_Text>(true);
            text.name = "LevelText";
            text.color = new Color(1f, 0.85f, 0.35f);
            return text;
        }

        void OnEnable()
        {
            DungeonTickets.Changed += Refresh;
            IdleProgress.Changed += Refresh;        // 스테이지를 깨면 해금이 바뀐다
            Refresh();
        }

        void OnDisable()
        {
            DungeonTickets.Changed -= Refresh;
            IdleProgress.Changed -= Refresh;
        }

        void Enter()
        {
            if (string.IsNullOrEmpty(scene) || SceneLoader.IsLoading) return;
            if (DungeonTickets.CanEnter(dungeon))
            {
                // 씬을 못 열면(빌드 설정에 없음) 횟수를 쓰지 않는다
                if (SceneLoader.Load(scene)) DungeonTickets.TryUse(dungeon);
            }
            else if (DungeonTickets.CanEnterWithAd(dungeon) && !EnterWithAd(dungeon, scene) && buttonLabel != null)
                buttonLabel.text = "광고 준비 중…";
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
            return AdService.ShowRewarded(AdPlacement.Dungeon, () =>
            {
                if (!DungeonTickets.CanEnterWithAd(dungeon) || SceneLoader.IsLoading) return;
                if (SceneLoader.Load(scene)) DungeonTickets.TryUseAd(dungeon);
            });
        }

        void Refresh()
        {
            bool unlocked = DungeonTickets.IsUnlocked(dungeon);
            int left = DungeonTickets.Remaining(dungeon);
            bool byAd = DungeonTickets.CanEnterWithAd(dungeon);
            if (lockOverlay != null) lockOverlay.SetActive(!unlocked);
            if (lockText != null && !unlocked) lockText.text = UnlockMessage();
            if (levelText != null) levelText.text = unlocked ? $"레벨 {DungeonTickets.Level(dungeon)}" : "";
            if (countText != null) countText.text = $"{left}/{DungeonTickets.PerDay}";
            if (buttonLabel != null)
                buttonLabel.text = byAd ? $"광고 보고 입장 ({DungeonTickets.AdRemaining(dungeon)}/{DungeonTickets.AdPerDay})" : enterLabel;
            if (enterButton != null) enterButton.interactable = unlocked && (left > 0 || byAd) && !string.IsNullOrEmpty(scene);
        }

        string UnlockMessage()
        {
            int stage = DungeonTickets.UnlockStage(dungeon);
            return stage < 0 ? "해금되지 않음" : $"스테이지 {stage} 진입 시 해금";
        }
    }
}
