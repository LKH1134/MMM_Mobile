using UnityEngine;

namespace MoreMush.Mobile
{
    // 수확기(낫). 한 번 던지면 감속 없이 같은 속도로 날아가며 상하좌우 벽·바·버섯에 부딪혀 반사각으로 튄다.
    // 움직임·충돌 계산은 HarvestField가 한꺼번에 한다 (좌표는 필드 로컬, 단위는 캔버스 픽셀).
    // 풀에서 꺼내 쓰므로 분신도 같은 프리팹(혹은 다른 수확기 프리팹)을 Spawn해서 Launch하면 된다
    [RequireComponent(typeof(RectTransform))]
    public class Harvester : MonoBehaviour, IPoolable
    {
        [SerializeField] float radius = 45f;          // 충돌 반지름
        [SerializeField] int damage = 1;              // 한 번 부딪힐 때 버섯 체력을 깎는 양
        [SerializeField] float spinSpeed = 900f;      // 날아가는 동안 도는 속도(도/초)
        [SerializeField] RectTransform visual;        // 돌릴 그림. 비우면 회전 없음

        RectTransform rect;

        public float Radius => radius;
        public int Damage => damage;
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public bool IsMoving => Velocity.sqrMagnitude > 0f;
        public int Combo { get; set; }                // 마지막으로 바에 닿은 뒤 캔 버섯 수
        internal PrefabPool<Harvester> Pool { get; set; }

        void Awake() => rect = (RectTransform)transform;

        public void OnSpawned()
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            Velocity = Vector2.zero;
            Combo = 0;
            if (visual != null) visual.localRotation = Quaternion.identity;
        }

        public void OnDespawned() => Velocity = Vector2.zero;

        public void Place(Vector2 position)
        {
            Position = position;
            Sync(0f);
        }

        public void Launch(Vector2 velocity)
        {
            Velocity = velocity;
            Combo = 0;
        }

        public void Stop() => Velocity = Vector2.zero;

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
