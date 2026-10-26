using UnityEditor;
using UnityEngine;

namespace MoreMush.Mobile.EditorTools
{
    // 개발용 InGame 메뉴. 판 수(보스 판 주기)를 바꾼다
    static class InGameDebugMenu
    {
        const int BossEvery = 10;   // HarvestGame의 RoundBase.bossEvery 기본값과 같게

        [MenuItem("MoreMush/Debug/Next Round Is Boss")]
        static void NextRoundBoss()
        {
            int done = PlayerPrefs.GetInt(HarvestGame.RoundKey, 0);
            int next = done + 1;
            int target = (next + BossEvery - 1) / BossEvery * BossEvery;   // 다음 보스 판 번호
            PlayerPrefs.SetInt(HarvestGame.RoundKey, target - 1);
            PlayerPrefs.Save();
            Debug.Log($"[InGameDebug] 다음 판 = {target}번째 판 (보스 판)");
        }

        [MenuItem("MoreMush/Debug/Reset Round Count")]
        static void ResetRounds()
        {
            PlayerPrefs.DeleteKey(HarvestGame.RoundKey);
            PlayerPrefs.Save();
            Debug.Log("[InGameDebug] 판 수를 0으로 되돌렸다");
        }
    }
}
