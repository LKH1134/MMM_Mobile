using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 새 기능이 열리면 "새로운 콘텐츠!" 안내 창을 띄운다 (리본 제목 + 기능 이름 + 아이콘 + 설명 + 확인). 한 번에 하나, 열린 순서대로.
    // 홈 화면을 보고 있을 때(Main 씬·다른 팝업·절전·로딩 없음) 띄운다. 잠긴 탭을 눌렀을 때의 짧은 안내(ShowHint)도 여기서 보여 준다.
    // 프리팹 Prefabs/Mobile/UI/Common/UnlockAnnouncer (Main·WorldTree 씬 캔버스에 배치). 모양·자리는 프리팹에서 고친다
    public class UnlockAnnouncer : MonoBehaviour
    {
        const float CheckEvery = 0.5f, PopSeconds = 0.25f, HintSeconds = 1.8f;

        static UnlockAnnouncer instance;

        [Header("안내 창")]
        [SerializeField] GameObject root;           // 어두운 막 + 창 (꺼 두고 쓴다)
        [SerializeField] RectTransform panel;       // 튀어나오는 창
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text descText;
        [SerializeField] Image icon;
        [SerializeField] Button okButton;

        [Header("잠긴 탭 안내")]
        [SerializeField] GameObject hint;
        [SerializeField] TMP_Text hintText;

        [Header("탭 가리키기")]
        [SerializeField] TutorialSpotlight guide;

        Feature current;
        bool open, inMain;
        float checkIn, hintLeft;
        Coroutine pop;

        void Awake()
        {
            instance = this;
            inMain = SceneManager.GetActiveScene().name == MainNavigation.MainScene;
            if (okButton != null) okButton.onClick.AddListener(Close);
            if (root != null) root.SetActive(false);
            if (hint != null) hint.SetActive(false);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            if (open) PopupGate.Closed(this);
        }

        // 잠긴 탭을 눌렀을 때 짧게 뜨는 안내 문구
        public static void ShowHint(string message)
        {
            if (instance == null || instance.hint == null) return;
            instance.hintText.text = message;
            instance.hint.SetActive(true);
            instance.hint.transform.SetAsLastSibling();
            instance.hintLeft = HintSeconds;
        }

        void Update()
        {
            if (hintLeft > 0f)
            {
                hintLeft -= Time.unscaledDeltaTime;
                if (hintLeft <= 0f && hint != null) hint.SetActive(false);
            }
            if (open || !inMain || root == null) return;
            if (guide != null && guide.IsActive) return;        // 탭 가리키는 중에는 다음 안내를 미룬다
            checkIn -= Time.unscaledDeltaTime;
            if (checkIn > 0f) return;
            checkIn = CheckEvery;
            if (SceneLoader.IsLoading || PowerSaveMode.Active || PopupGate.IsOpen) return;
            var bar = TabGroup.MainBar;
            if (bar == null) return;
            FeatureUnlock.Check();
            foreach (var f in FeatureUnlock.All)
            {
                if (!FeatureUnlock.IsUnlocked(f) || FeatureUnlock.IsAnnounced(f)) continue;
                // 스테이지로 열린 기능은 홈으로 돌아왔을 때 알린다. 소환은 편성 화면에 들어가서 열린 것이라 그 자리에서 바로 알린다
                if (bar.Current >= 0 && f != Feature.Gacha) continue;
                Show(f);
                return;
            }
        }

        // ── 안내 창 ──
        void Show(Feature feature)
        {
            current = feature;
            string key = "unlock." + feature.ToString().ToLowerInvariant();
            titleText.text = Loc.T("unlock.title");
            nameText.text = Loc.T(key + ".name");
            descText.text = Loc.T(key + ".desc");
            var sprite = FindIcon(feature);
            icon.sprite = sprite;
            icon.gameObject.SetActive(sprite != null);
            open = true;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            PopupGate.Opened(this, Close);
            if (pop != null) StopCoroutine(pop);
            pop = StartCoroutine(Pop());
        }

        // 탭 버튼의 그림을 그대로 쓴다 (없으면 그림 없이)
        static Sprite FindIcon(Feature feature)
        {
            var button = BottomBarLocks.FindButton(feature);
            var lift = button != null ? button.GetComponent<TabIconLift>() : null;
            return lift != null && lift.IconImage != null ? lift.IconImage.sprite : null;
        }

        void Close()
        {
            if (!open) return;
            open = false;
            FeatureUnlock.MarkAnnounced(current);
            root.SetActive(false);
            PopupGate.Closed(this);
            checkIn = 0.3f;     // 다음에 열린 기능이 있으면 곧바로 이어서
            StartGuide(current);
        }

        // 확인을 누르면 그 기능의 탭을 가리킨다 (처음 열 때까지)
        void StartGuide(Feature feature)
        {
            if (guide == null || FeatureUnlock.IsOpened(feature)) return;
            var button = BottomBarLocks.FindButton(feature);
            if (button != null) guide.Begin(button);
        }

        IEnumerator Pop()
        {
            for (float t = 0f; t < PopSeconds; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / PopSeconds, 3f);
                panel.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, k);
                yield return null;
            }
            panel.localScale = Vector3.one;
            pop = null;
        }
    }
}
