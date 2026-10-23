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

    // Skill.xlsx — 3001~3999
    public class SkillData : TableRow
    {
        public string ability;     // 능력 종류 키. 효과 코드가 이 키로 찾는다
        public float abilityValue; // 능력 수치 (배율·증가량 등, 해석은 ability마다)
        public long unlockPrice;   // 해금 가격
        public Currency currency;
        public int[] requires;     // 먼저 해금해야 하는 스킬 index들 (스킬 트리 선행 조건)
    }

    // Boss.xlsx — 4001~4999
    public class BossData : TableRow
    {
        public float hpRate;       // 체력 배율. 최종 체력 = 스테이지 체력 계수 × hpRate
        public string ability;     // 능력 종류 키
        public float abilityValue;
        public long reward;        // 처치 보상
        public string prefab;
    }
}
