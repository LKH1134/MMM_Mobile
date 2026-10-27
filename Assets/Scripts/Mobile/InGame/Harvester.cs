using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 수확기 하나에 걸리는 버프 (바에 닿거나 장치를 지날 때 SkillEffects·FieldDevices가 건다).
    // Slow·Weak는 포자 디버프 (군락에 부딪힐 때 확률로 걸린다)
    public enum HarvesterBuff { Power, Haste, Pierce, Homing, Heal, Stump, Slow, Weak }

    // 수확기(낫). 한 번 던지면 감속 없이 같은 속도로 날아가며 상하좌우 벽·바·버섯에 부딪혀 반사각으로 튄다.
    // 움직임·충돌 계산은 HarvestField가 한꺼번에 한다 (좌표는 필드 로컬, 단위는 캔버스 픽셀).
    // 풀에서 꺼내 쓰므로 분신도 같은 프리팹(혹은 다른 수확기 프리팹)을 Spawn해서 Launch하면 된다.
    // 분열체처럼 잠깐만 있는 수확기는 Temporary + Life로 표시하고, 시간이 다 되면 필드가 풀로 돌려놓는다
    [RequireComponent(typeof(RectTransform))]
    public class Harvester : MonoBehaviour, IPoolable
    {
        static readonly int BuffCount = System.Enum.GetValues(typeof(HarvesterBuff)).Length;

        [SerializeField] float radius = 45f;          // 충돌 반지름
        [SerializeField] int damage = 1;              // 기본 공격력. 한 번 부딪힐 때 버섯 체력을 깎는 양
        [SerializeField] float spinSpeed = 900f;      // 날아가는 동안 도는 속도(도/초)
        [SerializeField] RectTransform visual;        // 돌릴 그림. 비우면 회전 없음

        RectTransform rect;
        Graphic tintTarget;
        float size = 1f;
        readonly float[] buffs = new float[BuffCount];
        readonly List<Mushroom> touching = new List<Mushroom>();   // 지금 겹쳐 있는 버섯 (관통 중 같은 버섯을 매 프레임 때리지 않게)

        public float Radius => radius * size;
        public float BaseRadius => radius;
        // 크기 배율 (초토화·분열체). 충돌 반지름과 그림을 함께 키운다
        public float Size
        {
            get => size;
            set
            {
                size = value;
                if (rect == null) rect = (RectTransform)transform;
                rect.localScale = new Vector3(value, value, 1f);
            }
        }
        public int BonusDamage { get; set; }          // 스킬 등으로 더해지는 공격력. 꺼낼 때 HarvestField가 넣는다
        public int PowerDamage { get; set; }          // 공격력 버프(사냥 준비)가 걸려 있을 때 더할 값
        public float DamageMul { get; set; } = 1f;    // 분열체 등 공격력 배율
        public float StumpMul { get; set; } = 1f;     // 그루터기 버프가 걸려 있을 때 곱할 값
        public float NextHitMul { get; set; } = 1f;   // 다음 한 번만 곱할 피해 (이독제독). 맞히면 1로 돌아간다
        public float WeakMul { get; set; } = 1f;      // 약화 디버프 중 공격력 배율
        public bool Debuffed => Has(HarvesterBuff.Slow) || Has(HarvesterBuff.Weak);
        public int Damage
        {
            get
            {
                float d = (damage + BonusDamage + (Has(HarvesterBuff.Power) ? PowerDamage : 0)) * DamageMul;
                if (Has(HarvesterBuff.Stump)) d *= StumpMul;
                if (Has(HarvesterBuff.Weak)) d *= WeakMul;
                return Mathf.Max(1, Mathf.RoundToInt(d));
            }
        }
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public bool IsMoving => Velocity.sqrMagnitude > 0f;
        public bool Temporary { get; set; }           // 분열체 등 잠깐 있는 수확기 (바 버프를 받지 않는다)
        public float Life { get; set; }               // Temporary일 때 남은 시간
        public List<Mushroom> Touching => touching;
        internal PrefabPool<Harvester> Pool { get; set; }

        void Awake()
        {
            rect = (RectTransform)transform;
            if (visual != null) tintTarget = visual.GetComponent<Graphic>();
        }

        public void OnSpawned()
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            Velocity = Vector2.zero;
            Size = 1f;
            BonusDamage = PowerDamage = 0;
            DamageMul = StumpMul = NextHitMul = WeakMul = 1f;
            Temporary = false;
            Life = 0f;
            System.Array.Clear(buffs, 0, buffs.Length);
            touching.Clear();
            if (visual != null) visual.localRotation = Quaternion.identity;
            Tint(Color.white);
        }

        public void OnDespawned()
        {
            Velocity = Vector2.zero;
            touching.Clear();
        }

        public void Place(Vector2 position)
        {
            Position = position;
            Sync(0f);
        }

        public void Launch(Vector2 velocity)
        {
            Velocity = velocity;
        }

        public void Stop() => Velocity = Vector2.zero;

        // ── 버프 ──
        public bool Has(HarvesterBuff buff) => buffs[(int)buff] > 0f;
        public float Remaining(HarvesterBuff buff) => buffs[(int)buff];
        // 다시 걸면 시간을 새로 채운다 (남은 시간이 더 길면 그대로)
        public void SetBuff(HarvesterBuff buff, float seconds)
        {
            if (seconds <= 0f) return;
            buffs[(int)buff] = Mathf.Max(buffs[(int)buff], seconds);
        }
        public void ClearBuff(HarvesterBuff buff) => buffs[(int)buff] = 0f;

        public void TickBuffs(float dt)
        {
            for (int i = 0; i < buffs.Length; i++)
                if (buffs[i] > 0f) buffs[i] = Mathf.Max(0f, buffs[i] - dt);
        }

        // 임시: 걸린 버프를 그림 색으로 보여 준다 (버프 연출이 생기면 바꾼다)
        public void Tint(Color color)
        {
            if (tintTarget != null) tintTarget.color = color;
        }

        // 계산된 위치를 화면에 옮긴다. 도는 방향은 가로 속도를 따른다
        public void Sync(float dt)
        {
            rect.anchoredPosition = Position;
            if (visual == null || dt <= 0f || !IsMoving) return;
            float dir = Velocity.x >= 0f ? -1f : 1f;
            visual.Rotate(0f, 0f, dir * spinSpeed * dt);
        }
    }
}
