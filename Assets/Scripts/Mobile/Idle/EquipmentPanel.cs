using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 장비 화면 (Wepon·Amor·Pet 프리팹): 위 = 고른 장비 정보·장착·합성(도감 레벨업), 가운데 = 장비 칸(Equipment 순서), 아래 = 보유 효과·소환·일괄 합성.
    // 프리팹 안 오브젝트는 이름으로 찾는다 (Up / mid / Down). 칸 버튼에는 그림·장수·장착 표시를 코드로 붙인다.
    // 위쪽에 정보 칸이 없는 프리팹(Pet)은 Wepon과 같은 배치로 코드에서 만든다 (원래 있던 위쪽 오브젝트는 숨긴다)
    public class EquipmentPanel : MonoBehaviour
    {
        [SerializeField] GachaKind kind = GachaKind.Weapon;     // Weapon · Accessory(방어구) · Pet
        [SerializeField] TMP_FontAsset font;
        [SerializeField] Toast toastPrefab;         // 안내 문구 — 처음 쓸 때 캔버스 맨 위에 하나 만든다 (없으면 콘솔에만)

        class Slot { public Button button; public Image bg, icon; public TMP_Text count, badge; }

        readonly List<Slot> slots = new List<Slot>();
        Image detailIcon;
        TMP_Text detailName, detailCount, mergeBarText, equipStat, ownStat, equipLabel, ownTotal;
        RectTransform mergeBarFill;
        Button equipButton, mergeButton;
        int selected = -1;
        bool built;

        Equipment Eq => Equipment.Of(kind) ?? Equipment.Weapon;

        static readonly Color Locked = new Color(0.25f, 0.25f, 0.25f, 1f);
        static readonly Color SelectedBg = new Color(1f, 0.9f, 0.45f);
        static readonly Color EquippedBg = new Color(0.75f, 0.92f, 0.7f);

        void Awake() => Build();

        void OnEnable()
        {
            Eq.Changed += Refresh;
            Build();
            if (selected < 0) selected = Eq.Equipped >= 0 ? Eq.Equipped : HighestOwned();
            Refresh();
        }

        void OnDisable() => Eq.Changed -= Refresh;

        void Build()
        {
            if (built) return;
            built = true;

            var up = transform.Find("Up");
            if (up != null && up.Find("Text (TMP)") == null) BuildDetail(up);
            if (up != null)
            {
                detailIcon = up.Find("Image")?.GetComponent<Image>();
                if (detailIcon != null) detailIcon.preserveAspect = true;
                detailName = up.Find("Text (TMP)")?.GetComponent<TMP_Text>();
                detailCount = up.Find("Text (TMP) (1)")?.GetComponent<TMP_Text>();
                BuildMergeBar(FindChildByIndex(up, "Image", 1));
                equipStat = AddRowText(up.Find("Panel (1)"));
                ownStat = AddRowText(up.Find("Panel (2)"));
                equipButton = up.Find("Button")?.GetComponent<Button>();
                mergeButton = up.Find("Button (1)")?.GetComponent<Button>();
                if (equipButton != null)
                {
                    equipButton.onClick.AddListener(EquipSelected);
                    equipLabel = equipButton.GetComponentInChildren<TMP_Text>(true);
                }
                if (mergeButton != null) mergeButton.onClick.AddListener(MergeSelected);
            }

            var mid = transform.Find("mid");
            if (mid != null) BuildSlots(mid);

            var down = transform.Find("Down");
            if (down != null)
            {
                ownTotal = down.Find("Text (TMP)")?.GetComponent<TMP_Text>();
                down.Find("Summon")?.GetComponent<Button>()?.onClick.AddListener(OpenSummon);
                down.Find("All")?.GetComponent<Button>()?.onClick.AddListener(MergeAll);
            }
        }

        // 위쪽 정보 칸이 없는 프리팹(Pet): 원래 오브젝트를 숨기고 Wepon과 같은 배치(Up 가운데 기준)로 만든다.
        // 이름을 Wepon과 같게 붙여 Build가 그대로 찾는다
        void BuildDetail(Transform up)
        {
            foreach (Transform c in up)
            {
                c.gameObject.SetActive(false);
                c.name += " (숨김)";      // 아래에서 같은 이름으로 찾을 때 걸리지 않게
            }

            var icon = NewImage("Image", up);
            icon.color = Color.white;
            Center(icon.rectTransform, new Vector2(-450f, 103.4f), new Vector2(122.4f, 101.9f));

            var name = NewText("Text (TMP)", up, 36f);
            name.color = Brown;
            name.alignment = TextAlignmentOptions.MidlineLeft;
            Center(name.rectTransform, new Vector2(-281.6f, 126f), new Vector2(160.5f, 50f));
            var count = NewText("Text (TMP) (1)", up, 36f);
            count.color = Brown;
            count.alignment = TextAlignmentOptions.MidlineLeft;
            Center(count.rectTransform, new Vector2(-281.6f, 85f), new Vector2(160.5f, 50f));

            var bar = NewImage("Image", up);
            bar.preserveAspect = false;
            Center(bar.rectTransform, new Vector2(175.3f, 87.7f), new Vector2(742.7f, 30.6f));

            foreach (var (rowName, y) in new[] { ("Panel (1)", 18.4f), ("Panel (2)", -42.1f) })
            {
                var row = NewImage(rowName, up);
                row.preserveAspect = false;
                Skin(row, CreamSprite, 2.5f);
                var rt = row.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(79f, y + 154.7f);
                rt.offsetMax = new Vector2(-27.2f, y - 154.7f);
            }

            foreach (var (buttonName, x, label) in new[] { ("Button", -149f, "장착"), ("Button (1)", 141f, "합성") })
            {
                var img = NewImage(buttonName, up);
                img.preserveAspect = false;
                img.raycastTarget = true;
                Center(img.rectTransform, new Vector2(x, -121.2f), new Vector2(234.8f, 89.3f));
                img.gameObject.AddComponent<Button>();
                Skin(img, GoSprite, 1.4f);
                var t = NewText("Text (TMP)", img.transform, 30f);
                t.text = label;
                t.color = Color.white;
                var trt = t.rectTransform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = trt.offsetMax = Vector2.zero;
            }
        }

        // 프리팹 칸과 같은 게임 UI 그림 (Resources 경로)
        const string CreamSprite = "Art/Dream/UI/Window/btn_cream";
        const string GoSprite = "Art/Dream/UI/btn_go";
        static readonly Color Brown = new Color(0.36f, 0.24f, 0.14f);

        static void Skin(Image img, string sprite, float pixelsPerUnit)
        {
            var sp = Resources.Load<Sprite>(sprite);
            if (sp == null) return;
            img.sprite = sp;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = pixelsPerUnit;
            img.color = Color.white;
        }

        static void Center(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        // 같은 이름 자식이 여러 개일 때 n번째 (Up 안 "Image"는 아이콘·막대 두 개)
        static Transform FindChildByIndex(Transform parent, string name, int index)
        {
            int n = 0;
            foreach (Transform c in parent)
                if (c.name == name && n++ == index) return c;
            return null;
        }

        // 칸 버튼을 화면 순서(위→아래, 왼→오른)로 무기에 맞춘다. 무기보다 많은 칸은 숨긴다
        void BuildSlots(Transform mid)
        {
            var buttons = new List<Button>();
            foreach (Transform c in mid)
                if (c.TryGetComponent<Button>(out var b)) buttons.Add(b);
            buttons.Sort((a, b) =>
            {
                var pa = ((RectTransform)a.transform).anchoredPosition;
                var pb = ((RectTransform)b.transform).anchoredPosition;
                return Mathf.Abs(pa.y - pb.y) > 1f ? pb.y.CompareTo(pa.y) : pa.x.CompareTo(pb.x);
            });

            for (int i = 0; i < buttons.Count; i++)
            {
                var b = buttons[i];
                if (i >= Eq.Count) { b.gameObject.SetActive(false); continue; }
                int index = i;
                var rt = (RectTransform)b.transform;
                var s = new Slot { button = b, bg = b.GetComponent<Image>() };

                s.icon = NewImage("Icon", rt);
                s.icon.transform.SetAsFirstSibling();
                s.icon.sprite = Eq.Sprite(i);
                var irt = s.icon.rectTransform;
                irt.anchorMin = new Vector2(0f, 0.25f);
                irt.anchorMax = Vector2.one;
                irt.offsetMin = new Vector2(14f, 0f);
                irt.offsetMax = new Vector2(-14f, -26f);

                // 기존 글자(Lv.1)를 장수 줄로 쓴다
                s.count = b.GetComponentInChildren<TMP_Text>(true);
                if (s.count == null) s.count = NewText("Count", rt, 26f);
                if (font != null) s.count.font = font;
                s.count.fontSize = 26f;
                s.count.alignment = TextAlignmentOptions.Bottom;
                var crt = s.count.rectTransform;
                crt.anchorMin = Vector2.zero;
                crt.anchorMax = new Vector2(1f, 0.3f);
                crt.offsetMin = new Vector2(4f, 6f);
                crt.offsetMax = new Vector2(-4f, 0f);

                s.badge = NewText("Badge", rt, 22f);
                s.badge.alignment = TextAlignmentOptions.TopLeft;
                s.badge.fontStyle = FontStyles.Bold;
                var brt = s.badge.rectTransform;
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
                brt.pivot = new Vector2(0f, 1f);
                brt.anchoredPosition = new Vector2(8f, -6f);
                brt.sizeDelta = new Vector2(170f, 30f);

                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => { selected = index; Refresh(); });
                slots.Add(s);
            }
        }

        // 막대(Up의 두 번째 Image) 안에 채움과 "n/합성 개수" 글자를 붙인다
        void BuildMergeBar(Transform bar)
        {
            if (bar == null) return;
            var bg = bar.GetComponent<Image>();
            if (bg != null && bg.sprite == null) bg.color = new Color(0.85f, 0.82f, 0.78f);   // 그림(progress_back)이 있으면 그대로
            var fill = NewImage("Fill", bar);
            fill.color = new Color(0.45f, 0.78f, 0.38f);
            fill.preserveAspect = false;
            mergeBarFill = fill.rectTransform;
            mergeBarFill.anchorMin = Vector2.zero;
            mergeBarFill.anchorMax = new Vector2(0f, 1f);
            mergeBarFill.offsetMin = mergeBarFill.offsetMax = Vector2.zero;
            mergeBarText = NewText("Text", bar, 22f);
            var trt = mergeBarText.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
        }

        TMP_Text AddRowText(Transform row)
        {
            if (row == null) return null;
            var t = NewText("Text", row, 28f);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(20f, 0f);
            rt.offsetMax = new Vector2(-20f, 0f);
            return t;
        }

        // ── 동작 ──

        void EquipSelected()
        {
            if (!Eq.IsOwned(selected)) { Notify($"가지고 있지 않은 {Eq.Title}입니다"); return; }
            Eq.Equip(selected);
            Notify(Eq.Names[selected] + " 장착");
        }

        void MergeSelected()
        {
            if (!Eq.CanMerge(selected))
            {
                int lv = Eq.Level(selected);
                Notify(lv <= 0 ? $"가지고 있지 않은 {Eq.Title}입니다"
                    : lv >= Equipment.MaxLevel ? "최고 레벨입니다"
                    : $"같은 {Eq.Title} 조각 {Equipment.CopiesToNext(lv)}개가 있어야 합성할 수 있습니다");
                return;
            }
            int up = Eq.Merge(selected);
            Notify($"{Eq.Names[selected]} Lv.{Eq.Level(selected)} (+{up})");
        }

        void MergeAll()
        {
            int up = Eq.MergeAll();
            Notify(up > 0 ? $"일괄 합성: {Eq.Title} 레벨 +{up}" : $"합성할 {Eq.Title}가 없습니다");
        }

        // 소환 = 하단바 뽑기 탭
        void OpenSummon()
        {
            if (TabGroup.MainBar != null) TabGroup.MainBar.Select(MainNavigation.GachaTab);
            else MainNavigation.Open(MainNavigation.GachaTab);
        }

        Toast toast;

        void Notify(string message)
        {
            if (toast == null && toastPrefab != null)
            {
                var canvas = GetComponentInParent<Canvas>();
                toast = Instantiate(toastPrefab, canvas != null ? canvas.rootCanvas.transform : transform);
            }
            if (toast != null) toast.Show(message, 2f);
            else Debug.Log($"[{Eq.Title}] {message}");
        }

        int HighestOwned()
        {
            for (int i = Eq.Count - 1; i >= 0; i--) if (Eq.IsOwned(i)) return i;
            return 0;
        }

        // ── 그리기 ──

        void Refresh()
        {
            int equipped = Eq.Equipped;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                int lv = Eq.Level(i);
                s.icon.color = lv > 0 ? Color.white : Locked;
                s.count.text = lv <= 0 ? "미보유"
                    : lv >= Equipment.MaxLevel ? $"Lv.{lv} MAX"
                    : $"Lv.{lv} <size=80%>({Eq.Copies(i)}/{Equipment.CopiesToNext(lv)})</size>";
                s.badge.text = i == equipped ? "<color=#3A8F2E>장착</color>" : "";
                s.bg.color = i == selected ? SelectedBg : i == equipped ? EquippedBg : Color.white;
            }

            if (ownTotal != null)
                ownTotal.text = $"보유 효과: {Eq.StatName} +{Eq.TotalOwnPercent:0.#}%   장착 효과: {Eq.StatName} +{Eq.EquipPercent(equipped):0.#}%";

            if (selected < 0 || selected >= Eq.Count) return;
            int level = Eq.Level(selected), copies = Eq.Copies(selected), need = Equipment.CopiesToNext(level);
            bool have = level > 0, max = level >= Equipment.MaxLevel;
            if (detailIcon != null) { detailIcon.sprite = Eq.Sprite(selected); detailIcon.color = have ? Color.white : Locked; }
            if (detailName != null) detailName.text = $"[{GachaProgress.GradeNames[Equipment.Grade(selected)]}] {Eq.Names[selected]}";
            if (detailCount != null) detailCount.text = have ? $"Lv.{level}" : "미보유";
            if (mergeBarFill != null) mergeBarFill.anchorMax = new Vector2(max ? 1f : have ? Mathf.Clamp01(copies / (float)need) : 0f, 1f);
            if (mergeBarText != null) mergeBarText.text = !have ? "뽑기로 얻을 수 있습니다" : max ? "최고 레벨" : $"다음 레벨 조각 {copies}/{need}";
            // 다음 레벨 값도 같이 보여 준다
            string Next(double next) => have && !max ? $"  <size=80%><color=#3A8F2E>→ +{next:0.#}%</color></size>" : "";
            int shown = Mathf.Max(1, level);
            if (equipStat != null) equipStat.text = $"장착 효과  {Eq.StatName} +{Equipment.EquipPercentAt(selected, shown):0.#}%" + Next(Equipment.EquipPercentAt(selected, level + 1));
            if (ownStat != null) ownStat.text = $"보유 효과  {Eq.StatName} +{Equipment.OwnPercentAt(selected, shown):0.#}%" + Next(Equipment.OwnPercentAt(selected, level + 1));

            bool isEquipped = selected == equipped;
            if (equipButton != null) equipButton.interactable = have && !isEquipped;
            if (equipLabel != null) equipLabel.text = isEquipped ? "장착 중" : "장착";
            if (mergeButton != null) mergeButton.interactable = Eq.CanMerge(selected);
        }

        Image NewImage(string name, Transform parent)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        TMP_Text NewText(string name, Transform parent, float size)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = new Color(0.25f, 0.2f, 0.15f);
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }
    }
}
