using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 새 기능이 열리면 "새로운 콘텐츠!" 안내 창을 띄운다 (리본 제목 + 기능 이름 + 아이콘 + 설명 + 확인). 한 번에 하나, 열린 순서대로.
    // 홈 화면을 보고 있을 때(Main 씬·다른 팝업·절전·로딩 없음) 띄운다. 자기 오버레이 캔버스를 코드로 만들고 씬이 바뀌어도 남겨 둔다.
    // 잠긴 탭을 눌렀을 때의 짧은 안내(ShowHint)도 여기서 보여 준다
    public class UnlockAnnouncer : MonoBehaviour
    {
        const int SortingOrder = 31000;         // 방치 정산(30000) 위, 씬 로딩(32000) 아래
        const string FrameSprite = "UI/UI_PP_Out_Wood", PaperSprite = "UI/UI_PP_Out_Paper1", RibbonSprite = "UI/UI_PP_Under_Tag_Yellow", OkSprite = "UI/UI_Btn_Red";
        static readonly Color Ink = new Color(0.30f, 0.20f, 0.10f, 1f);
        const float CheckEvery = 0.5f, PopSeconds = 0.25f, HintSeconds = 1.8f;

        static UnlockAnnouncer instance;

        GameObject root, hint;
        TutorialSpotlight guide;
        RectTransform panel;
        TMP_Text titleText, nameText, descText, hintText;
        Image icon;
        Feature current;
        bool open, inMain;
        float checkIn, hintLeft;
        Coroutine pop;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("UnlockAnnouncer", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2340f);
            scaler.matchWidthOrHeight = 0.5f;
            instance = go.AddComponent<UnlockAnnouncer>();
            instance.inMain = SceneManager.GetActiveScene().name == MainNavigation.MainScene;
            instance.BindCamera();
            SceneManager.sceneLoaded += (s, m) =>
            {
                if (m != LoadSceneMode.Single) return;
                instance.inMain = s.name == MainNavigation.MainScene;
                instance.CloseImmediate();
                if (instance.guide != null) instance.guide.End();
                instance.BindCamera();
            };
        }

        // Main 캔버스(Screen Space - Camera)와 같은 카메라에 붙인다: 오버레이 캔버스는 어두운 반투명 막이 화면에 안 먹는 일이 있어, 팝업·광고 상자와 같은 방식으로 맞춘다.
        // 층은 sortingOrder로 (Main 캔버스 10000 위)
        void BindCamera()
        {
            var canvas = GetComponent<Canvas>();
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (c == canvas || !c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceCamera || c.worldCamera == null) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = c.worldCamera;
                canvas.planeDistance = c.planeDistance;
                return;
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        // 잠긴 탭을 눌렀을 때 짧게 뜨는 안내 문구
        public static void ShowHint(string message)
        {
            if (instance == null) return;
            instance.BuildHint();
            instance.hintText.text = message;
            instance.hint.SetActive(true);
            instance.hintLeft = HintSeconds;
        }

        void Update()
        {
            if (hintLeft > 0f)
            {
                hintLeft -= Time.unscaledDeltaTime;
                if (hintLeft <= 0f && hint != null) hint.SetActive(false);
            }
            if (open || !inMain) return;
            if (guide != null && guide.IsActive) return;        // 손가락 안내 중에는 다음 안내를 미룬다
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
            if (root == null) Build();
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
            PopupGate.Opened(this, Close);
            if (pop != null) StopCoroutine(pop);
            pop = StartCoroutine(Pop());
        }

        // 탭 버튼의 그림을 그대로 쓴다 (없으면 그림 없이)
        static Sprite FindIcon(Feature feature)
        {
            string buttonName = feature == Feature.Team ? "TeamBtn" : feature == Feature.Gacha ? "GachaBtn" : feature == Feature.Dungeon ? "DungeonBtn" : "WorldTreeBtn";
            foreach (var lift in FindObjectsByType<TabIconLift>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (lift.name == buttonName && lift.IconImage != null) return lift.IconImage.sprite;
            return null;
        }

        void Close()
        {
            if (!open) return;
            open = false;
            FeatureUnlock.MarkAnnounced(current);
            if (root != null) root.SetActive(false);
            PopupGate.Closed(this);
            checkIn = 0.3f;     // 다음에 열린 기능이 있으면 곧바로 이어서
            StartGuide(current);
        }

        // 확인을 누르면 그 기능의 탭을 가리킨다 (처음 열 때까지)
        void StartGuide(Feature feature)
        {
            if (FeatureUnlock.IsOpened(feature)) return;
            var button = BottomBarLocks.FindButton(feature);
            if (button == null) return;
            if (guide == null) guide = TutorialSpotlight.Create(transform);
            guide.Begin(button);
        }

        // 씬이 바뀌어 창이 사라질 때: 안내는 본 것으로 치지 않고 그냥 닫는다 (다음에 홈에서 다시 뜬다)
        void CloseImmediate()
        {
            if (!open) return;
            open = false;
            if (root != null) root.SetActive(false);
            PopupGate.Closed(this);
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

        // ── 화면 만들기 ──
        void Build()
        {
            var font = BorrowFont();
            root = new GameObject("UnlockPopup", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(transform, false);
            Stretch((RectTransform)root.transform);
            root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);      // 뒤를 어둡게 하고 누름을 막는다

            panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(root.transform, false);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(1000f, 1500f);

            var ribbon = NewImage("Ribbon", panel, Resources.Load<Sprite>(RibbonSprite), new Vector2(900f, 170f), new Vector2(0f, 560f));
            ribbon.type = Image.Type.Sliced;
            titleText = NewText("Title", ribbon.transform, 66f, Ink, font, new Vector2(800f, 130f), Vector2.zero);
            titleText.fontStyle = FontStyles.Bold;

            nameText = NewText("Name", panel, 100f, Color.white, font, new Vector2(900f, 150f), new Vector2(0f, 330f));
            nameText.fontStyle = FontStyles.Bold;

            icon = NewImage("Icon", panel, null, new Vector2(440f, 440f), new Vector2(0f, 20f));
            icon.preserveAspect = true;

            var box = NewImage("DescBox", panel, Resources.Load<Sprite>(PaperSprite), new Vector2(880f, 280f), new Vector2(0f, -390f));
            box.type = Image.Type.Sliced;
            descText = NewText("Desc", box.transform, 42f, Ink, font, new Vector2(780f, 220f), Vector2.zero);
            descText.fontStyle = FontStyles.Bold;

            var ok = NewImage("Ok", panel, Resources.Load<Sprite>(OkSprite), new Vector2(460f, 150f), new Vector2(0f, -690f));
            ok.type = Image.Type.Sliced;
            ok.raycastTarget = true;
            var label = NewText("Label", ok.transform, 54f, Color.white, font, new Vector2(400f, 100f), Vector2.zero);
            label.text = Loc.T("common.ok");
            var button = ok.gameObject.AddComponent<Button>();
            UiSkin.GreyWhenDisabled(button);
            button.targetGraphic = ok;
            button.onClick.AddListener(Close);
            root.SetActive(false);
        }

        void BuildHint()
        {
            if (hint != null) return;
            var font = BorrowFont();
            hint = new GameObject("Hint", typeof(RectTransform), typeof(Image));
            hint.transform.SetParent(transform, false);
            var rt = (RectTransform)hint.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(880f, 140f);
            rt.anchoredPosition = new Vector2(0f, -760f);
            var img = hint.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.8f);
            img.raycastTarget = false;
            hintText = NewText("Text", hint.transform, 46f, Color.white, font, new Vector2(840f, 120f), Vector2.zero);
            hint.SetActive(false);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 pos)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        static TMP_Text NewText(string name, Transform parent, float size, Color color, TMP_FontAsset font, Vector2 box, Vector2 pos)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = box;
            rt.anchoredPosition = pos;
            t.fontSize = size;
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = size * 0.5f;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            if (font != null) t.font = font;
            OutlineText.ApplyIfWhite(t);
            return t;
        }

        // 한글 글꼴은 씬에 있는 글자에서 가져온다 (TMP 기본 글꼴은 한글이 없다. AdBox와 같은 방식)
        TMP_FontAsset BorrowFont()
        {
            foreach (var t in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (!t.transform.IsChildOf(transform) && t.font != null) return t.font;
            return null;
        }
    }
}
