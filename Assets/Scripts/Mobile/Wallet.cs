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

        // 골드는 long이라 문자열로 저장한다 (PlayerPrefs에 long이 없다).
        // 잔액은 화면마다 자주 읽으므로 처음 한 번만 읽어 메모리에 두고, 쓸 때 둘 다 고친다. PlayerPrefs를 밖에서 지웠으면 Reload
        static long? gold, mycelite;

        public static long Gold => gold ??= Load(GoldKey);
        public static long Mycelite => mycelite ??= Load(MyceliteKey);

        // 망가진 값·음수는 0으로 (잔액이 음수면 화면에 "-5"가 뜨고 계산이 꼬인다)
        static long Load(string key) =>
            long.TryParse(PlayerPrefs.GetString(key, "0"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var v) ? System.Math.Max(0, v) : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload() => gold = mycelite = null;

        public static long Get(Currency currency) => currency == Currency.Mycelite ? Mycelite : Gold;

        public static void Add(Currency currency, long amount)
        {
            if (amount <= 0) return;
            long have = Get(currency);
            Set(currency, amount > long.MaxValue - have ? long.MaxValue : have + amount);   // 넘치면 음수 → 0이 되지 않게 최댓값에서 멈춘다
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

        // 개발자 모드(DevMode) 전용: 잔액을 그대로 정한다
        public static void DevSet(Currency currency, long value) => Set(currency, value);

        static void Set(Currency currency, long value)
        {
            value = System.Math.Max(0, value);
            if (currency == Currency.Mycelite) mycelite = value;
            else gold = value;
            PlayerPrefs.SetString(currency == Currency.Mycelite ? MyceliteKey : GoldKey, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            SaveScheduler.MarkDirty();      // 전투 골드는 1초마다 들어온다 — 파일 쓰기는 몇 초에 한 번 (SaveScheduler)
            Changed?.Invoke();
        }

        // 화면 표시 (방치형 알파벳 단위, 1,000마다 한 단위): 999 / 1.23a / 45.6b / 789c … z 다음은 aa, ab …
        public static string Format(long value) => Format((double)value);

        public static string Format(double value)
        {
            if (double.IsNaN(value)) return "0";
            if (value < 0) return "-" + Format(-value);
            if (value < UnitStep) return System.Math.Floor(value).ToString("0");
            if (double.IsInfinity(value)) return "∞";
            int unit = 0;
            double v = value;
            while (v >= UnitStep) { v /= UnitStep; unit++; }
            // 반올림하면 "1000a"가 되는 값은 다음 단위로 ("1a"가 아니라 "1b")
            if (v >= UnitStep - 0.5) { v /= UnitStep; unit++; }
            string digits = v >= 100 ? v.ToString("0") : v >= 10 ? v.ToString("0.#") : v.ToString("0.##");
            return digits + UnitName(unit);
        }

        const double UnitStep = 1000;

        // 1 → a, 26 → z, 27 → aa, 28 → ab … (몇 번째 단위인지, 1부터)
        static string UnitName(int unit)
        {
            if (unit <= Letters) return ((char)('a' + unit - 1)).ToString();
            int n = unit - Letters - 1;     // aa부터 0
            return ((char)('a' + n / Letters % Letters)).ToString() + (char)('a' + n % Letters);
        }

        const int Letters = 26;
    }
}
