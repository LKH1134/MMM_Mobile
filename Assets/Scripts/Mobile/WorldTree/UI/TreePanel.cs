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

        protected static string Gold(long v) => Wallet.Format(v) + " 골드";
        protected static string Mycelite(long v) => "균사석 " + Wallet.Format(v);
    }
}
