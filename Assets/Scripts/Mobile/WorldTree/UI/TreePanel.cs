using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 세계수 팝업(성장·꼬마·공방·꾸미기)의 Content에 붙인다. 처음 켜질 때 Content의 기존 자식(준비 중 문구 등)을 끄고 스크롤 목록을 만든다.
    // 세계수·지갑·꼬마가 바뀌면 목록을 통째로 다시 만들고, 1초마다 Live 글자(남은 시간 등)만 고친다
    public abstract class TreePanel : MonoBehaviour
    {
        protected TMP_FontAsset font;
        protected RectTransform list;
        readonly List<(TMP_Text text, Func<string> value)> live = new List<(TMP_Text, Func<string>)>();
        float tick;
        bool dirty;

        void Init()
        {
            var popup = GetComponentInParent<UiPopup>(true);
            var sample = popup != null ? popup.GetComponentInChildren<TMP_Text>(true) : null;
            font = sample != null ? sample.font : TMP_Settings.defaultFontAsset;
            foreach (Transform child in transform) child.gameObject.SetActive(false);
            list = TreeUi.ScrollList(transform);
        }

        protected virtual void OnEnable()
        {
            if (list == null) Init();
            WorldTree.Changed += MarkDirty;
            Wallet.Changed += MarkDirty;
            Formation.Changed += MarkDirty;
            Rebuild();
        }

        protected virtual void OnDisable()
        {
            WorldTree.Changed -= MarkDirty;
            Wallet.Changed -= MarkDirty;
            Formation.Changed -= MarkDirty;
        }

        // 한 프레임에 여러 번 바뀌어도 한 번만 다시 만든다
        void MarkDirty() => dirty = true;

        void LateUpdate()
        {
            if (dirty) Rebuild();
            tick -= Time.unscaledDeltaTime;
            if (tick > 0f) return;
            tick = 1f;
            if (OnTick()) { Rebuild(); return; }
            foreach (var (text, value) in live) if (text != null) text.text = value();
        }

        protected void Rebuild()
        {
            dirty = false;
            live.Clear();
            for (int i = list.childCount - 1; i >= 0; i--)
            {
                var child = list.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            try { Build(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        protected abstract void Build();

        // 1초마다. true면 목록을 다시 만든다 (제작이 끝나 버튼이 바뀔 때 등)
        protected virtual bool OnTick() => false;

        protected TMP_Text Live(TMP_Text text, Func<string> value)
        {
            live.Add((text, value));
            text.text = value();
            return text;
        }

        protected static void Toast(string message)
        {
            var screen = FindAnyObjectByType<WorldTreeScreen>();
            if (screen != null) screen.ShowToast(message);
        }

        // ── [UI 개편 2026-11-05] 프리팹 견본 (Content/Templates 아래, 꺼 둔 카드들). 모양은 프리팹에서 고치고 코드는 값만 채운다 ──
        Transform templates;

        // 견본 묶음에 names가 모두 있으면 true. 하나라도 없으면 각 시트가 예전 코드 화면으로 그린다
        protected bool HasTemplates(params string[] names)
        {
            if (templates == null) templates = transform.Find("Templates");
            if (templates == null) return false;
            foreach (var n in names) if (templates.Find(n) == null) return false;
            return true;
        }

        // 견본을 복사해 목록(또는 parent)에 붙인다
        protected RectTransform Use(string name, Transform parent = null)
        {
            var go = Instantiate(templates.Find(name).gameObject, parent != null ? parent : list, false);
            go.name = name;
            go.SetActive(true);
            return (RectTransform)go.transform;
        }

        protected static TMP_Text SetText(Transform card, string path, string text)
        {
            var t = path == null ? card.GetComponentInChildren<TMP_Text>(true) : card.Find(path)?.GetComponent<TMP_Text>();   // path null = 첫 글자 (제목 줄)
            if (t != null) t.text = text;
            return t;
        }

        protected static UnityEngine.UI.Image SetIcon(Transform card, string path, Sprite sprite)
        {
            var img = card.Find(path)?.GetComponent<UnityEngine.UI.Image>();
            if (img == null) return null;
            img.sprite = sprite;
            img.enabled = sprite != null;
            return img;
        }

        protected static void Show(Transform card, string path, bool on)
        {
            var t = card.Find(path);
            if (t != null) t.gameObject.SetActive(on);
        }

        // 꺼진 카드(아직 없음·잠김)는 회색 바탕
        protected static void CardOff(Transform card, bool off)
        {
            if (off && card.TryGetComponent<UnityEngine.UI.Image>(out var bg)) bg.sprite = Resources.Load<Sprite>("UI/UI_PP_In_Color_Grey");
        }

        protected static void SetBar(Transform card, string path, float value)
        {
            var fill = card.Find(path) as RectTransform;
            if (fill != null) fill.anchorMax = new Vector2(Mathf.Clamp01(value), fill.anchorMax.y);
        }

        // label null = 견본 글자 그대로, sprite null = 견본 그림(누를 수 있을 때 색). 누를 수 없으면 회색
        protected static UnityEngine.UI.Button SetButton(Transform card, string path, string label, bool interactable, Action onClick, string sprite = null)
        {
            var b = card.Find(path)?.GetComponent<UnityEngine.UI.Button>();
            if (b == null) return null;
            if (sprite != null && b.image != null) b.image.sprite = Resources.Load<Sprite>(sprite);
            UiSkin.GreyWhenDisabled(b);
            b.interactable = interactable;
            b.onClick.RemoveAllListeners();
            if (onClick != null) b.onClick.AddListener(() => onClick());
            if (label != null) { var t = b.GetComponentInChildren<TMP_Text>(true); if (t != null) t.text = label; }
            return b;
        }

        protected static string Gold(long v) => Wallet.Format(v) + " 골드";
        protected static string Mycelite(long v) => "균사석 " + Wallet.Format(v);
    }
}
