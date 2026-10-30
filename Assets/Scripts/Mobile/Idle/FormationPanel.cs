using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 편성 화면 (먼작귀 포켓식): 캐릭터 탭 → 동료 칸(Middle) → 칸을 누르면 꼬마 고르기 팝업(Char).
    // 고른 꼬마를 누른 칸에 놓고, 이미 다른 칸에 있던 꼬마면 두 칸을 맞바꾼다(Formation.Place).
    // 팝업 첫 칸은 X 표시 "편성 해제" 버튼이고, 그 칸에 이미 있는 꼬마를 다시 골라도 해제된다. 못 얻은 꼬마는 "미보유"로 막는다.
    // 칸 버튼은 씬에 있는 것을 쓰고(자식 Image = 그림, w = 이름, w (1) = 레벨), 팝업 버튼에는 그림·배치 표시를 코드로 붙인다
    public class FormationPanel : MonoBehaviour
    {
        [SerializeField] Button characterTab;       // Down/Button (1) "캐릭터"
        [SerializeField] Button autoButton;         // "자동 편성" — 비우면 GGoma/Button을 찾아 쓴다
        [SerializeField] GameObject middle;         // 동료 칸 묶음
        [SerializeField] Button[] slotButtons;      // Middle의 칸 버튼 (순서 = Formation 칸 = 전투 Ggoma1~5)
        [SerializeField] GameObject charPopup;      // 꼬마 고르기 팝업
        [SerializeField] RectTransform charGrid;    // 팝업 안 꼬마 버튼들의 부모
        [SerializeField] Button closeButton;
        [SerializeField] TMP_FontAsset font;
        [SerializeField] string clearIcon = "Art/Dream/UI/btn_close";   // 편성 해제 버튼 그림 (Resources 경로)
        [SerializeField] int popupSortingOrder = 10; // 부모 캔버스보다 이만큼 위에 그린다 (전투 화면·강화 목록 위)

        class Pick { public Button button; public Image bg, icon; public TMP_Text name, badge; public string id; }

        readonly List<Pick> picks = new List<Pick>();
        int targetSlot = -1;

        void Awake()
        {
            if (characterTab != null) characterTab.onClick.AddListener(ShowMiddle);
            if (autoButton == null && characterTab != null && characterTab.transform.parent != null)
            {
                // GGoma 프리팹 구조: GGoma/Down/Button (1) = 캐릭터 탭, GGoma/Button = 자동 편성
                var ggoma = characterTab.transform.parent.parent;
                var found = ggoma != null ? ggoma.Find("Button") : null;
                if (found != null) autoButton = found.GetComponent<Button>();
            }
            if (autoButton != null) autoButton.onClick.AddListener(Formation.AutoArrange);
            else Debug.LogWarning("[FormationPanel] 자동 편성 버튼을 찾지 못함");
            for (int i = 0; i < slotButtons.Length; i++)
            {
                int slot = i;
                slotButtons[i].onClick.AddListener(() => OpenPicker(slot));
            }
            if (closeButton != null) closeButton.onClick.AddListener(ClosePicker);
            BuildPicks();
            ClosePicker();      // 씬에서 켜 두었어도 시작할 때는 닫는다
        }

        void OnEnable()
        {
            Formation.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => Formation.Changed -= Refresh;

        void ShowMiddle()
        {
            if (middle != null) middle.SetActive(true);
            ClosePicker();
        }

        void OpenPicker(int slot)
        {
            targetSlot = slot;
            PopupGate.Opened(this, ClosePicker);
            charPopup.SetActive(true);
            // 팝업은 Equit 안에 있어 뒤에 오는 강화 목록·전투 화면에 가려진다 → 자기 캔버스로 위에 올린다
            if (!charPopup.TryGetComponent<Canvas>(out var c))
            {
                c = charPopup.AddComponent<Canvas>();
                charPopup.AddComponent<GraphicRaycaster>();
            }
            c.overrideSorting = true;
            // 부모 캔버스 순서 기준 (Main 캔버스 순서가 10000이라 고정값이면 뒤로 숨는다)
            var parent = charPopup.transform.parent != null ? charPopup.transform.parent.GetComponentInParent<Canvas>() : null;
            c.sortingOrder = (parent != null ? parent.rootCanvas.sortingOrder : 0) + popupSortingOrder;
            Refresh();
        }

        void ClosePicker()
        {
            targetSlot = -1;
            PopupGate.Closed(this);
            if (charPopup != null) charPopup.SetActive(false);
        }

        void Choose(string id)
        {
            if (targetSlot < 0) return;
            if (string.IsNullOrEmpty(id)) Formation.Clear(targetSlot);
            else Formation.Place(targetSlot, id);
            ClosePicker();
        }

        // 팝업의 버튼을 화면 순서(위→아래, 왼→오른)로 쓴다: 첫 칸 = 편성 해제, 다음부터 꼬마 수만큼, 나머지는 숨긴다
        void BuildPicks()
        {
            var buttons = new List<Button>(charGrid.GetComponentsInChildren<Button>(true));
            buttons.RemoveAll(b => b.transform.parent != charGrid);
            buttons.Sort((a, b) =>
            {
                var pa = ((RectTransform)a.transform).anchoredPosition;
                var pb = ((RectTransform)b.transform).anchoredPosition;
                return Mathf.Abs(pa.y - pb.y) > 1f ? pb.y.CompareTo(pa.y) : pa.x.CompareTo(pb.x);
            });

            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                if (i > Formation.Ids.Length) { b.gameObject.SetActive(false); continue; }
                string id = i == 0 ? "" : Formation.Ids[i - 1];     // "" = 편성 해제
                var rt = (RectTransform)b.transform;
                var p = new Pick { button = b, bg = b.GetComponent<Image>(), id = id };

                p.icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                p.icon.transform.SetParent(rt, false);
                p.icon.transform.SetAsFirstSibling();
                p.icon.sprite = id == "" ? Resources.Load<Sprite>(clearIcon) : Formation.Icon(id);
                p.icon.preserveAspect = true;
                p.icon.raycastTarget = false;
                var irt = p.icon.rectTransform;
                irt.anchorMin = new Vector2(0f, 0.22f);
                irt.anchorMax = new Vector2(1f, 1f);
                irt.offsetMin = new Vector2(8f, 0f);
                irt.offsetMax = new Vector2(-8f, -8f);

                // 기존 글자(Text (TMP))를 이름 줄로 쓴다
                p.name = b.GetComponentInChildren<TMP_Text>(true);
                if (p.name == null) p.name = NewText("Name", rt, 30f);
                p.name.text = id == "" ? "편성 해제" : Formation.Name(id);
                p.name.fontSize = 30f;
                p.name.alignment = TextAlignmentOptions.Bottom;
                if (font != null) p.name.font = font;
                var nrt = p.name.rectTransform;
                nrt.anchorMin = new Vector2(0f, 0f);
                nrt.anchorMax = new Vector2(1f, 0.25f);
                nrt.offsetMin = new Vector2(4f, 6f);
                nrt.offsetMax = new Vector2(-4f, 0f);

                p.badge = NewText("Badge", rt, 26f);
                p.badge.color = Color.white;
                p.badge.fontStyle = FontStyles.Bold;
                p.badge.outlineWidth = 0.25f;
                p.badge.outlineColor = new Color32(40, 25, 15, 255);
                var brt = p.badge.rectTransform;
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
                brt.pivot = new Vector2(0f, 1f);
                brt.anchoredPosition = new Vector2(10f, -8f);
                brt.sizeDelta = new Vector2(160f, 36f);
                p.badge.alignment = TextAlignmentOptions.TopLeft;

                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => Choose(id));
                picks.Add(p);
            }
        }

        void Refresh()
        {
            for (int i = 0; i < slotButtons.Length && i < Formation.SlotCount; i++)
            {
                var slot = slotButtons[i].transform;
                string id = Formation.Get(i);
                bool empty = string.IsNullOrEmpty(id);
                var img = slot.Find("Image")?.GetComponent<Image>();
                if (img != null) { img.sprite = Formation.Icon(id); img.preserveAspect = true; img.color = empty ? Color.clear : Color.white; }
                var nameText = slot.Find("w")?.GetComponent<TMP_Text>();
                if (nameText != null) { nameText.text = empty ? "동료 선택" : Formation.Name(id); if (font != null) nameText.font = font; }
                var lvText = slot.Find("w (1)")?.GetComponent<TMP_Text>();
                if (lvText != null) { lvText.text = empty ? "" : "Lv.1"; if (font != null) lvText.font = font; }
            }

            string current = targetSlot >= 0 ? Formation.Get(targetSlot) : null;
            foreach (var p in picks)
            {
                if (p.id == "")
                {
                    // 편성 해제: 고르는 칸이 이미 비어 있으면 누를 일이 없다
                    bool can = !string.IsNullOrEmpty(current);
                    p.button.interactable = can;
                    p.bg.color = can ? Color.white : new Color(0.75f, 0.75f, 0.75f);
                    p.badge.text = "";
                    continue;
                }
                // 꼬마 뽑기로 아직 못 얻은 꼬마는 어둡게, 누를 수 없다
                bool owned = Formation.IsOwned(p.id);
                p.button.interactable = owned;
                if (p.icon != null) p.icon.color = owned ? Color.white : new Color(0.25f, 0.25f, 0.25f);
                if (!owned) { p.badge.text = "미보유"; p.bg.color = new Color(0.6f, 0.6f, 0.6f); continue; }
                int at = Formation.SlotOf(p.id);
                p.badge.text = at >= 0 ? $"{at + 1}번 배치" : "";
                // 지금 고르는 칸에 있는 꼬마는 노랗게, 다른 칸에 있는 꼬마는 살짝 어둡게
                p.bg.color = p.id == current ? new Color(1f, 0.9f, 0.45f) : at >= 0 ? new Color(0.85f, 0.85f, 0.85f) : Color.white;
            }
        }

        TMP_Text NewText(string name, Transform parent, float size)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            if (font != null) t.font = font;
            t.fontSize = size;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }
    }
}
