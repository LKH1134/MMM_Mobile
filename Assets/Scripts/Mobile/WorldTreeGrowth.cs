using System;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 세계수 성장 단계: 0(새싹)부터 MaxStage까지. 성장 조건·비용은 아직 없다 — 정해지면 Grow를 부르는 쪽에서 검사한다.
    // PlayerPrefs("WorldTree.Stage"). 드물게 바뀌는 값이라 바로 저장한다
    public static class WorldTreeGrowth
    {
        public const int MaxStage = 3;
        const string Key = "WorldTree.Stage";

        public static event Action Changed;

        public static int Stage => Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, MaxStage);
        public static bool IsMax => Stage >= MaxStage;

        // 한 단계 자란다. 이미 다 자랐으면 false
        public static bool Grow()
        {
            if (IsMax) return false;
            Set(Stage + 1);
            return true;
        }

        public static void Set(int stage)
        {
            PlayerPrefs.SetInt(Key, Mathf.Clamp(stage, 0, MaxStage));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
