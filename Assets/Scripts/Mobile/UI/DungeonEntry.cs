using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 던전 목록 카드 하나: 오늘 남은 입장 횟수 "n/2"를 보여 주고, 입장하기를 누르면 하나 쓰고 씬을 연다.
    // 남은 횟수가 0이거나 아직 해금 전이면 입장 버튼을 막는다. scene이 비어 있으면(아직 없는 던전) 횟수만 보여 준다.
    // 해금 조건은 던전 시트(스테이지 n 진입). 카드 안의 LockOverlay를 해금 전에만 켜고 LockText에 조건을 적는다
    public class DungeonEntry : MonoBehaviour
    {
        [SerializeField] int dungeon = 1;           // 던전 번호 (DungeonTickets 저장 키)
        [SerializeField] string scene = "";
        [SerializeField] Button enterButton;
        [SerializeField] TMP_Text countText;

        GameObject lockOverlay;     // 카드 안에서 이름으로 찾는다 (프리팹을 고치지 않아도 되게)
        TMP_Text lockText;

        void Awake()
        {
            if (enterButton != null) enterButton.onClick.AddListener(Enter);
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (lockOverlay == null && t.name == "LockOverlay") lockOverlay = t.gameObject;
                else if (lockText == null && t.name == "LockText") lockText = t.GetComponent<TMP_Text>();
            }
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
            if (string.IsNullOrEmpty(scene) || !DungeonTickets.CanEnter(dungeon)) return;
            // 씬을 못 열면(빌드 설정에 없음) 횟수를 쓰지 않는다
            if (SceneLoader.Load(scene)) DungeonTickets.TryUse(dungeon);
        }

        void Refresh()
        {
            bool unlocked = DungeonTickets.IsUnlocked(dungeon);
            int left = DungeonTickets.Remaining(dungeon);
            if (lockOverlay != null) lockOverlay.SetActive(!unlocked);
            if (lockText != null && !unlocked) lockText.text = UnlockMessage();
            if (countText != null) countText.text = $"{left}/{DungeonTickets.PerDay}";
            if (enterButton != null) enterButton.interactable = unlocked && left > 0 && !string.IsNullOrEmpty(scene);
        }

        string UnlockMessage()
        {
            int stage = DungeonTickets.UnlockStage(dungeon);
            return stage < 0 ? "해금되지 않음" : $"스테이지 {stage} 진입 시 해금";
        }
    }
}
