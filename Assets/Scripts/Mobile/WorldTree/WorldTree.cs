using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 세계수 효과 종류 (가호·꼬마 특기·장식). 비율은 0.05 = 5%. 표의 TreeEffect 열 이름과 같아야 한다
    public enum TreeEffect { Attack, Hp, Speed, CritChance, CritDamage, KillGold, BossMycelite, Harvest, CraftSpeed }

    // 꾸미기 자리 (탭): 뿌리·줄기·가지·수관
    public enum TreeZone { Root, Trunk, Branch, Crown }

    // 세계수: 성장(나이테·공물·가호)·꼬마(진화·부탁)·공방·꾸미기. 세계수 경험치 퀘스트 보상은 메인 퀘스트(DailyQuests treeExp)에서 온다. 수치는 MainTable.xlsx 세계수 시트들에서 온다.
    // 저장은 PlayerPrefs("WorldTree.Save") JSON 하나. 자주 바뀌는 값(창고 버섯은 처치마다)은 메모리만 고치고
    // SaveScheduler가 저장할 때 JSON을 쓴다. 진화·제작처럼 드문 쓰기는 바로 저장한다.
    // PC MoreMustMush의 Game.Farm*.cs를 모바일 방치형에 맞게 옮겼다 (창고 버섯 = 방치 전투 처치, 꼬마 = 뽑기 꼬마. 수확 보따리는 앱 밖 보상과 겹쳐 11-03에 뺐다)
    public static partial class WorldTree
    {
        const string SaveKey = "WorldTree.Save";

        [Serializable] public class StoreEntry { public int enemy; public long count; }
        [Serializable] public class KidState { public string id; public int star; }
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
            // 11-10까지의 세계수 의뢰(questDay·questWeek·questDaily·questWeekly)는 메인 퀘스트로 옮겼다. 옛 세이브 JSON에 남은 그 필드는 JsonUtility가 무시한다
            public int enemyRev;        // 적 ID 개편 차수 (1 = 11-05 식용 10종 → 5등급)
        }

        const int EnemyRev = 1;

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
            bonusDirty = true;
            enemyBySprite = null;
            enemySprites.Clear();
            SaveScheduler.BeforeFlush -= WritePending;
            SaveScheduler.BeforeFlush += WritePending;
        }

        static void Load()
        {
            string json = PlayerPrefs.GetString(SaveKey, "");
            data = Parse(json, out bool broken);
            if (data == null)
            {
                // 비었거나 깨졌다 → 기기 안 백업 파일에서 되살린다. 깨진 원문은 따로 남긴다 (조사용)
                if (broken) LocalBackup.Keep(BackupName + ".corrupt", json);
                if (LocalBackup.TryRead(BackupName, out var backup) && (data = Parse(backup, out _)) != null)
                {
                    Debug.LogWarning("[WorldTree] 저장 데이터가 " + (broken ? "깨져" : "없어") + " 백업 파일에서 되살렸다");
                    pending = true;     // 다음 저장 때 PlayerPrefs에도 다시 쓴다
                }
                else if (broken) Debug.LogError("[WorldTree] 저장 데이터를 읽지 못했고 백업도 없어 새로 시작한다");
            }
            if (data == null) data = new SaveData { enemyRev = EnemyRev };
            if (data.enemyRev < EnemyRev) MigrateEnemies();
            data.stage = Mathf.Clamp(data.stage, 1, MaxStage);
            data.ring = Mathf.Clamp(data.ring, 1, RingMax);
            TrimPlacedToSlots();
            bonusDirty = true;
        }

        const string BackupName = "worldtree";   // LocalBackup 파일 이름

        // 빈 문자열이면 null (broken = false), 읽지 못하면 null (broken = true)
        static SaveData Parse(string json, out bool broken)
        {
            broken = false;
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var d = JsonUtility.FromJson<SaveData>(json);
                if (d != null) return d;
            }
            catch (Exception e) { Debug.LogWarning("[WorldTree] 저장 JSON 읽기 실패: " + e.Message); }
            broken = true;
            return null;
        }

        // 11-05 전 세이브: 식용 10종(1001~1010)을 두 종씩 한 등급(1001~1005)으로 합친다 — 창고 개수는 더하고, 부탁·종류 퀘스트 버섯은 바꾼다
        static void MigrateEnemies()
        {
            int Map(int id) => id >= 1001 && id <= 1010 ? 1001 + (id - 1001) / 2 : id;
            var merged = new List<StoreEntry>();
            foreach (var s in data.store)
            {
                int id = Map(s.enemy);
                var have = merged.Find(m => m.enemy == id);
                if (have != null) have.count += s.count;
                else merged.Add(new StoreEntry { enemy = id, count = s.count });
            }
            data.store = merged;
            foreach (var r in data.requests) r.enemy = Map(r.enemy);
            data.enemyRev = EnemyRev;
            pending = true;
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

        // 버튼 처리 뒤: 세이브 값을 바로 PlayerPrefs에 넣고(지갑도 바로 넣으므로 둘이 같이 저장된다) 파일 쓰기는 SaveScheduler에 맡긴다.
        // 예전엔 누를 때마다 PlayerPrefs.Save + 백업 파일 쓰기를 해서 나이테 성장처럼 자주 누르는 버튼에서 끊겼다.
        // 앱이 뒤로 가거나 끝날 때는 SaveScheduler가 바로 저장한다
        static void SaveNow()
        {
            pending = true;
            WritePrefs();
            SaveScheduler.MarkDirty();
        }

        static bool backupPending;      // 백업 파일은 SaveScheduler가 저장할 때만 쓴다

        static void WritePrefs()
        {
            if (!pending || data == null) return;
            pending = false;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            backupPending = true;
        }

        // SaveScheduler.BeforeFlush: 밀린 세이브를 PlayerPrefs에, 바뀐 게 있으면 백업 파일도
        static void WritePending()
        {
            WritePrefs();
            if (!backupPending || data == null) return;
            backupPending = false;
            LocalBackup.Write(BackupName, PlayerPrefs.GetString(SaveKey, ""));   // PlayerPrefs가 깨지거나 사라질 때 되살릴 사본
        }

        // 값이 바뀐 뒤: 효과 다시 계산·저장 예약·화면 갱신
        static void Notify(bool saveNow = false)
        {
            bonusDirty = true;
            if (saveNow) SaveNow(); else MarkDirty();
            Changed?.Invoke();
        }

        // ── 시간 (KST 날짜·주) ──
        static long NowTicks => GameClock.NowTicks;   // 시계를 되돌려도 제작·쿨타임이 뒤로 가지 않게
        static string Today => DungeonTickets.Today;

        // 개수 글자 (창고 마리 수 등): 1,500처럼 쉼표 (Wallet.Format은 1.5a 같은 재화 단위)
        public static string Count(long v) => v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);

        // 이번 주(월요일 시작) 첫날
        static string ThisWeek => DungeonTickets.ThisWeek;

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

        // 효과 이름은 표 키 effect.<이름> ("공격력 +{0}")
        public static string EffectText(TreeEffect effect, double v) =>
            Loc.F("effect." + effect.ToString().ToLowerInvariant(), Percent(v));
    }
}
