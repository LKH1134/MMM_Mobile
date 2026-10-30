using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 뽑기 연출: 화면을 어둡게 덮고, 뽑은 장비(꼬마) 칸을 하나씩 튀어나오게 늘어놓는다.
    // 칸 = 등급 색 빛 + 그림 + 등급 이름, 처음 얻은 것은 위에 "NEW!!". 연출 중 화면을 누르면 남은 칸을 한 번에 띄운다.
    // 처음 열 때 캔버스 맨 위에 만들어 두고 다시 쓴다 (GachaPanel.ShowResult). 하단 탭을 바꾸면 PopupGate로 닫힌다
    public class GachaResultPopup : MonoBehaviour
    {
        public struct Entry
        {
            public Sprite icon;
            public int grade;       // GachaProgress.GradeNames 번호
            public bool isNew;
        }

        const float MaxCell = 180f;
        const float Gap = 22f;
        const float AreaWidth = 960f;
        const float PopDelay = 0.06f;       // 칸 사이 간격
        const float PopTime = 0.18f;        // 칸 하나가 커졌다 자리 잡는 시간
        const string CreamSprite = "Art/Dream/UI/Window/btn_cream";

        static readonly Color[] GradeColors =
        {
            new Color(0.85f, 0.85f, 0.85f),     // 일반
            new Color(0.55f, 0.85f, 0.45f),     // 고급
            new Color(0.45f, 0.7f, 1f),         // 희귀
            new Color(0.8f, 0.5f, 1f),          // 영웅
            new Color(1f, 0.8f, 0.3f),          // 전설
        };
        static readonly Color TextColor = new Color(0.25f, 0.2f, 0.15f);
        static readonly Color NewColor = new Color(1f, 0.45f, 0.5f);

        TMP_FontAsset font;
        RectTransform grid;
        Button closeButton;
        readonly List<RectTransform> cells = new List<RectTransform>();
        Coroutine playing;

        public static GachaResultPopup Create(Transform canvasRoot, TMP_FontAsset font)
        {
            var go = new GameObject("GachaResultPopup", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(canvasRoot, false);
            var popup = go.AddComponent<GachaResultPopup>();
            popup.font = font;
            popup.Build();
            go.SetActive(false);
            return popup;
        }

        void Build()
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            var dim = GetComponent<Button>();
            dim.transition = Selectable.Transition.None;
            dim.onClick.AddListener(Skip);

            grid = new GameObject("Grid", typeof(RectTransform)).GetComponent<RectTransform>();
            grid.SetParent(rt, false);
            grid.anchorMin = grid.anchorMax = new Vector2(0.5f, 0.55f);
            grid.sizeDelta = Vector2.zero;

            var closeImage = NewImage("Close", rt, CreamSprite);
            closeImage.raycastTarget = true;
            closeButton = closeImage.gameObject.AddComponent<Button>();
            var crt = (RectTransform)closeButton.transform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.12f);
            crt.sizeDelta = new Vector2(320f, 120f);
            var label = NewText("Label", crt, 44f, TextColor);
            Stretch(label.rectTransform);
            label.text = "닫기";
            closeButton.onClick.AddListener(Close);
        }

        public void Show(IList<Entry> entries)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            PopupLayer.Raise(gameObject, PopupLayer.Popup);
            PopupGate.Opened(this, Close);
            Layout(entries);
            if (playing != null) StopCoroutine(playing);
            playing = StartCoroutine(Play());
        }

        void Close()
        {
            if (playing != null) { StopCoroutine(playing); playing = null; }
            PopupGate.Closed(this);
            gameObject.SetActive(false);
        }

        // 11개는 5칸씩, 그보다 많으면 7칸씩 줄을 채운다. 순서는 뽑힌 순서 그대로
        void Layout(IList<Entry> entries)
        {
            foreach (var c in cells) Destroy(c.gameObject);
            cells.Clear();

            int cols = entries.Count > 15 ? 7 : 5;
            int rows = (entries.Count + cols - 1) / cols;
            float cell = Mathf.Min(MaxCell, (AreaWidth - Gap * (cols - 1)) / cols);
            float step = cell + Gap * 1.6f;     // 줄 사이는 NEW 글자 자리만큼 넓게
            for (int i = 0; i < entries.Count; i++)
            {
                int r = i / cols, c = i % cols;
                float x = (c - (cols - 1) * 0.5f) * (cell + Gap);     // 마지막 줄은 왼쪽부터
                float y = ((rows - 1) * 0.5f - r) * step;
                cells.Add(BuildCell(entries[i], new Vector2(x, y), cell));
            }
        }

        RectTransform BuildCell(Entry e, Vector2 pos, float size)
        {
            var color = GradeColors[Mathf.Clamp(e.grade, 0, GradeColors.Length - 1)];
            var root = new GameObject("Cell", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(grid, false);
            root.anchoredPosition = pos;
            root.sizeDelta = new Vector2(size, size);

            var glow = NewImage("Glow", root, CreamSprite);
            Stretch(glow.rectTransform, -10f);
            glow.color = new Color(color.r, color.g, color.b, 0.75f);

            var frame = NewImage("Frame", root, CreamSprite);
            Stretch(frame.rectTransform);

            var icon = NewImage("Icon", root, null);
            icon.sprite = e.icon;
            Stretch(icon.rectTransform, size * 0.16f);

            var grade = NewText("Grade", root, size * 0.15f, new Color(color.r * 0.6f, color.g * 0.6f, color.b * 0.6f));
            grade.text = GachaProgress.GradeNames[Mathf.Clamp(e.grade, 0, GachaProgress.GradeCount - 1)];
            grade.fontStyle = FontStyles.Bold;
            grade.alignment = TextAlignmentOptions.TopLeft;
            Stretch(grade.rectTransform, size * 0.07f);

            if (e.isNew)
            {
                var badge = NewText("New", root, size * 0.17f, NewColor);
                badge.text = "NEW!!";
                badge.fontStyle = FontStyles.Bold;
                var brt = badge.rectTransform;
                brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
                brt.anchoredPosition = new Vector2(0f, size * 0.1f);
                brt.sizeDelta = new Vector2(size, size * 0.25f);
            }

            root.localScale = Vector3.zero;
            return root;
        }

        IEnumerator Play()
        {
            closeButton.gameObject.SetActive(false);
            for (int i = 0; i < cells.Count; i++)
            {
                StartCoroutine(Pop(cells[i]));
                yield return new WaitForSecondsRealtime(PopDelay);
            }
            yield return new WaitForSecondsRealtime(PopTime);
            closeButton.gameObject.SetActive(true);
            playing = null;
        }

        // 0 → 1.15 → 1
        static IEnumerator Pop(RectTransform cell)
        {
            for (float t = 0f; t < PopTime; t += Time.unscaledDeltaTime)
            {
                if (cell == null) yield break;
                float k = t / PopTime;
                cell.localScale = Vector3.one * (k < 0.6f ? Mathf.Lerp(0f, 1.15f, k / 0.6f) : Mathf.Lerp(1.15f, 1f, (k - 0.6f) / 0.4f));
                yield return null;
            }
            if (cell != null) cell.localScale = Vector3.one;
        }

        // 연출 중에 누르면 남은 칸을 한 번에 띄운다
        void Skip()
        {
            if (playing == null) return;
            StopAllCoroutines();
            playing = null;
            foreach (var c in cells) c.localScale = Vector3.one;
            closeButton.gameObject.SetActive(true);
        }

        Image NewImage(string name, Transform parent, string sprite)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.raycastTarget = false;
            img.preserveAspect = sprite == null;
            var sp = sprite != null ? Resources.Load<Sprite>(sprite) : null;
            if (sp != null)
            {
                img.sprite = sp;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 2.5f;
            }
            return img;
        }

        TMP_Text NewText(string name, Transform parent, float size, Color color)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        // inset > 0이면 안쪽으로 줄이고, < 0이면 바깥으로 넓힌다
        static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
