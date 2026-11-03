using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 수확 보따리 받기 버튼: 쌓인 골드·균사석과 보관 시간(가득 차면 "가득!")을 1초마다 보여 주고, 누르면 받는다
    [RequireComponent(typeof(Button))]
    public class TreeBagButton : MonoBehaviour
    {
        [SerializeField] TMP_Text label;      // "수확 보따리 받기" + 쌓인 양
        [SerializeField] TMP_Text timeText;   // 보관 시간. 비워도 된다

        float tick;

        void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Claim);
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        }

        void OnEnable()
        {
            WorldTree.Changed += Show;
            Show();
        }

        void OnDisable() => WorldTree.Changed -= Show;

        void Update()
        {
            tick -= Time.unscaledDeltaTime;
            if (tick > 0f) return;
            tick = 1f;
            Show();
        }

        void Show()
        {
            long gold = WorldTree.BagGold, mycelite = WorldTree.BagMycelite;
            string text = "수확 보따리 받기\n+" + Wallet.Format(gold) + " 골드";
            if (mycelite > 0) text += " · +" + Wallet.Format(mycelite) + " 균사석";
            string time = WorldTree.BagFull ? "가득!" : TreeUi.Duration(WorldTree.BagSeconds) + " / " + TreeUi.Duration(WorldTree.BagCapSeconds);
            if (timeText != null) timeText.text = time;
            else text += "\n" + time;
            if (label != null) label.text = text;
        }

        void Claim()
        {
            var (gold, mycelite) = WorldTree.ClaimBag();
            var screen = FindAnyObjectByType<WorldTreeScreen>();
            if (screen == null) return;
            if (gold <= 0 && mycelite <= 0) screen.ShowToast("아직 보따리가 비어 있어요");
            else screen.ShowToast("+" + Wallet.Format(gold) + " 골드" + (mycelite > 0 ? " · +" + Wallet.Format(mycelite) + " 균사석" : "") + "을 받았어요!");
        }
    }
}
