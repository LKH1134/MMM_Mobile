using UnityEditor;
using UnityEngine;

namespace MoreMush.Mobile.EditorTools
{
    // 개발용 InGame 메뉴. 지금 스테이지를 바꾼다 (Stage.xlsx 기준)
    static class InGameDebugMenu
    {
        [MenuItem("MoreMush/Debug/Go To Next Boss Stage")]
        static void NextBossStage()
        {
            GameData.Reload();
            int now = StageProgress.Current;
            int target = 0;
            foreach (var s in GameData.Stages.All)
                if (s.boss != 0 && s.stage >= now && (target == 0 || s.stage < target)) target = s.stage;
            if (target == 0)
            {
                Debug.LogWarning("[InGameDebug] 지금 이후로 보스 스테이지가 없다");
                return;
            }
            StageProgress.Set(target);
            Debug.Log($"[InGameDebug] 다음 판 = {target}스테이지 (보스)");
        }

        [MenuItem("MoreMush/Debug/Reset Stage")]
        static void ResetStage()
        {
            StageProgress.Set(1);
            Debug.Log("[InGameDebug] 1스테이지로 되돌렸다");
        }
    }
}
