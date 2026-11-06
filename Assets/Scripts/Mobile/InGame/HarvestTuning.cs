using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MoreMush.Mobile
{
    // 수확 판(던전 1 InGame 등) 수치를 수확전투·수확등급 시트에서 넣는다. 씬·프리팹의 인스펙터 값은 표에 행이 없을 때만 남는다.
    // 어느 던전의 행인지는 지금 씬 이름 = 던전 시트 scene으로 찾는다.
    // scope: RoundBase · HarvestField · FieldInput · Harvester · Cluster · Single · Bar · SkillEffects · FieldDevices.
    // BossGimmicks.splitAtN·StageHud.speedN 배열 항목은 각 컴포넌트가 Number로 읽는다.
    // 프리팹은 원본을 건드리지 않고, 표 값을 넣은 복사본(Template)을 꺼진 보관함 아래에 두고 풀·크기 계산이 그것을 쓴다
    public static class HarvestTuning
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static Transform holder;     // 꺼진 보관함 (씬과 함께 사라진다)

        // 지금 씬의 던전 번호 (던전 시트에 없으면 0 = 표 값 없음).
        // 벽돌깨기 던전 1~3은 같은 씬을 쓰므로 들어온 던전(DungeonSession.Current)이 이 씬이면 그 번호
        public static int CurrentDungeon
        {
            get
            {
                string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                var session = GameBalance.Dungeon(DungeonSession.Current);
                if (session != null && session.implemented && session.scene == scene) return session.dungeon;
                foreach (var d in GameBalance.Dungeons)
                    if (d.implemented && d.scene == scene) return d.dungeon;
                return 0;
            }
        }

        // 수확전투 시트의 기준 던전 번호: 같은 씬을 쓰는 던전 중 행이 있는 가장 앞 던전 (던전 2·3은 던전 1 판 설정이 기준)
        static int TuningDungeon
        {
            get
            {
                int dungeon = CurrentDungeon;
                var row = GameBalance.Dungeon(dungeon);
                if (row == null) return dungeon;
                int best = 0;
                foreach (var d in GameBalance.Dungeons)
                    if (d.implemented && d.scene == row.scene && HasRows(d.dungeon) && (best == 0 || d.dungeon < best)) best = d.dungeon;
                return best != 0 ? best : dungeon;
            }
        }

        static bool HasRows(int dungeon)
        {
            foreach (var r in GameBalance.HarvestSettings) if (r.dungeon == dungeon) return true;
            return false;
        }

        // 이 던전의 행. 같은 씬을 쓰는 기준 던전(던전 1)과 다르면, 이 던전에 없는 필드는 기준 던전 값을 쓴다
        // (던전 2는 황금 버섯 확률 한 줄만 따로 두고 나머지는 던전 1 설정 그대로)
        static IEnumerable<HarvestSettingRow> Rows(string scope)
        {
            int own = CurrentDungeon, baseDungeon = TuningDungeon;
            var overridden = new HashSet<string>();
            if (own != baseDungeon)
                foreach (var r in GameBalance.HarvestSettings)
                    if (r.dungeon == own && r.scope == scope) { overridden.Add(r.field); yield return r; }
            foreach (var r in GameBalance.HarvestSettings)
                if (r.dungeon == baseDungeon && r.scope == scope && !overridden.Contains(r.field)) yield return r;
        }

        // target(컴포넌트 또는 RoundBase)의 필드에 그 scope 행 값을 넣는다
        public static void Apply(object target, string scope)
        {
            if (target == null) return;
            var type = target.GetType();
            foreach (var r in Rows(scope))
            {
                var f = type.GetField(r.field, Fields);
                if (f == null) { Debug.LogWarning($"[HarvestTuning] {type.Name}에 {r.field} 필드가 없다 ({r.key})"); continue; }
                if (f.FieldType == typeof(int)) f.SetValue(target, (int)Math.Round(r.value));
                else if (f.FieldType == typeof(long)) f.SetValue(target, (long)Math.Round(r.value));
                else if (f.FieldType == typeof(float)) f.SetValue(target, (float)r.value);
                else if (f.FieldType == typeof(double)) f.SetValue(target, r.value);
                else if (f.FieldType == typeof(bool)) f.SetValue(target, r.value != 0);
                else Debug.LogWarning($"[HarvestTuning] {type.Name}.{r.field}는 넣을 수 없는 자료형 {f.FieldType.Name}");
            }
        }

        // scope 행의 값 (없으면 fallback)
        public static float Number(string scope, string field, float fallback)
        {
            foreach (var r in Rows(scope)) if (r.field == field) return (float)r.value;
            return fallback;
        }

        // 프리팹의 표 값 복사본. 원본 프리팹 에셋은 바꾸지 않는다
        public static T Template<T>(T prefab, string scope) where T : Component
        {
            if (prefab == null) return null;
            if (holder == null)
            {
                var go = new GameObject("HarvestTuningTemplates");
                go.SetActive(false);
                holder = go.transform;
            }
            var copy = Object.Instantiate(prefab, holder, false);
            copy.name = prefab.name;
            Apply(copy, scope);
            return copy;
        }
    }
}
