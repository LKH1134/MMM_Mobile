using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 뽑기 연출: 화면을 어둡게 덮고, 뽑은 장비(꼬마) 칸을 창 안에 하나씩 튀어나오게 늘어놓는다. 창 바로 아래에 닫기.
    // 칸 = 등급 색 테두리 + 그림 + 등급 이름, 처음 얻은 것은 위에 "NEW!". 연출 중 화면을 누르면 남은 칸을 한 번에 띄운다.
    // Main 씬 캔버스에 배치(꺼 둔 GachaResultPopup). 칸은 Grid 아래 CellTemplate을 복제한다. 하단 탭을 바꾸면 PopupGate로 닫힌다
    public class GachaResultPopup : MonoBehaviour
    {
        public struct Entry
        {
            public Sprite icon;
            public int grade;       // GachaProgress.GradeNames 번호 (테두리 색)
            public string label;    // 칸 아래 글자. 비우면 등급 이름 (꼬마는 등급이 없어 이름을 넣는다)
            public bool isNew;
        }

        [SerializeField] RectTransform grid;            // 칸을 늘어놓는 자리 (창 가운데)
        [SerializeField] RectTransform window;          // 칸 크기에 맞춰 늘고 주는 창
        [SerializeField] Button closeButton;            // 창 바로 아래에 붙는다
        [SerializeField] Button skipArea;               // 어두운 막 (연출 중 누르면 건너뛴다)
        [SerializeField] GachaResultCell cellTemplate;  // 꺼 둔 칸 (이 크기 = templateSize)

        [Header("배치")]
        [SerializeField] float maxCell = 200f;          // 적게 뽑았을 때 칸 최대 크기
        [SerializeField] float gap = 16f;
        [SerializeField] float areaWidth = 940f;        // 칸을 늘어놓는 폭
        [SerializeField] float windowPad = 44f;         // 칸과 창 테두리 사이
        [SerializeField] float newSpace = 0.15f;        // 맨 윗줄 위 NEW 글자 자리 (칸 크기 배율)
        [SerializeField] float closeGap = 14f;          // 창 아래 끝과 닫기 사이
        [SerializeField] float popDelay = 0.06f, popTime = 0.18f;

        [Header("등급별 테두리 (일반·희귀·영웅·전설)")]
        [SerializeField] Sprite[] gradeSprites;

        readonly List<RectTransform> cells = new List<RectTransform>();
        float cellScale = 1f;
        Coroutine playing;

        void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (skipArea != null) skipArea.onClick.AddListener(Skip);
            if (cellTemplate != null) cellTemplate.gameObject.SetActive(false);
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

        // 15개까지(11회)는 5칸씩, 그보다 많으면 7칸씩 줄을 채운다. 순서는 뽑힌 순서 그대로
        void Layout(IList<Entry> entries)
        {
            foreach (var c in cells) Destroy(c.gameObject);
            cells.Clear();

            int cols = entries.Count > 15 ? 7 : Mathf.Clamp(entries.Count, 1, 5);
            int rows = (entries.Count + cols - 1) / cols;
            float cell = Mathf.Min(maxCell, (areaWidth - gap * (cols - 1)) / cols);
            float step = cell + gap * 2.2f;     // 줄 사이는 NEW 글자 자리만큼 넓게
            var templateRect = (RectTransform)cellTemplate.transform;
            cellScale = cell / Mathf.Max(1f, templateRect.rect.width);
            for (int i = 0; i < entries.Count; i++)
            {
                int r = i / cols, c = i % cols;
                float x = (c - (cols - 1) * 0.5f) * (cell + gap);     // 마지막 줄은 왼쪽부터
                float y = ((rows - 1) * 0.5f - r) * step;
                cells.Add(BuildCell(entries[i], new Vector2(x, y)));
            }

            // 창 = 칸 영역 + 여백 (위는 NEW 글자 자리, 아래는 등급 테두리 10). 닫기는 창 바로 아래
            float top = (rows - 1) * 0.5f * step + cell * (0.5f + newSpace) + windowPad;
            float bottom = -(rows - 1) * 0.5f * step - cell * 0.5f - 10f - windowPad;
            float width = cols * cell + (cols - 1) * gap + windowPad * 2f;
            if (window != null)
            {
                window.sizeDelta = new Vector2(width, top - bottom);
                window.anchoredPosition = new Vector2(0f, grid.anchoredPosition.y + (top + bottom) * 0.5f);
            }
            if (closeButton != null)
                ((RectTransform)closeButton.transform).anchoredPosition = new Vector2(0f, grid.anchoredPosition.y + bottom - closeGap);
        }

        RectTransform BuildCell(Entry e, Vector2 pos)
        {
            var cell = Instantiate(cellTemplate, grid, false);
            cell.gameObject.SetActive(true);
            var rt = (RectTransform)cell.transform;
            rt.anchoredPosition = pos;
            if (cell.glow != null && gradeSprites != null && gradeSprites.Length > 0)
                cell.glow.sprite = gradeSprites[Mathf.Clamp(e.grade, 0, gradeSprites.Length - 1)];
            if (cell.icon != null) cell.icon.sprite = e.icon;
            if (cell.nameText != null)
                cell.nameText.text = !string.IsNullOrEmpty(e.label) ? e.label : GachaProgress.GradeNames[Mathf.Clamp(e.grade, 0, GachaProgress.GradeCount - 1)];
            if (cell.newBadge != null) cell.newBadge.SetActive(e.isNew);
            rt.localScale = Vector3.zero;
            return rt;
        }

        IEnumerator Play()
        {
            closeButton.gameObject.SetActive(false);
            for (int i = 0; i < cells.Count; i++)
            {
                StartCoroutine(Pop(cells[i]));
                yield return new WaitForSecondsRealtime(popDelay);
            }
            yield return new WaitForSecondsRealtime(popTime);
            closeButton.gameObject.SetActive(true);
            playing = null;
        }

        // 0 → 1.15 → 1 (칸 배율 기준)
        IEnumerator Pop(RectTransform cell)
        {
            for (float t = 0f; t < popTime; t += Time.unscaledDeltaTime)
            {
                if (cell == null) yield break;
                float k = t / popTime;
                float s = k < 0.6f ? Mathf.Lerp(0f, 1.15f, k / 0.6f) : Mathf.Lerp(1.15f, 1f, (k - 0.6f) / 0.4f);
                cell.localScale = Vector3.one * (s * cellScale);
                yield return null;
            }
            if (cell != null) cell.localScale = Vector3.one * cellScale;
        }

        // 연출 중에 누르면 남은 칸을 한 번에 띄운다
        void Skip()
        {
            if (playing == null) return;
            StopAllCoroutines();
            playing = null;
            foreach (var c in cells) c.localScale = Vector3.one * cellScale;
            closeButton.gameObject.SetActive(true);
        }
    }
}
