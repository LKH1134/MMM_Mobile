using TMPro;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 위쪽 공용 바 (프리팹 TopBar: 세계수 레벨 · 골드 · 균사석 · 설정). Main·WorldTree 어디에 두어도 같은 값을 보여 준다.
    // 설정 버튼 동작은 씬에서 연결한다 (Main = OptionPopup.Toggle, WorldTree = WorldTreeScreen)
    public class TopBarView : MonoBehaviour
    {
        [SerializeField] TMP_Text levelText;
        [SerializeField] TMP_Text goldText;
        [SerializeField] TMP_Text myceliteText;

        void OnEnable()
        {
            Wallet.Changed += Refresh;
            WorldTree.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            Wallet.Changed -= Refresh;
            WorldTree.Changed -= Refresh;
        }

        void Refresh()
        {
            if (levelText != null) levelText.text = "Lv." + Mathf.Max(1, WorldTree.Stage);   // 세계수 단계 = 계정 레벨
            if (goldText != null) goldText.text = Wallet.Format(Wallet.Gold);
            if (myceliteText != null) myceliteText.text = Wallet.Format(Wallet.Mycelite);
        }
    }
}
