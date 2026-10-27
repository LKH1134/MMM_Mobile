namespace MoreMush.Mobile
{
    // 데이터 표의 행 정의. 필드 이름 = 엑셀 1행의 열 이름. 필드를 바꾸면 Assets/Data/Tables의 엑셀 열도 같이 바꾼다.
    // 필드는 리플렉션으로 채우므로 IL2CPP 코드 제거에 지워지지 않게 같은 폴더 link.xml에 행 클래스를 적어 둔다 (새 표를 만들면 거기도 추가)

    public enum Currency { Gold, Mycelite }

    // Mushroom.xlsx — 1001~1999
    public class MushroomData : TableRow
    {
        public int dexNo;          // 도감 번호 (도감 기능에서 정렬·표시)
        public int hp;             // 체력. 1이면 한 번에 캐지는 단일 개체
        public long reward;        // 캤을 때 오르는 돈
        public string prefab;      // 필드에 놓을 프리팹 경로 (Resources 기준)
    }

    // Harvester.xlsx — 2001~2999
    public class HarvesterData : TableRow
    {
        public long unlockPrice;   // 해금 가격
        public Currency currency;  // 해금 가격의 재화
        public int damage;         // 한 번 부딪힐 때 버섯 체력을 깎는 양
        public float speed;        // 날아가는 속도 (픽셀/초)
        public string prefab;
    }

    public enum SkillBranch { Start, Edible, Medicinal, Toxic }               // 시작 / 식용 / 약용 / 독성 가지
    public enum SkillTag { SlideBar, Gain, Stage, Skill, Harvester }          // 【슬라이드 바】【획득률】【스테이지】【스킬】【수확기】

    // Skill.xlsx — 3001~3999. 한 행 = 스킬 트리 노드 하나 (같은 스킬의 Lv.4, Lv.5가 따로 놓이면 행도 따로)
    public class SkillData : TableRow
    {
        public SkillBranch branch; // 가지
        public int rarity;         // 레어도 (0 = 시작, 1~19). 유저에게는 안 보이는 관리용, 클수록 트리 바깥쪽
        public SkillTag tag;
        public string group;       // 같은 스킬 계열 키 (효율적 사냥 Lv.1~3 / Lv.4 / Lv.5 노드가 같은 값)
        public int startLevel;     // 이 노드의 첫 레벨이 계열에서 몇 레벨인지 (Lv.4 노드면 4). 비우면 1
        public int lane;           // 스킬 트리의 세로 줄 (0~4, 왼쪽부터)
        public int row;            // 스킬 트리의 가로 줄 (0 = 맨 아래 시작 노드, 위로 갈수록 큼)
        public string ability;     // 능력 종류 키 (SkillAbility). 효과 코드가 이 키로 찾는다
        public float abilityBase;  // 1레벨 이상일 때 한 번 더하는 값 (레벨마다 오르는 폭이 첫 레벨과 다를 때)
        public float abilityValue; // 레벨당 능력 수치 (배율·증가량 등, 해석은 ability마다). 비율은 0.1 = 10%
        public float param;        // 레벨과 무관한 고정 수치 (번개 공격력 30%, 버프 지속 3초 등, 해석은 ability마다)
        public int maxLevel;       // 최대 레벨. 비우면 1
        public long unlockPrice;   // 첫 레벨 가격
        public Currency currency;  // 첫 레벨 재화
        public long levelPrice;    // 2레벨부터의 첫 가격. 비우면 unlockPrice × priceGrowth^(레벨-1)을 같은 재화로
        public Currency levelCurrency; // 2레벨부터의 재화 (levelPrice가 있을 때만 읽는다)
        public float priceGrowth;  // 레벨마다 가격 배율 (비우면 1)
        public int link;          // 연결 노드 index (트리에 선으로 그린다). 1레벨 이상 배워야 열린다. 0 = 없음 (시작 노드)
        public int[] requires;     // 이전 레벨 노드 index들 (같은 스킬의 앞 노드). 최대 레벨까지 배워야 열린다
    }

    // Stage.xlsx — 5001~5999. 한 행 = 스테이지 하나 (stage 1부터 차례로). 보스 스테이지는 boss에 보스 index를 넣는다
    public class StageData : TableRow
    {
        public int stage;          // 스테이지 번호 (1부터)
        public string theme;       // 지역 (숲·달빛 밤·들판). 배경 등 연출용
        public float hpRate;       // 체력 계수. 버섯·보스 체력에 곱한다. 뒤로 갈수록 크다
        public float rewardRate;   // 점수·수확량 배율
        public int boss;           // 보스 index (Boss.xlsx). 0이면 일반 스테이지
        public StageGoal goalType; // 일반 스테이지 클리어 조건. None이면 판을 마치기만 하면 넘어간다 (보스 스테이지는 보스 처치)
        public int goal;           // 목표 수치 (Harvest: 버섯 n개). 같은 스테이지를 연속으로 못 넘으면 RoundBase.goalRelief만큼 줄여 준다
        public float mushHpMul;    // 일반 버섯 체력 배율 (보스엔 안 곱함). 비우면 1
        public int gem;            // 첫 클리어 때 주는 균사석 (★ 스테이지). 보스 스테이지는 Boss.xlsx gem을 더한다
    }

    public enum StageGoal { None, Harvest }   // 없음 / 버섯 n개 수확

    public enum BossGimmick { None, Heal, Purge, Split, Move }   // 없음 / 체력 회복 / 임시 수확기 제거 / 분신 / 좌우 이동

    // Boss.xlsx — 6001~6999
    public class BossData : TableRow
    {
        public float hpRate;       // 체력 배수. 보스 체력 = 기본 보스 체력 × hpRate × 스테이지 hpRate
        public BossGimmick gimmick;
        public float gimmickValue; // Heal: 회복 비율(최대 체력 대비) / Split: 분신 체력 (분열 순간 본체 체력 대비) / Move: 속도(px/초)
        public float gimmickInterval; // Heal·Purge: 간격(초)
        public string color;       // 임시 구분 색 (#rrggbb). 보스 그림이 생기면 prefab으로 바꾼다
        public long reward;        // 처치 점수 (스테이지 rewardRate를 곱한다)
        public int drop;           // 처치 때 얻는 레전더리 버섯 수
        public int gem;            // 첫 처치 때 얻는 균사석 (다시 잡으면 없음, StageProgress.Cleared)
        public float failGold;     // 못 잡았을 때 그 판 골드 배율 (비우면 1)
        public string prefab;
    }
}
