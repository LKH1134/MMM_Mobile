using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 하단바 탭의 해금 표시: 아직 안 열린 탭은 아이콘·글자를 어둡게 하고 자물쇠를 얹어 누름을 막는다 (누르면 "스테이지 N에 도달하면 열려요" 안내).
    // 열렸지만 아직 한 번도 안 연 탭은 반짝여서 어디인지 알려 준다 (튜토리얼). 하단바가 있는 씬(Main·세계수)마다 자동으로 붙는다 — 씬·프리팹에 둘 것 없음
    public class BottomBarLocks : MonoBehaviour
    {
        const string LockSprite = "UI/Icon/lock";
        static readonly Color DimIcon = new Color(0.32f, 0.32f, 0.36f, 1f), DimLabel = new Color(1f, 1f, 1f, 0.45f);
        static readonly Color PulseColor = new Color(1f, 0.86f, 0.3f, 1f);
        const float PulseSpeed = 4f, PulseMin = 0.12f, PulseMax = 0.38f;

        sealed class Entry
        {
            public Feature feature;
            public Button button;
            public Image icon;
            public Color iconColor;
            public TMP_Text[] labels;
            public Color[] labelColors;
            public GameObject lockHit;      // 자물쇠 + 누름을 받는 투명 판 (잠겨 있을 때만 켠다)
            public Image pulse;
        }

        readonly List<Entry> entries = new List<Entry>();
        bool built;

        // 버튼 이름 → 기능 (하단바 프리팹의 버튼 이름)
        static bool TryFeature(string buttonName, out Feature feature)
        {
            switch (buttonName)
            {
                case "TeamBtn": feature = Feature.Team; return true;
                case "GachaBtn": feature = Feature.Gacha; return true;
                case "DungeonBtn": feature = Feature.Dungeon; return true;
                case "WorldTreeBtn": feature = Feature.WorldTree; return true;
                default: feature = default; return false;
            }
        }

        // 잠긴 탭을 눌렀을 때 안내: 소환은 편성 화면을 열면, 나머지는 해금 스테이지에 도달하면
        static string HintFor(Feature feature) => feature == Feature.Gacha
            ? Loc.T("unlock.hint_gacha")
            : Loc.F("unlock.hint", IdleBattle.HudStageNumber(FeatureUnlock.UnlockStage(feature)));

        // 이 기능의 하단바 버튼 (현재 씬에 없으면 null)
        public static Button FindButton(Feature feature)
        {
            string name = feature == Feature.Team ? "TeamBtn" : feature == Feature.Gacha ? "GachaBtn" : feature == Feature.Dungeon ? "DungeonBtn" : "WorldTreeBtn";
            foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (b.name == name && b.GetComponent<TabIconLift>() != null) return b;
            return null;
        }

        // ── 씬마다 붙이기 ──
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded += (s, m) => Install();
            Install();
        }

        static void Install()
        {
            foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!TryFeature(b.name, out _)) continue;
                var parent = b.transform.parent;
                if (parent != null && parent.GetComponent<BottomBarLocks>() == null) parent.gameObject.AddComponent<BottomBarLocks>();
            }
        }

        void Build()
        {
            built = true;
            foreach (Transform child in transform)
            {
                if (!TryFeature(child.name, out var feature) || !child.TryGetComponent<Button>(out var button)) continue;
                var e = new Entry { feature = feature, button = button };
                var lift = child.GetComponent<TabIconLift>();
                e.icon = lift != null ? lift.IconImage : null;
                if (e.icon != null) e.iconColor = e.icon.color;
                e.labels = child.GetComponentsInChildren<TMP_Text>(true);
                e.labelColors = new Color[e.labels.Length];
                for (int i = 0; i < e.labels.Length; i++) e.labelColors[i] = e.labels[i].color;

                // 반짝임 (버튼 맨 뒤)
                var pg = new GameObject("NewPulse", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                pg.GetComponent<LayoutElement>().ignoreLayout = true;
                pg.transform.SetParent(child, false);
                Stretch((RectTransform)pg.transform);
                pg.transform.SetAsFirstSibling();
                e.pulse = pg.GetComponent<Image>();
                e.pulse.raycastTarget = false;
                e.pulse.color = new Color(PulseColor.r, PulseColor.g, PulseColor.b, 0f);
                pg.SetActive(false);

                // 잠금 판 (맨 앞): 투명 판이 누름을 받아 안내를 띄운다. 가운데 자물쇠
                var hit = new GameObject("LockHit", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                hit.GetComponent<LayoutElement>().ignoreLayout = true;
                hit.transform.SetParent(child, false);
                Stretch((RectTransform)hit.transform);
                hit.transform.SetAsLastSibling();
                var hitImage = hit.GetComponent<Image>();
                hitImage.color = new Color(0f, 0f, 0f, 0.35f);          // 칸 전체를 살짝 어둡게
                var feat = feature;
                hit.GetComponent<Button>().onClick.AddListener(() => UnlockAnnouncer.ShowHint(HintFor(feat)));
                var lockIcon = new GameObject("Lock", typeof(RectTransform), typeof(Image));
                lockIcon.transform.SetParent(hit.transform, false);
                var lr = (RectTransform)lockIcon.transform;
                lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
                lr.sizeDelta = new Vector2(90f, 90f);
                var li = lockIcon.GetComponent<Image>();
                li.sprite = Resources.Load<Sprite>(LockSprite);
                li.preserveAspect = true;
                li.raycastTarget = false;
                e.lockHit = hit;

                button.onClick.AddListener(() => FeatureUnlock.MarkOpened(feat));
                entries.Add(e);
            }
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        void OnEnable()
        {
            if (!built) Build();
            FeatureUnlock.Changed += Apply;
            IdleProgress.Changed += FeatureUnlock.Check;
            FeatureUnlock.Check();
            Apply();
        }

        void OnDisable()
        {
            FeatureUnlock.Changed -= Apply;
            IdleProgress.Changed -= FeatureUnlock.Check;
        }

        void Apply()
        {
            foreach (var e in entries)
            {
                if (e.button == null) continue;
                bool open = FeatureUnlock.IsUnlocked(e.feature);
                e.lockHit.SetActive(!open);
                if (e.icon != null) e.icon.color = open ? e.iconColor : DimIcon;
                for (int i = 0; i < e.labels.Length; i++)
                    if (e.labels[i] != null) e.labels[i].color = open ? e.labelColors[i] : DimLabel;
                e.pulse.gameObject.SetActive(open && !FeatureUnlock.IsOpened(e.feature));
            }
        }

        void Update()
        {
            float a = Mathf.Lerp(PulseMin, PulseMax, (Mathf.Sin(Time.unscaledTime * PulseSpeed) + 1f) * 0.5f);
            foreach (var e in entries)
                if (e.pulse != null && e.pulse.gameObject.activeSelf)
                    e.pulse.color = new Color(PulseColor.r, PulseColor.g, PulseColor.b, a);
        }
    }
}
