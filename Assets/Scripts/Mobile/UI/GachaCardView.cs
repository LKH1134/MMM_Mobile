using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 뽑기 카드 오른쪽 열의 화면 조각 (프리팹 Gacha_Panel1에 배치): 맨 위 = 뽑기 레벨 "LV n" + 경험치 막대 "exp/다음",
    // 그 아래 = 광고 소환(초록, 한 줄) · 재화 소환 11회/35회 — 버튼마다 "n회 소환" 위 줄 + 아래 줄(재화 아이콘·가격).
    // 모양·자리는 프리팹에서 고치고, GachaPanel이 값만 채운다
    public static class GachaCardView
    {
        [System.Serializable] public class ExpRow { public TMP_Text level, value; public RectTransform fill; }
        [System.Serializable] public class ButtonText { public TMP_Text title, sub; public Image costIcon; }

        public static void SetExp(ExpRow row, int level, int exp, int need)
        {
            if (row == null || row.level == null) return;
            row.level.text = $"LV {level}";
            bool max = need <= 0;
            float k = max ? 1f : Mathf.Clamp01(exp / (float)need);
            if (row.fill != null)
            {
                row.fill.anchorMax = new Vector2(k, 1f);
                row.fill.gameObject.SetActive(k > 0f);
            }
            if (row.value != null) row.value.text = max ? Loc.T("upgrade.max") : $"{exp}/{need}";
        }
    }
}
