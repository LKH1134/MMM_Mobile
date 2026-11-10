using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // [UI 개편 2026-11-05] 누르는 동안 버튼 그림을 회색으로 어둡게 (ColorTint 버튼의 기본 눌림 0.78과 같은 값).
    // 그림 바꾸기(SpriteSwap: 비활성 회색 그림)·없음(None) 버튼은 눌림 표시가 없어서 붙인다. ColorTint 버튼은 원래 눌림이 있어 아무것도 안 한다.
    // 색은 Image.color가 아니라 CanvasRenderer 곱하기 색으로만 바꿔 리소스 그림 색은 그대로다.
    // 버튼마다 손으로 붙일 필요 없음: 아래 Scanner가 씬·팝업의 모든 버튼에 자동으로 붙인다 (프리팹에 직접 붙여도 된다)
    [DisallowMultipleComponent]
    public class ButtonPressShade : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler
    {
        public static readonly Color Pressed = new Color(0.78f, 0.78f, 0.78f, 1f);
        const float FadeTime = 0.05f;

        Button button;
        bool down, inside;

        void Awake() => button = GetComponent<Button>();

        bool Active => button != null && button.transition != Selectable.Transition.ColorTint && button.targetGraphic != null;

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            down = inside = true;
            Shade();
        }

        public void OnPointerUp(PointerEventData e) { down = false; Shade(); }
        public void OnPointerExit(PointerEventData e) { inside = false; Shade(); }
        public void OnPointerEnter(PointerEventData e) { inside = true; Shade(); }

        void OnDisable()
        {
            down = false;
            if (Active) button.targetGraphic.CrossFadeColor(Color.white, 0f, true, true);
        }

        void Shade()
        {
            if (!Active) return;
            bool on = down && inside && button.IsInteractable();
            button.targetGraphic.CrossFadeColor(on ? Pressed : Color.white, FadeTime, true, true);
        }

        // ── 모든 버튼에 자동으로 붙이기: 0.5초마다 새로 생긴 버튼을 찾는다 (팝업·코드로 만든 버튼 포함) ──
        class Scanner : MonoBehaviour
        {
            float wait;
            Selectable[] buffer = new Selectable[64];

            // 씬 전체를 뒤지지 않고 Unity가 들고 있는 켜진 Selectable 목록만 본다 (FindObjectsByType는 모바일에서 무겁고 배열을 새로 만든다)
            void Update()
            {
                wait -= Time.unscaledDeltaTime;
                if (wait > 0f) return;
                wait = 0.5f;
                int n = Selectable.allSelectableCount;
                if (buffer.Length < n) buffer = new Selectable[Mathf.NextPowerOfTwo(n)];
                n = Selectable.AllSelectablesNoAlloc(buffer);
                for (int i = 0; i < n; i++)
                {
                    if (buffer[i] is Button b && !b.TryGetComponent<ButtonPressShade>(out _)) b.gameObject.AddComponent<ButtonPressShade>();
                    buffer[i] = null;       // 꺼진 버튼을 잡아 두지 않게
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            // HideAndDontSave(DontSave 포함)는 플레이를 꺼도 에디터에 남아, 다시 컴파일할 때마다 "referenced script is missing"을 낸다
            // (Scanner가 중첩 클래스라 스크립트 파일과 이어지지 않음). 숨기기만 하면 플레이 종료 때 Unity가 지운다 (2026-11-06)
            var go = new GameObject("ButtonPressShade.Scanner") { hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable };
            DontDestroyOnLoad(go);
            go.AddComponent<Scanner>();
        }
    }
}
