using UnityEngine;

namespace MoreMush.Mobile
{
    // 지갑: 골드·균사석. PlayerPrefs("Wallet.Gold", "Wallet.Mycelite")에 저장한다 (세이브 구조가 생기면 옮긴다).
    // Main 방치 전투(IdleBattle)에서 웨이브 적을 잡으면 골드, 보스를 잡으면 균사석(+골드)이 들어오고, 비운 동안 골드는 IdleAway가 넣는다.
    // 강화·뽑기·스킬에서 뺀다. 화면(WalletLabel 등)은 Changed를 듣고 다시 그린다
    public static class Wallet
    {
        const string GoldKey = "Wallet.Gold";
        const string MyceliteKey = "Wallet.Mycelite";

        public static event System.Action Changed;

        // 예전 테스트 지급(처음 한 번 균사석 50,000)을 받은 기기에서 한 번 되돌린다 — 지갑에는 직접 번 재화만 남긴다
        const long TestMycelite = 50000;
        const string TestGrantKey = "Wallet.TestGrant";
        const string TestReclaimKey = "Wallet.TestReclaimed";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ReclaimTestCurrency()
        {
            if (PlayerPrefs.GetInt(TestGrantKey, 0) != 1 || PlayerPrefs.GetInt(TestReclaimKey, 0) == 1) return;
            PlayerPrefs.SetInt(TestReclaimKey, 1);
            Set(Currency.Mycelite, Mycelite - TestMycelite);   // 이미 써 버렸으면 0에서 멈춘다
        }

        // 골드는 long이라 문자열로 저장한다 (PlayerPrefs에 long이 없다)
        public static long Gold => long.TryParse(PlayerPrefs.GetString(GoldKey, "0"), out var v) ? v : 0;
        public static long Mycelite => long.TryParse(PlayerPrefs.GetString(MyceliteKey, "0"), out var v) ? v : 0;

        public static long Get(Currency currency) => currency == Currency.Mycelite ? Mycelite : Gold;

        public static void Add(Currency currency, long amount)
        {
            if (amount <= 0) return;
            Set(currency, Get(currency) + amount);
        }

        public static bool CanAfford(Currency currency, long price) => price <= 0 || Get(currency) >= price;

        public static bool TrySpend(Currency currency, long price)
        {
            if (price <= 0) return true;
            long have = Get(currency);
            if (have < price) return false;
            Set(currency, have - price);
            return true;
        }

        static void Set(Currency currency, long value)
        {
            PlayerPrefs.SetString(currency == Currency.Mycelite ? MyceliteKey : GoldKey, System.Math.Max(0, value).ToString());
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // 화면 표시: 1,234 / 12.3만 / 4.5억 …
        public static string Format(long value)
        {
            if (value < 100000) return value.ToString("N0");
            string[] units = { "만", "억", "조", "경" };
            double v = value;
            int u = -1;
            while (v >= 10000 && u < units.Length - 1) { v /= 10000; u++; }
            return (v >= 100 ? v.ToString("0") : v.ToString("0.#")) + units[u];
        }
    }
}
