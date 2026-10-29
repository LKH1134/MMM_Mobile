using TMPro;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 재화 숫자 글자. 같은 오브젝트의 TMP 글자를 Wallet 값으로 채우고, 값이 바뀔 때마다 다시 쓴다
    [RequireComponent(typeof(TMP_Text))]
    public class WalletLabel : MonoBehaviour
    {
        [SerializeField] Currency currency = Currency.Gold;

        TMP_Text label;

        void Awake() => label = GetComponent<TMP_Text>();

        void OnEnable()
        {
            Wallet.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => Wallet.Changed -= Refresh;

        void Refresh() => label.text = Wallet.Format(Wallet.Get(currency));
    }
}
