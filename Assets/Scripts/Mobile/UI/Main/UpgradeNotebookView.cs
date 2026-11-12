using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 홈 강화 목록 (프리팹 UpgradeNotebook): 노트 모양 판의 content 아래에 강화 줄(UpgradeRow 프리팹 인스턴스)을 능력치마다 하나씩 배치해 둔다.
    // 줄 순서·모양·아이콘은 프리팹에서 고치고, 이 스크립트는 IdleProgress 값만 넣는다. 예전 강화 목록(UpgradePanel, 지금은 지움)과 같은 규칙:
    // 잠긴 줄은 "OO Lv N 달성 시 잠금 해제"로 덮고, 누르고 있으면 계속 강화된다(HoldRepeat), 단계 제한이면 기준 레벨을 버튼에 적는다
    public class UpgradeNotebookView : MonoBehaviour
    {
        [SerializeField] List<UpgradeRowView> rows = new List<UpgradeRowView>();   // 배치한 줄 (줄마다 stat을 정해 둔다)
        [SerializeField] RectTransform content;     // 줄이 쌓이는 곳 (VerticalLayoutGroup + ContentSizeFitter)

        [Header("색")]
        [SerializeField] Color costColor = new Color(0.36f, 0.22f, 0.12f);
        [SerializeField] Color shortCostColor = new Color(0.85f, 0.25f, 0.2f);
        [SerializeField] Sprite disabledButtonSprite;   // 살 수 없을 때 버튼 그림 (비우면 UI/UI_Btn_Grey)
        [SerializeField] float cappedLabelSize = 28f;   // 단계 제한 문구 글자 크기

        RectTransform viewport;
        float shownContentY = float.NaN, shownViewportH = float.NaN;
        readonly Vector3[] corners = new Vector3[4];

        void Awake()
        {
            if (content == null || rows.Count == 0)
            {
                Debug.LogError("[UpgradeNotebookView] content·rows를 연결한다", this);
                enabled = false;
                return;
            }
            viewport = content.parent as RectTransform;
            rows.RemoveAll(r => r == null);
            foreach (var r in rows) InitRow(r);
        }

        // 스프링: 목록 가림에 잘리지 않게 두고, 가운데가 목록(viewport) 위·아래 밖으로 나가면(= 반 넘게 나가면) 숨긴다.
        // 스크롤·크기가 바뀐 프레임에만 다시 본다
        void LateUpdate()
        {
            if (viewport == null) return;
            float y = content.anchoredPosition.y, h = viewport.rect.height;
            if (y == shownContentY && h == shownViewportH) return;
            shownContentY = y;
            shownViewportH = h;
            viewport.GetWorldCorners(corners);
            float bottom = corners[0].y, top = corners[1].y;
            foreach (var r in rows)
            {
                if (r.springs == null) continue;
                foreach (var s in r.springs)
                {
                    if (s == null) continue;
                    s.rectTransform.GetWorldCorners(corners);
                    float mid = (corners[0].y + corners[1].y) * 0.5f;
                    bool show = mid <= top && mid >= bottom;
                    if (s.enabled != show) s.enabled = show;
                }
            }
        }

        void OnEnable()
        {
            Wallet.Changed += RefreshAfford;    // 전투 골드로 1초마다 온다 — 살 수 있는지만 다시 본다
            IdleProgress.Changed += OnProgressChanged;
            Loc.Changed += OnLanguageChanged;
            OnLanguageChanged();
        }

        void OnDisable()
        {
            Wallet.Changed -= RefreshAfford;
            IdleProgress.Changed -= OnProgressChanged;
            Loc.Changed -= OnLanguageChanged;
        }

        // 언어가 바뀌면(또는 꺼져 있는 동안 바뀌었으면) 이름·잠금 문구를 다시 쓰고 값도 다시 그린다
        void OnLanguageChanged()
        {
            foreach (var r in rows) WriteNames(r, IdleProgress.Get(r.stat));
            Refresh();
        }

        static void WriteNames(UpgradeRowView r, IdleProgress.Def d)
        {
            if (r.nameText != null) r.nameText.text = IdleProgress.DisplayName(d.stat);
            if (r.lockText != null) r.lockText.text = d.unlockLv > 0 ? Loc.F("upgrade.unlock_at", IdleProgress.DisplayName(d.unlockStat), d.unlockLv) : "";
        }

        // 강화 버튼을 누르고 있으면 초당 수십 번 온다 → 줄 전체 다시 쓰기는 RefreshEvery마다 한 번으로 묶는다 (첫 번째는 바로)
        const float RefreshEvery = 0.1f;
        float nextRefreshAt;
        bool refreshPending;

        void OnProgressChanged()
        {
            if (Time.unscaledTime >= nextRefreshAt) Refresh();
            else refreshPending = true;
        }

        void Update()
        {
            if (refreshPending && Time.unscaledTime >= nextRefreshAt) Refresh();
        }

        // 화면 폭(Stage 1080~1316)이 바뀌면 줄 폭을 바로 다시 맞춘다 — 그대로 두면 줄이 예전 폭에 남아 스프링이 접힌 선에서 어긋난다
        void OnRectTransformDimensionsChange()
        {
            if (content != null) LayoutRebuilder.MarkLayoutForRebuild(content);
        }

        // 배치된 줄에 강화 동작을 잇고 버튼 글자의 원래 자리를 기억한다 (아이콘·HoldRepeat은 프리팹에 넣어 두었다)
        void InitRow(UpgradeRowView r)
        {
            // 아이콘 = 업그레이드 시트 resourceId (못 읽으면 프리팹에 놓은 그림 그대로)
            if (r.icon != null)
            {
                var path = IdleProgress.Get(r.stat)?.icon;
                var sprite = string.IsNullOrEmpty(path) ? null : Resources.Load<Sprite>(path);
                if (sprite != null) r.icon.sprite = sprite;
            }
            if (r.button != null)
            {
                var stat = r.stat;
                r.button.onClick.AddListener(() => IdleProgress.TryUpgrade(stat));
            }
            if (r.buttonText != null)
            {
                var rt = r.buttonText.rectTransform;
                r.labelY = rt.anchoredPosition.y;
                r.labelHeight = rt.sizeDelta.y;
                r.labelSize = r.buttonText.fontSize;
            }
        }

        void Refresh()
        {
            if (!isActiveAndEnabled) return;
            refreshPending = false;
            nextRefreshAt = Time.unscaledTime + RefreshEvery;
            foreach (var r in rows)
            {
                int lv = IdleProgress.Level(r.stat);
                bool unlocked = IdleProgress.Unlocked(r.stat);
                bool max = IdleProgress.IsMax(r.stat);
                bool capped = IdleProgress.IsCapped(r.stat);
                long cost = IdleProgress.Cost(r.stat);
                r.nextCost = cost;
                r.buyable = unlocked && !max && !capped;
                if (r.level != null) r.level.text = "LV " + lv;
                if (r.value != null) r.value.text = IdleProgress.FormatValue(r.stat, IdleProgress.Value(r.stat));
                if (r.lockCover != null) r.lockCover.SetActive(!unlocked);
                if (r.costRow != null) r.costRow.SetActive(!max && !capped);
                if (r.cost != null) r.cost.text = Wallet.Format(cost);
                if (r.buttonText != null)
                {
                    // 단계 제한: 다음 단계를 여는 기준 강화 레벨을 버튼에 적는다. 비용 줄이 없으면 글자를 가운데로
                    var d = IdleProgress.Get(r.stat);
                    r.buttonText.text = max ? Loc.T("upgrade.max")
                        : capped ? Loc.F("upgrade.cap_need", IdleProgress.DisplayName(d.unlockStat), IdleProgress.NextCapGateLevel(r.stat))
                        : Loc.T("upgrade.buy");
                    // 글자 크기·자리는 바뀔 때만 쓴다 (같은 값을 써도 레이아웃을 다시 계산한다)
                    float size = capped ? cappedLabelSize : r.labelSize;
                    if (r.buttonText.fontSize != size) r.buttonText.fontSize = size;
                    var rt = r.buttonText.rectTransform;
                    var sd = new Vector2(rt.sizeDelta.x, capped ? r.labelHeight * 2f : r.labelHeight);
                    if (rt.sizeDelta != sd) rt.sizeDelta = sd;
                    var ap = new Vector2(rt.anchoredPosition.x, max || capped ? 0f : r.labelY);
                    if (rt.anchoredPosition != ap) rt.anchoredPosition = ap;
                }
            }
            RefreshAfford();
        }

        // 잔액만 바뀌었을 때: 버튼을 누를 수 있는지·비용 색만 (글자·배치는 그대로)
        void RefreshAfford()
        {
            if (!isActiveAndEnabled) return;
            long gold = Wallet.Gold;
            foreach (var r in rows)
            {
                bool enough = gold >= r.nextCost;
                bool can = r.buyable && enough;
                if (r.button != null && r.button.interactable != can)
                {
                    r.button.interactable = can;
                    if (r.buttonImage != null)
                    {
                        if (r.normalButton == null) r.normalButton = r.buttonImage.sprite;
                        if (disabledButtonSprite == null) disabledButtonSprite = Resources.Load<Sprite>(UiSkin.BtnGrey);
                        r.buttonImage.sprite = can ? r.normalButton : disabledButtonSprite;   // 색을 입히지 않고 회색 그림으로 바꿔 낀다
                        r.buttonImage.color = Color.white;
                    }
                }
                if (r.cost != null)
                {
                    var c = enough ? costColor : shortCostColor;
                    if (r.cost.color != c) r.cost.color = c;
                }
            }
        }
    }
}
