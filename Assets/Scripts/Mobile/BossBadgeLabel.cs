using TMPro;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 하단 전투 버튼 위 배지 글자. 이번 판이 보스 판이면 "보스 등장", 아니면 "전투중..." (표시만 하고 누를 수 없다).
    // 화면이 열릴 때 StageProgress.BossActive를 읽는다
    [RequireComponent(typeof(TMP_Text))]
    public class BossBadgeLabel : MonoBehaviour
    {
        [SerializeField] string bossText = "보스 등장";
        [SerializeField] string normalText = "전투중...";

        void OnEnable() => GetComponent<TMP_Text>().text = StageProgress.BossActive ? bossText : normalText;
    }
}
