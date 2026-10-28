using UnityEditor;
using UnityEngine;

namespace MoreMush.Mobile.EditorTools
{
    // 개발용 InGame 메뉴. 지금 스테이지를 바꾼다 (표 뒤 만든 스테이지 포함), 성능 벤치마크를 돌린다
    static class InGameDebugMenu
    {
        const string BenchmarkPending = "MoreMush.BenchmarkPending";

        // 플레이 중이 아니면 플레이 모드에 들어가 바로 시작한다 (결과는 콘솔·화면, 파일은 persistentDataPath/Benchmark)
        [MenuItem("MoreMush/Debug/Run Benchmark")]
        static void RunBenchmark()
        {
            if (EditorApplication.isPlaying)
            {
                Benchmark.Start();
                return;
            }
            SessionState.SetBool(BenchmarkPending, true);
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("MoreMush/Debug/Open Benchmark Folder")]
        static void OpenBenchmarkFolder()
        {
            string dir = System.IO.Path.Combine(Application.persistentDataPath, "Benchmark");
            System.IO.Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        [InitializeOnLoadMethod]
        static void HookPlayMode()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(BenchmarkPending, false)) return;
                SessionState.EraseBool(BenchmarkPending);
                Benchmark.Start();
            };
        }

        [MenuItem("MoreMush/Debug/Go To Next Boss Stage")]
        static void NextBossStage()
        {
            GameData.Reload();
            int now = StageProgress.Current;
            int target = 0;
            for (int s = now; s < now + 1000 && target == 0; s++)
                if (StageProgress.Get(s) is StageData d && d.boss != 0) target = s;
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
