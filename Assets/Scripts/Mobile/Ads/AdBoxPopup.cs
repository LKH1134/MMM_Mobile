using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 광고 상자를 누르면 뜨는 "광고 보기" 창 (골드 또는 균사석 보상 안내 + 취소 / 광고 보기).
    // 화면은 Main 씬에 배치한 AdBoxPopup 프리팹(AdBoxView, 자기 캔버스로 팝업 층)이고, 여기서는 값 채우기·버튼 동작만 한다
    public class AdBoxPopup
    {
        const string AdIcon = "UI/Icon/advertisement";
        const string AdIconMycelite = "UI/Icon/advertisement_mycelite";   // 균사석 상자: 동전 자리에 균사석

        readonly GameObject root;
        readonly TMP_Text goldText, myceliteText, statusText;
        readonly Button watchButton;
        Action onWatch, onClose, onCancel;

        public bool IsOpen => root != null && root.activeSelf;

        public AdBoxPopup(AdBoxView view)
        {
            root = view.gameObject;
            goldText = view.goldText;
            myceliteText = view.myceliteText;
            statusText = view.statusText;
            watchButton = view.watchButton;
            if (watchButton != null) { UiSkin.GreyWhenDisabled(watchButton); watchButton.onClick.AddListener(() => onWatch?.Invoke()); }
            if (view.cancelButton != null) view.cancelButton.onClick.AddListener(Cancel);
            root.SetActive(false);
        }

        // gold·mycelite = 받을 양. watch = "광고 보기"를 눌렀을 때, close = 창이 닫힐 때(취소·뒤로가기·보상 뒤),
        // cancel = "취소" 버튼을 눌렀을 때만 (닫히기 전에 불린다 — 상자를 치우는 데 쓴다)
        public void Open(long gold, long mycelite, Action watch, Action close, Action cancel = null)
        {
            onWatch = watch;
            onClose = close;
            onCancel = cancel;
            // 상자는 한 가지 보상만 준다: 받지 않는 쪽 묶음은 숨기고, 그림·안내 문구도 그 보상에 맞춘다
            bool mycelitePrize = mycelite > 0 && gold <= 0;
            if (goldText != null) { goldText.text = "+" + Wallet.Format(gold); goldText.transform.parent.gameObject.SetActive(gold > 0); }
            if (myceliteText != null) { myceliteText.text = "+" + Wallet.Format(mycelite); myceliteText.transform.parent.gameObject.SetActive(mycelite > 0); }
            var icon = root.transform.Find("Frame/Paper/AdIcon") ?? FindDeep(root.transform, "AdIcon");
            if (icon != null && icon.TryGetComponent<Image>(out var iconImage))
            {
                var sprite = Resources.Load<Sprite>(mycelitePrize ? AdIconMycelite : AdIcon);
                if (sprite != null) iconImage.sprite = sprite;
            }
            var desc = FindDeep(root.transform, "Desc");
            if (desc != null && desc.TryGetComponent<TMP_Text>(out var descText)) descText.text = Loc.T(mycelitePrize ? "adbox.desc_mycelite" : "adbox.desc_gold");
            if (statusText != null) statusText.text = "";
            if (watchButton != null) watchButton.interactable = true;
            root.SetActive(true);
        }

        public void SetWatchEnabled(bool enabled)
        {
            if (watchButton != null) watchButton.interactable = enabled;
        }

        public void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        public void Close()
        {
            if (!IsOpen) return;
            root.SetActive(false);
            var close = onClose;
            onClose = onWatch = onCancel = null;
            close?.Invoke();
        }

        void Cancel()
        {
            var cancel = onCancel;
            Close();
            cancel?.Invoke();
        }

        static Transform FindDeep(Transform parent, string name)
        {
            foreach (var t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
