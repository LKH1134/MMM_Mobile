using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 터치 판정 정리: 누를 필요 없는 그래픽(버튼 위 글자·아이콘 등)의 raycastTarget을 끈다.
    // 터치마다 EventSystem은 켜진 raycastTarget 그래픽을 전부 검사하므로 모바일에서 수가 적을수록 가볍다.
    // 끄는 조건 (터치 동작이 바뀌지 않는 경우만):
    //   1) 자기에게 터치 처리기(버튼·스크롤·EventTrigger·IPointer… 구현)가 없고, 어떤 Selectable의 targetGraphic도 아니다
    //   2) 레이캐스트를 받는 조상 그래픽이 있고 그 사각형이 자기를 완전히 덮는다 (눌러도 그 조상이 대신 맞는다)
    //   3) 자기와 그 조상 사이에 터치 처리기가 없다 (이벤트가 올라가는 길이 같다)
    //   4) 입력칸(TMP_InputField) 안이 아니고, 조상이 알파 판정(alphaHitTestMinimumThreshold)을 쓰지 않는다
    // 씬에서는 프리팹 인스턴스 안은 건드리지 않는다 (프리팹 원본에서 고친다). 결과는 Temp/RaycastTargetAudit.txt
    public static class RaycastTargetAudit
    {
        static readonly string[] PrefabRoots = { "Assets/Prefabs" };
        static readonly string[] Scenes = { "Assets/Scenes/Start.unity", "Assets/Scenes/Main.unity", "Assets/Scenes/WorldTree.unity", "Assets/Scenes/InGame.unity" };
        const string ReportPath = "Temp/RaycastTargetAudit.txt";

        [MenuItem("MoreMush/Optimize/Raycast Target - 점검만")]
        static void DryRun() => Run(false);

        [MenuItem("MoreMush/Optimize/Raycast Target - 끄기 적용")]
        static void Apply() => Run(true);

        public static string Run(bool apply)
        {
            if (apply && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return "취소";
            var report = new StringBuilder();
            reasons.Clear();
            int total = 0, off = 0;
            string startScene = SceneManager.GetActiveScene().path;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", PrefabRoots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var found = Collect(root, false, ref total);
                    if (found.Count == 0) continue;
                    report.Append("[프리팹] ").Append(path).Append('\n');
                    foreach (var g in found) { report.Append("    ").Append(PathOf(g.transform, root.transform)).Append('\n'); if (apply) g.raycastTarget = false; }
                    off += found.Count;
                    if (apply) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            foreach (var scenePath in Scenes)
            {
                if (!File.Exists(scenePath)) continue;
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var found = new List<Graphic>();
                foreach (var go in scene.GetRootGameObjects()) found.AddRange(Collect(go, true, ref total));
                if (found.Count == 0) continue;
                report.Append("[씬] ").Append(scenePath).Append('\n');
                foreach (var g in found) { report.Append("    ").Append(PathOf(g.transform, null)).Append('\n'); if (apply) { Undo.RecordObject(g, "raycastTarget"); g.raycastTarget = false; } }
                off += found.Count;
                if (apply) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            }

            if (!string.IsNullOrEmpty(startScene)) EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);
            string head = $"raycastTarget 켜진 그래픽 {total}개 중 끌 수 있는 것 {off}개 ({(apply ? "적용함" : "점검만")})\n";
            foreach (var kv in reasons) head += $"  남김 — {kv.Key}: {kv.Value}\n";
            File.WriteAllText(ReportPath, head + report);
            Debug.Log("[RaycastTargetAudit] " + head + "자세한 목록: " + ReportPath);
            return head;
        }

        static List<Graphic> Collect(GameObject root, bool skipPrefabInstances, ref int total)
        {
            var result = new List<Graphic>();
            var targetGraphics = new HashSet<Graphic>();
            foreach (var s in root.GetComponentsInChildren<Selectable>(true)) if (s.targetGraphic != null) targetGraphics.Add(s.targetGraphic);
            foreach (var g in root.GetComponentsInChildren<Graphic>(true))
            {
                if (!g.raycastTarget) continue;
                total++;
                if (skipPrefabInstances && PrefabUtility.IsPartOfPrefabInstance(g)) continue;
                if (!skipPrefabInstances && PrefabUtility.IsPartOfPrefabInstance(g) && PrefabUtility.GetNearestPrefabInstanceRoot(g) != root) continue;   // 중첩 프리팹은 그 원본에서
                string why = WhyKeep(g, targetGraphics);
                if (why == null) result.Add(g);
                else { reasons.TryGetValue(why, out int n); reasons[why] = n + 1; }
            }
            return result;
        }

        static readonly Dictionary<string, int> reasons = new Dictionary<string, int>();

        // 끄면 안 되는 이유 (null이면 꺼도 된다)
        static string WhyKeep(Graphic g, HashSet<Graphic> targetGraphics)
        {
            if (targetGraphics.Contains(g)) return "버튼 판정 그래픽";
            if (HasHandler(g.transform)) return "자기에게 터치 처리기";
            if (g.GetComponentInParent<TMP_InputField>(true) != null || g.GetComponentInParent<InputField>(true) != null) return "입력칸 안";
            if (g is Image img && img.alphaHitTestMinimumThreshold > 0f) return "알파 판정";
            var rect = WorldRect(g.rectTransform);
            for (var t = g.transform.parent; t != null; t = t.parent)
            {
                if (t.TryGetComponent<Graphic>(out var a) && a.raycastTarget && a.enabled)
                {
                    if (a is Image ai && ai.alphaHitTestMinimumThreshold > 0f) return "조상이 알파 판정";
                    return Contains(WorldRect(a.rectTransform), rect) ? null : "덮는 조상 밖으로 나감";
                }
                if (HasHandler(t)) return "사이에 처리기";       // 사이에 처리기가 있으면 이벤트 길이 달라진다
                if (t.GetComponent<Canvas>() != null) return "덮는 조상 없음";   // 다른 캔버스로 넘어가면 그리는 순서가 달라진다
            }
            return "덮는 조상 없음";
        }

        static bool HasHandler(Transform t)
        {
            foreach (var c in t.GetComponents<Component>())
                if (c is IEventSystemHandler && !(c is ButtonPressShade)) return true;
            return false;
        }

        static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return Rect.MinMaxRect(Mathf.Min(c[0].x, c[2].x), Mathf.Min(c[0].y, c[2].y), Mathf.Max(c[0].x, c[2].x), Mathf.Max(c[0].y, c[2].y));
        }

        static bool Contains(Rect outer, Rect inner)
        {
            const float eps = 0.5f;
            return inner.xMin >= outer.xMin - eps && inner.yMin >= outer.yMin - eps && inner.xMax <= outer.xMax + eps && inner.yMax <= outer.yMax + eps;
        }

        static string PathOf(Transform t, Transform root)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null && p != root; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
