using UnityEngine;

namespace MoreMush.Mobile
{
    // 개발자 모드(DevMode) 전용 세계수 조작. 게임 흐름에서는 부르지 않는다
    public static partial class WorldTree
    {
        // 표의 모든 장식을 가진 것으로 (놓기는 그대로 칸 수 안에서)
        public static int DevUnlockAllDecos()
        {
            int added = 0;
            foreach (var d in GameBalance.Decos)
            {
                if (D.decosOwned.Contains(d.key)) continue;
                D.decosOwned.Add(d.key);
                added++;
            }
            Notify(true);
            return added;
        }

        // 제작 중인 장식을 모두 지금 끝난 것으로 (받기는 공방에서)
        public static int DevFinishCrafts()
        {
            long now = NowTicks;
            foreach (var c in D.crafts) c.endTicks = now;
            Notify(true);
            return D.crafts.Count;
        }

        // 창고 버섯 개수를 정한다 (0 이하면 뺀다)
        public static void DevSetStore(int enemy, long count)
        {
            var have = D.store.Find(s => s.enemy == enemy);
            if (count <= 0) { if (have != null) D.store.Remove(have); }
            else if (have != null) have.count = count;
            else D.store.Add(new StoreEntry { enemy = enemy, count = count });
            Notify(true);
        }

        public static void DevSetStage(int stage)
        {
            D.stage = Mathf.Clamp(stage, 1, MaxStage);
            D.ring = Mathf.Clamp(D.ring, 1, RingMax);
            TrimPlacedToSlots();
            Notify(true);
        }

        public static void DevSetRing(int ring)
        {
            D.ring = Mathf.Clamp(ring, 1, RingMax);
            Notify(true);
        }
    }
}
