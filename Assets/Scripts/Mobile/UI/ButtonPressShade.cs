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

            void Update()
            {
                wait -= Time.unscaledDeltaTime;
                if (wait > 0f) return;
                wait = 0.5f;
                foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (!b.TryGetComponent<ButtonPressShade>(out _)) b.gameObject.AddComponent<ButtonPressShade>();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var go = new GameObject("ButtonPressShade.Scanner") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<Scanner>();
        }
    }
}
