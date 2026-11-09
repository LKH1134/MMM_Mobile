using UnityEngine;

namespace MoreMush.Mobile
{
    // 방치 전투(Main IdleBattle)와 같은 그림을 벽돌깨기 던전(InGame)에서도 쓰려고 모은 것.
    //   수확기 = 장착한 무기 그림 (없으면 Harvesters 폴더 첫 그림)
    //   버섯 = Enumy 군락 시트 조각 (보스 조각 제외). 독성·약용은 상수 enemyToxicRate·enemyMedicinalRate 비율, 나머지는 식용.
    //   군락지 둘레의 단일 버섯은 가운데 군락과 같은 종의 Index 단일 버섯 조각 (UnitOf)
    // 그림 고르기는 따로 둔 난수를 쓴다 — 판 진행용 UnityEngine.Random 순서를 건드리면 측정기(같은 시드 = 같은 결과)가 달라진다
    public static class BattleLooks
    {
        const string KindFolder = "Art/Dream/Characters/Enumy";        // IdleBattle.kindFolder와 같은 폴더
        const string HarvesterFolder = "Art/Dream/Harvesters";         // IdleBattle.harvesterFolder와 같은 폴더

        static Sprite[] edible, toxic, medicinal, harvesters;
        static readonly System.Random rng = new System.Random();

        static void EnsureEnemies()
        {
            if (edible != null) return;
            var kinds = System.Array.FindAll(Resources.LoadAll<Sprite>(KindFolder), sp => !GameBalance.IsBossSprite(sp.name));
            edible = System.Array.FindAll(kinds, sp => GgomaSkills.KindOf(sp.name) == MushroomKind.Edible);
            toxic = System.Array.FindAll(kinds, sp => GgomaSkills.KindOf(sp.name) == MushroomKind.Toxic);
            medicinal = System.Array.FindAll(kinds, sp => GgomaSkills.KindOf(sp.name) == MushroomKind.Medicinal);
        }

        // 버섯 그림 묶음 하나 (IdleBattle.PickEnemyPool과 같은 비율). 그림이 없으면 빈 배열
        public static Sprite[] PickEnemyPool()
        {
            EnsureEnemies();
            double r = rng.NextDouble();
            double tx = GameBalance.Const("enemyToxicRate"), md = GameBalance.Const("enemyMedicinalRate");
            if (r < tx && toxic.Length > 0) return toxic;
            if (r >= tx && r < tx + md && medicinal.Length > 0) return medicinal;
            return edible;
        }

        // 묶음에서 하나 (비었으면 null — 부르는 쪽이 프리팹 그림을 그대로 둔다)
        public static Sprite Pick(Sprite[] pool) => pool != null && pool.Length > 0 ? pool[rng.Next(pool.Length)] : null;

        const string UnitFolder = "Art/Dream/Index";                   // 단일 버섯 시트 (군락 시트와 조각 번호가 같다)
        static System.Collections.Generic.Dictionary<string, Sprite> units;

        // 군락 그림과 같은 종의 단일 버섯 그림 (Mush_X_Y_Colony_n → Mush_X_Y_Unit_n). 없으면 null
        public static Sprite UnitOf(Sprite colony)
        {
            if (colony == null) return null;
            if (units == null)
            {
                units = new System.Collections.Generic.Dictionary<string, Sprite>();
                foreach (var sp in Resources.LoadAll<Sprite>(UnitFolder)) units[sp.name] = sp;
            }
            return units.TryGetValue(colony.name.Replace("_Colony_", "_Unit_"), out var unit) ? unit : null;
        }

        // 던지는 수확기 그림 (없으면 null)
        public static Sprite Harvester()
        {
            var weapon = Equipment.Weapon.Sprite(Equipment.Weapon.Equipped);
            if (weapon != null) return weapon;
            if (harvesters == null) harvesters = Resources.LoadAll<Sprite>(HarvesterFolder);
            return harvesters.Length > 0 ? harvesters[0] : null;
        }
    }
}
