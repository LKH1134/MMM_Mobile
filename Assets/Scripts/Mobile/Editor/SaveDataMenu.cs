using UnityEditor;
using UnityEngine;

namespace MoreMush.Mobile.EditorTools
{
    // 개발용: 저장된 게임 기록(PlayerPrefs 전부 — 지갑·방치 진행·편성·장비·뽑기·던전·퀘스트·세계수·옵션)을 지운다.
    // 플레이 중이면 플레이를 먼저 끝내고 지운다 — 끝날 때 SaveScheduler가 메모리 값을 다시 저장하기 때문
    static class SaveDataMenu
    {
        const string Pending = "MoreMush.ClearSavesPending";

        [MenuItem("MoreMush/게임 기록 전체 초기화")]
        static void ClearAll()
        {
            if (!EditorUtility.DisplayDialog("게임 기록 전체 초기화",
                    "저장된 게임 기록(PlayerPrefs)을 모두 지웁니다. 되돌릴 수 없습니다." +
                    (EditorApplication.isPlaying ? "\n\n플레이 모드를 끝낸 뒤 지웁니다." : ""),
                    "초기화", "취소"))
                return;
            if (EditorApplication.isPlaying)
            {
                SessionState.SetBool(Pending, true);
                EditorApplication.ExitPlaymode();
                return;
            }
            Clear();
        }

        [InitializeOnLoadMethod]
        static void HookPlayMode()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Pending, false)) return;
                SessionState.EraseBool(Pending);
                Clear();
            };
        }

        static void Clear()
        {
            DataResetButton.ClearSaves();
            Debug.Log("[SaveDataMenu] 게임 기록을 모두 지웠다");
        }
    }
}
