using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 던전 목록 카드 하나: 오늘 남은 입장 횟수 "n/2"를 보여 주고, 입장하기를 누르면 하나 쓰고 씬을 연다.
    // 남은 횟수가 0이면 입장 버튼을 막는다. scene이 비어 있으면(아직 없는 던전) 횟수만 보여 준다
    public class DungeonEntry : MonoBehaviour
    {
        [SerializeField] int dungeon = 1;           // 던전 번호 (DungeonTickets 저장 키)
        [SerializeField] string scene = "";
        [SerializeField] Button enterButton;
        [SerializeField] TMP_Text countText;

        void Awake()
        {
            if (enterButton != null) enterButton.onClick.AddListener(Enter);
        }

        void OnEnable()
        {
            DungeonTickets.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => DungeonTickets.Changed -= Refresh;

        void Enter()
        {
            if (string.IsNullOrEmpty(scene) || !DungeonTickets.CanEnter(dungeon)) return;
            // 씬을 못 열면(빌드 설정에 없음) 횟수를 쓰지 않는다
            if (SceneLoader.Load(scene)) DungeonTickets.TryUse(dungeon);
        }

        void Refresh()
        {
            int left = DungeonTickets.Remaining(dungeon);
            if (countText != null) countText.text = $"{left}/{DungeonTickets.PerDay}";
            if (enterButton != null) enterButton.interactable = left > 0 && !string.IsNullOrEmpty(scene);
        }
    }
}
