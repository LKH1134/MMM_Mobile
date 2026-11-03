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

        // 지금 씬의 던전 번호 (던전 시트에 없으면 0 = 표 값 없음)
        public static int CurrentDungeon
        {
            get
            {
                string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                foreach (var d in GameBalance.Dungeons)
                    if (d.implemented && d.scene == scene) return d.dungeon;
                return 0;
            }
        }

        static IEnumerable<HarvestSettingRow> Rows(string scope)
        {
            int dungeon = CurrentDungeon;
            foreach (var r in GameBalance.HarvestSettings)
                if (r.dungeon == dungeon && r.scope == scope) yield return r;
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
