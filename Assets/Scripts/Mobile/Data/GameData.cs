using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 기존 스킬 트리/판 플레이용 데이터. 방치형 통합 표와 분리해 Legacy 복사본을 읽는다.
    // 사용: GameData.Mushrooms.Get(1001), GameData.Skills.All
    // Legacy는 자동 임포트에서 제외된다. 새 방치형 통합 표는 GameBalance.xlsx (임포트/검증만 연결).
    public static class GameData
    {
        public const string ResourceFolder = "Tables/Legacy";

        // 표 이름(= 엑셀 파일 이름 = 시트 이름)과 index 범위
        public static readonly DataTable<MushroomData> Mushrooms = new DataTable<MushroomData>("Mushroom", 1001, 1999);
        public static readonly DataTable<HarvesterData> Harvesters = new DataTable<HarvesterData>("Harvester", 2001, 2999);
        public static readonly DataTable<SkillData> Skills = new DataTable<SkillData>("Skill", 3001, 3999);
        public static readonly DataTable<StageData> Stages = new DataTable<StageData>("Stage", 5001, 5999);
        public static readonly DataTable<BossData> Bosses = new DataTable<BossData>("Boss", 6001, 6999);

        public static bool Loaded { get; private set; }

        // 씬 로드 전에 한 번 읽어 둔다. 다시 읽고 싶으면 Reload
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoLoad() => Reload();

        public static void Reload()
        {
            var issues = new List<string>();
            Load(Mushrooms, issues);
            Load(Harvesters, issues);
            Load(Skills, issues);
            Load(Stages, issues);
            Load(Bosses, issues);
            Loaded = true;
            foreach (var issue in issues) Debug.LogWarning("[GameData] " + issue);
        }

        static void Load<T>(DataTable<T> table, List<string> issues) where T : TableRow, new()
        {
            var asset = Resources.Load<TextAsset>(ResourceFolder + "/" + table.Name);
            if (asset == null)
            {
                issues.Add($"[{table.Name}] Resources/{ResourceFolder}/{table.Name}.bytes 없음");
                table.Load(null, new List<string>());
                return;
            }
            Parse(table, asset.bytes, issues);
            Resources.UnloadAsset(asset);
        }

        // 엑셀 바이트에서 표 이름과 같은 시트를 읽는다. 없으면 첫 시트
        public static void Parse<T>(DataTable<T> table, byte[] xlsx, List<string> issues) where T : TableRow, new()
        {
            Dictionary<string, List<string[]>> sheets;
            try { sheets = XlsxReader.Read(xlsx); }
            catch (System.Exception e)
            {
                issues.Add($"[{table.Name}] xlsx를 읽지 못했다: {e.Message}");
                table.Load(null, new List<string>());
                return;
            }

            if (!sheets.TryGetValue(table.Name, out var sheet))
                foreach (var first in sheets.Values) { sheet = first; break; }
            table.Load(sheet, issues);
        }
    }
}
