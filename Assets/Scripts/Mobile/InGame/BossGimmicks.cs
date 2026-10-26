using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 보스 판의 기믹 (Boss.xlsx gimmick·gimmickValue·gimmickInterval, PC판 보스 기믹을 옮김).
    //   Heal  (숲 속의 청소부): interval초마다 최대 체력의 value만큼 회복 (분신 포함 살아 있는 보스 모두)
    //   Purge (죽음의 천사):    interval초마다 필드의 임시 수확기(분열체)를 모두 없앤다
    //   Split (환상 버섯):      본체 체력이 66%·33%가 될 때 분신을 하나씩 부른다. 분신 체력 = 그때 본체 체력 × value (PC판: 1). 분신까지 모두 쓰러뜨려야 처치
    //   Move  (움직이는 버섯):  좌우로 value px/초로 쉬지 않고 움직인다 (벽에서 돌아선다)
    public class BossGimmicks : MonoBehaviour
    {
        [SerializeField] HarvestField field;
        [SerializeField] float[] splitAt = { 0.66f, 0.33f };   // 남은 체력 비율

        BossData boss;
        Mushroom main;
        float timer;
        int nextSplit;
        readonly Dictionary<Mushroom, float> moveDir = new Dictionary<Mushroom, float>();   // 몸마다 좌우 방향
        readonly List<Mushroom> parts = new List<Mushroom>();

        void Update() => Step(Time.deltaTime);

        // dt초 진행. 테스트에서도 직접 부를 수 있다
        public void Step(float dt)
        {
            if (!field.Running || field.Stats == null || !field.Stats.BossRound || dt <= 0f) return;
            if (boss == null)
            {
                boss = field.Stats.Boss;
                foreach (var m in field.Mushrooms) if (m.IsBoss) main = m;
                timer = boss.gimmickInterval;
            }

            switch (boss.gimmick)
            {
                case BossGimmick.Heal:
                    if (Tick(dt))
                        foreach (var m in field.Mushrooms)
                            if (m.IsBoss && m.Alive) m.Heal(Mathf.CeilToInt(m.MaxHp * boss.gimmickValue));
                    break;
                case BossGimmick.Purge:
                    if (Tick(dt)) field.PurgeTemporaryHarvesters();
                    break;
                case BossGimmick.Split:
                    Split();
                    break;
                case BossGimmick.Move:
                    Move(dt);
                    break;
            }
        }

        bool Tick(float dt)
        {
            if (boss.gimmickInterval <= 0f) return false;
            timer -= dt;
            if (timer > 0f) return false;
            timer += boss.gimmickInterval;
            return true;
        }

        void Split()
        {
            if (main == null || !main.Alive || nextSplit >= splitAt.Length) return;
            if ((float)main.Hp / main.MaxHp > splitAt[nextSplit]) return;
            nextSplit++;
            var b = field.Stats.Base;
            float size = b.bossSize * b.splitCloneSize;
            Rect r = field.Rect.rect;
            float x = Mathf.Clamp(main.Position.x + (nextSplit % 2 == 1 ? -1f : 1f) * main.Radius * 1.4f, r.xMin + main.Radius, r.xMax - main.Radius);
            float y = Mathf.Min(main.Position.y + main.Radius * 0.6f, r.yMax - main.Radius);   // 본체보다 조금 위
            field.SpawnBossPart(new Vector2(x, y), main.Hp * (boss.gimmickValue > 0f ? boss.gimmickValue : 1f), size);
        }

        void Move(float dt)
        {
            Rect r = field.Rect.rect;
            parts.Clear();
            foreach (var m in field.Mushrooms) if (m.IsBoss && m.Alive) parts.Add(m);
            foreach (var m in parts)
            {
                if (!moveDir.TryGetValue(m, out float dir)) dir = 1f;
                var p = m.Position;
                p.x += dir * boss.gimmickValue * dt;
                if (p.x + m.Radius > r.xMax) { p.x = r.xMax - m.Radius; dir = -1f; }
                else if (p.x - m.Radius < r.xMin) { p.x = r.xMin + m.Radius; dir = 1f; }
                moveDir[m] = dir;
                m.Place(p);
            }
            // 유도·번개가 보스를 따라가도록 군락지 가운데도 옮긴다
            if (parts.Count > 0 && field.Colonies.Count > 0) field.Colonies[0].center = parts[0].Position;
        }
    }
}
