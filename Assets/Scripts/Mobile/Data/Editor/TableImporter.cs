using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MoreMush.Mobile.EditorTools
{
    // Assets/Data/Tables/*.xlsx가 바뀌면 Resources/Tables/<이름>.bytes로 복사하고 내용을 검사해 콘솔에 알린다.
    // 빌드에는 .bytes 복사본이 들어가고, 런타임의 GameData가 그걸 파싱한다 (Resources는 .xlsx를 그대로 못 읽는다).
    // 엑셀이 열려 있을 때 생기는 잠금 파일(~$*.xlsx)은 무시한다
    class TableImporter : AssetPostprocessor
    {
        const string SourceFolder = "Assets/Data/Tables";
        const string OutputFolder = "Assets/Resources/" + GameData.ResourceFolder;

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported.Concat(moved).Where(IsTable)) Export(path);
            foreach (var path in deleted.Concat(movedFrom).Where(IsTable)) RemoveCopy(path);
        }

        [MenuItem("MoreMush/Data/Reimport Tables")]
        static void ReimportAll()
        {
            if (!Directory.Exists(SourceFolder)) return;
            foreach (var path in Directory.GetFiles(SourceFolder, "*.xlsx").Select(p => p.Replace('\\', '/')).Where(IsTable)) Export(path);
            GameData.Reload();
        }

        static bool IsTable(string path) =>
            path.StartsWith(SourceFolder + "/") && path.EndsWith(".xlsx", System.StringComparison.OrdinalIgnoreCase)
            && !Path.GetFileName(path).StartsWith("~$");

        static void Export(string path)
        {
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (IOException e)
            {
                Debug.LogWarning($"[TableImporter] {path}을(를) 읽지 못했다 (엑셀에서 저장 중일 수 있다): {e.Message}");
                return;
            }

            string name = Path.GetFileNameWithoutExtension(path);
            Directory.CreateDirectory(OutputFolder);
            string output = OutputFolder + "/" + name + ".bytes";
            if (!File.Exists(output) || !File.ReadAllBytes(output).SequenceEqual(bytes))
            {
                File.WriteAllBytes(output, bytes);
                AssetDatabase.ImportAsset(output);
            }

            var issues = new List<string>();
            int rows = Validate(name, bytes, issues);
            if (rows < 0) Debug.LogWarning($"[TableImporter] {name}: GameData에 등록되지 않은 표다 (복사만 했다)");
            else if (issues.Count == 0) Debug.Log($"[TableImporter] {name}: {rows}행 OK");
            else Debug.LogWarning($"[TableImporter] {name}: {rows}행, 문제 {issues.Count}개\n" + string.Join("\n", issues));
        }

        // 새 표를 추가하면 여기에도 한 줄 추가한다. 등록되지 않은 이름이면 -1
        static int Validate(string name, byte[] bytes, List<string> issues)
        {
            switch (name)
            {
                case "Mushroom": return Check(new DataTable<MushroomData>(name, GameData.Mushrooms.IndexMin, GameData.Mushrooms.IndexMax), bytes, issues);
                case "Harvester": return Check(new DataTable<HarvesterData>(name, GameData.Harvesters.IndexMin, GameData.Harvesters.IndexMax), bytes, issues);
                case "Skill": return Check(new DataTable<SkillData>(name, GameData.Skills.IndexMin, GameData.Skills.IndexMax), bytes, issues);
                case "Boss": return Check(new DataTable<BossData>(name, GameData.Bosses.IndexMin, GameData.Bosses.IndexMax), bytes, issues);
                default: return -1;
            }
        }

        static int Check<T>(DataTable<T> table, byte[] bytes, List<string> issues) where T : TableRow, new()
        {
            GameData.Parse(table, bytes, issues);
            return table.Count;
        }

        static void RemoveCopy(string path)
        {
            string output = OutputFolder + "/" + Path.GetFileNameWithoutExtension(path) + ".bytes";
            if (File.Exists(output)) AssetDatabase.DeleteAsset(output);
        }
    }
}
