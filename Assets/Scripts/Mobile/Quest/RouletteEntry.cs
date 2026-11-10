using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 홈 왼쪽 아이콘 열의 룰렛 버튼 (프리팹 MainHud › SideButtons › roulette). 누르면 룰렛 화면을 열고,
    // 지금 돌릴 수 있으면 버튼에 점(Dot)을 켠다. 룰렛 화면은 처음 누를 때 맨 위 캔버스 아래에 만든다 (씬을 건드리지 않게)
    public class RouletteEntry : MonoBehaviour
    {
        const float CheckEvery = 1f;

        [SerializeField] Button button;
        [SerializeField] GameObject dot;                       // 지금 돌릴 수 있으면 켠다
        [SerializeField] RouletteScreenView screenPrefab;      // Prefabs/Mobile/UI/Main/RouletteScreen

        RouletteScreenView screen;
        float wait;

        void Awake()
        {
            if (button == null) TryGetComponent(out button);
            if (button != null) button.onClick.AddListener(Open);
        }

        void OnEnable()
        {
            Roulette.Changed += RefreshDot;
            wait = CheckEvery;
            RefreshDot();
        }

        void OnDisable() => Roulette.Changed -= RefreshDot;

        void Update()
        {
            wait -= Time.unscaledDeltaTime;
            if (wait > 0f) return;
            wait = CheckEvery;
            RefreshDot();      // 대기 시간이 끝나거나 한국 00시가 지나면 점이 켜진다
        }

        void RefreshDot()
        {
            if (dot == null) return;
            bool on = Roulette.CanSpin;
            if (dot.activeSelf != on) dot.SetActive(on);
        }

        void Open()
        {
            if (screenPrefab == null) { Debug.LogError("[RouletteEntry] screenPrefab이 비어 있다 (MainHud 프리팹에서 연결한다)"); return; }
            if (screen == null)
            {
                var canvas = GetComponentInParent<Canvas>().rootCanvas.transform;
                screen = Instantiate(screenPrefab, canvas, false);
                screen.gameObject.SetActive(false);
            }
            screen.Open();
        }
    }
}
