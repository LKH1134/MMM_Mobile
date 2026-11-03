using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 세계수 효과 종류 (가호·꼬마 특기·장식). 비율은 0.05 = 5%. 표의 TreeEffect 열 이름과 같아야 한다
    public enum TreeEffect { Attack, Hp, Speed, CritChance, CritDamage, KillGold, BossMycelite, Harvest, BagGold, BagMycelite, BagHours, CraftSpeed }

    // 꾸미기 자리 (탭): 뿌리·줄기·가지·수관
    public enum TreeZone { Root, Trunk, Branch, Crown }

    // 세계수: 성장(나이테·공물·가호)·꼬마(호감도·진화·부탁)·공방·꾸미기·수확 보따리·의뢰(일일·주간). 수치는 GameBalance.xlsx 세계수 시트들에서 온다.
    // 저장은 PlayerPrefs("WorldTree.Save") JSON 하나. 자주 바뀌는 값(창고 버섯은 처치마다)은 메모리만 고치고
    // SaveScheduler가 저장할 때 JSON을 쓴다. 진화·제작처럼 드문 쓰기는 바로 저장한다.
    // PC MoreMustMush의 Game.Farm*.cs를 모바일 방치형에 맞게 옮겼다 (창고 버섯 = 방치 전투 처치, 꼬마 = 뽑기 꼬마, 보따리 = 단계별 시간당 표)
    public static partial class WorldTree
    {
        const string SaveKey = "WorldTree.Save";

        [Serializable] public class StoreEntry { public int enemy; public long count; }
        [Serializable] public class KidState { public string id; public int star; public int affinity; }
        [Serializable] public class Request { public string kid; public int enemy; public int count; public bool done; }
        [Serializable] public class Craft { public string deco; public long startTicks, endTicks; }

        [Serializable]
        public class SaveData
        {
            public int stage = 1, ring = 1;
            public double exp;
            public List<StoreEntry> store = new List<StoreEntry>();
            public List<KidState> kids = new List<KidState>();
            public List<Request> requests = new List<Request>();
            public string requestDay = "", requestWeek = "";
            public int weekDone;
            public bool weekClaimed;
            public List<Craft> crafts = new List<Craft>();
            public string rushDay = "";
            public int rushUsed;
            public List<string> decosOwned = new List<string>();
            public List<string> decosPlaced = new List<string>();
            public List<string> decosFirstPlaced = new List<string>();
            public double bagGold, bagMycelite, bagSeconds;
            public long bagTicks;
            public string questDay = "", questWeek = "";
            public List<Quest> questDaily = new List<Quest>();
            public List<Quest> questWeekly = new List<Quest>();
        }

        static SaveData data;
        static bool pending;

        // 무엇이든 바뀌면 (화면이 다시 그린다)
        public static event Action Changed;

        static SaveData D
        {
            get
            {
                if (data == null) Load();
                return data;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload()
        {
            data = null;
            pending = false;
            nextEnsureTicks = 0;
            bonusDirty = true;
            enemyBySprite = null;
            SaveScheduler.BeforeFlush -= WritePending;
            SaveScheduler.BeforeFlush += WritePending;
        }

        static void Load()
        {
            string json = PlayerPrefs.GetString(SaveKey, "");
            data = null;
            if (json.Length > 0)
            {
                try { data = JsonUtility.FromJson<SaveData>(json); }
                catch (Exception e) { Debug.LogError("[WorldTree] 저장 데이터를 읽지 못해 새로 시작한다: " + e.Message); }
            }
            if (data == null) data = new SaveData();
            data.stage = Mathf.Clamp(data.stage, 1, MaxStage);
            data.ring = Mathf.Clamp(data.ring, 1, RingMax);
            if (data.bagTicks <= 0) data.bagTicks = NowTicks;
            TrimPlacedToSlots();
            bonusDirty = true;
        }

        // 자리별로 지금 단계 칸 수보다 많이 놓인 장식은 뒤에 놓은 것부터 빼서 보유 목록으로 돌린다 (칸 수가 줄어든 표·옛 세이브)
        static void TrimPlacedToSlots()
        {
            foreach (TreeZone zone in Enum.GetValues(typeof(TreeZone)))
            {
                int over = PlacedIn(zone).Count - ZoneSlots(zone);
                for (int i = data.decosPlaced.Count - 1; i >= 0 && over > 0; i--)
                {
                    var d = Deco(data.decosPlaced[i]);
                    if (d == null || d.zone != zone) continue;
                    data.decosPlaced.RemoveAt(i);
                    over--;
                    pending = true;
                }
            }
        }

        // 메모리만 고쳤다: 다음 저장 때 쓴다
        static void MarkDirty()
        {
            pending = true;
            SaveScheduler.MarkDirty();
        }

        // 바로 저장 (드물고 중요한 쓰기)
        static void SaveNow()
        {
            pending = true;
            WritePending();
            PlayerPrefs.Save();
        }

        static void WritePending()
        {
            if (!pending || data == null) return;
            pending = false;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        }

        // 값이 바뀐 뒤: 효과 다시 계산·저장 예약·화면 갱신
        static void Notify(bool saveNow = false)
        {
            bonusDirty = true;
            if (saveNow) SaveNow(); else MarkDirty();
            Changed?.Invoke();
        }

        // ── 시간 (KST 날짜·주) ──
        static long NowTicks => DateTime.UtcNow.Ticks;
        static string Today => DungeonTickets.Today;

        // 이번 주(월요일 시작) 첫날
        static string ThisWeek
        {
            get
            {
                var now = DungeonTickets.KstNow.Date;
                int back = ((int)now.DayOfWeek + 6) % 7;
                return now.AddDays(-back).ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        // ── 표 ──
        public static int MaxStage => GameBalance.TreeStages.Count;
        public static int RingMax => GameBalance.ConstInt("treeRingMax");
        static TreeStageRow Row(int stage) => GameBalance.TreeStages[Mathf.Clamp(stage, 1, MaxStage) - 1];
        public static TreeStageRow CurrentRow => Row(Stage);

        // 지금 스테이지 적 1마리 처치 골드 × kills (골드 비용·보상은 이렇게 적는다 — 스테이지가 오르면 같이 커진다)
        static long KillGold(double kills) => IdleProgress.ClampToLong(Math.Ceiling((double)IdleProgress.EnemyGold(IdleProgress.Stage) * kills));

        public static Sprite TreeSprite(int stage)
        {
            string path = GameBalance.ResourcePath(Row(stage).resourceId);
            return path != null ? Resources.Load<Sprite>(path) : null;
        }

        public static string Percent(double v) => (v * 100).ToString("0.#") + "%";

        public static string EffectText(TreeEffect effect, double v)
        {
            switch (effect)
            {
                case TreeEffect.Attack: return "공격력 +" + Percent(v);
                case TreeEffect.Hp: return "체력 +" + Percent(v);
                case TreeEffect.Speed: return "공격 속도 +" + Percent(v);
                case TreeEffect.CritChance: return "치명타 확률 +" + Percent(v);
                case TreeEffect.CritDamage: return "치명타 피해 +" + Percent(v);
                case TreeEffect.KillGold: return "처치 골드 +" + Percent(v);
                case TreeEffect.BossMycelite: return "보스 균사석 +" + Percent(v);
                case TreeEffect.Harvest: return "버섯 획득량 +" + Percent(v);
                case TreeEffect.BagGold: return "보따리 골드 +" + Percent(v);
                case TreeEffect.BagMycelite: return "보따리 균사석 +" + Percent(v);
                case TreeEffect.BagHours: return "보따리 보관 +" + v.ToString("0.#") + "시간";
                case TreeEffect.CraftSpeed: return "제작 속도 +" + Percent(v);
            }
            return effect + " " + v;
        }
    }
}
