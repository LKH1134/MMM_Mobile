using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 하단바 탭의 해금 표시: 아직 안 열린 탭은 아이콘·글자를 어둡게 하고 자물쇠 판(LockHit)을 켜 누름을 막는다 (누르면 "스테이지 N에 도달하면 열려요" 안내).
    // 열렸지만 아직 한 번도 안 연 탭은 반짝여서(NewPulse) 어디인지 알려 준다.
    // 하단바 프리팹(BottomBar)에 붙어 있다. 탭마다 LockHit·NewPulse 자식은 프리팹에 배치되어 있고, 여기서는 켜고 끄기만 한다
    public class BottomBarLocks : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            public Feature feature;
            public Button button;
            public GameObject lockHit;      // 자물쇠 + 누름을 받는 판 (잠겨 있을 때만 켠다)
            public Button lockButton;       // lockHit의 버튼 (누르면 안내)
            public Image pulse;             // 처음 열 때까지 반짝이는 판
        }

        [SerializeField] Entry[] entries;
        [SerializeField] Color dimIcon = new Color(0.32f, 0.32f, 0.36f, 1f);
        [SerializeField] Color dimLabel = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] Color pulseColor = new Color(1f, 0.86f, 0.3f, 1f);
        [SerializeField] float pulseSpeed = 4f, pulseMin = 0.12f, pulseMax = 0.38f;

        static BottomBarLocks current;

        // 실행 중에 정해 두는 원래 색 (어둡게 했다가 되돌릴 때)
        Image[] icons;
        Color[] iconColors;
        TMP_Text[][] labels;
        Color[][] labelColors;

        void Awake()
        {
            int n = entries != null ? entries.Length : 0;
            icons = new Image[n]; iconColors = new Color[n];
            labels = new TMP_Text[n][]; labelColors = new Color[n][];
            for (int i = 0; i < n; i++)
            {
                var e = entries[i];
                if (e.button == null) continue;
                var lift = e.button.GetComponent<TabIconLift>();
                icons[i] = lift != null ? lift.IconImage : null;
                if (icons[i] != null) iconColors[i] = icons[i].color;
                labels[i] = e.button.GetComponentsInChildren<TMP_Text>(true);
                labelColors[i] = new Color[labels[i].Length];
                for (int k = 0; k < labels[i].Length; k++) labelColors[i][k] = labels[i][k].color;
                var feat = e.feature;
                if (e.lockButton != null) e.lockButton.onClick.AddListener(() => UnlockAnnouncer.ShowHint(HintFor(feat)));
                e.button.onClick.AddListener(() => FeatureUnlock.MarkOpened(feat));
            }
        }

        // 잠긴 탭을 눌렀을 때 안내: 소환은 편성 화면을 열면, 나머지는 해금 스테이지에 도달하면
        static string HintFor(Feature feature) => feature == Feature.Gacha
            ? Loc.T("unlock.hint_gacha")
            : Loc.F("unlock.hint", IdleBattle.HudStageNumber(FeatureUnlock.UnlockStage(feature)));

        // 이 기능의 하단바 버튼 (지금 씬의 하단바에 없으면 null)
        public static Button FindButton(Feature feature)
        {
            if (current == null || current.entries == null) return null;
            foreach (var e in current.entries)
                if (e.feature == feature && e.button != null && e.button.gameObject.activeInHierarchy) return e.button;
            return null;
        }

        void OnEnable()
        {
            current = this;
            FeatureUnlock.Changed += Apply;
            IdleProgress.Changed += FeatureUnlock.Check;
            FeatureUnlock.Check();
            Apply();
        }

        void OnDisable()
        {
            if (current == this) current = null;
            FeatureUnlock.Changed -= Apply;
            IdleProgress.Changed -= FeatureUnlock.Check;
        }

        void Apply()
        {
            // icons·labels는 Awake에서 만든다 — 플레이 중 스크립트를 다시 컴파일하면 Awake 없이 OnEnable만 다시 불려 비어 있다
            if (entries == null || icons == null || labels == null) return;
            for (int i = 0; i < entries.Length; i++)
            {
                var e = entries[i];
                if (e.button == null) continue;
                bool open = FeatureUnlock.IsUnlocked(e.feature);
                if (e.lockHit != null) e.lockHit.SetActive(!open);
                if (icons[i] != null) icons[i].color = open ? iconColors[i] : dimIcon;
                for (int k = 0; k < labels[i].Length; k++)
                    if (labels[i][k] != null) labels[i][k].color = open ? labelColors[i][k] : dimLabel;
                if (e.pulse != null) e.pulse.gameObject.SetActive(open && !FeatureUnlock.IsOpened(e.feature));
            }
        }

        void Update()
        {
            if (entries == null) return;
            float a = Mathf.Lerp(pulseMin, pulseMax, (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f);
            foreach (var e in entries)
                if (e.pulse != null && e.pulse.gameObject.activeSelf)
                    e.pulse.color = new Color(pulseColor.r, pulseColor.g, pulseColor.b, a);
        }
    }
}
