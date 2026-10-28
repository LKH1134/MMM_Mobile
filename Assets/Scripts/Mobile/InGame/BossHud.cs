using TMPro;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 보스 판 화면 위 보스 정보: 이름(Boss.xlsx name)과 본체 체력 바. 보스 판에서 보스가 나오면 보이고, 아니면 숨긴다.
    // 바는 본체 체력만 보여 준다. 분신(환상 버섯)이 살아 있으면 이름 옆에 "분신 ×N" — 분신까지 모두 쓰러뜨려야 처치라
    // 본체가 쓰러져도 분신이 남아 있으면 바는 0인 채로 남는다. 채움은 실제 체력을 부드럽게 따라가고(fillSpeed),
    // 깎인 몫은 잠깐 밝은 바(trail)로 남았다가 따라 줄어든다
    public class BossHud : MonoBehaviour
    {
        [SerializeField] HarvestField field;
        [SerializeField] GameObject root;             // 이름·바 묶음. 보스 판이 아니면 숨긴다
        [SerializeField] TMP_Text nameText;
        [SerializeField] RectTransform fill;          // 체력 바 채움. 앵커 x 최대값 = 남은 비율
        [SerializeField] RectTransform trail;         // 깎인 몫 잔상 (비워도 된다)
        [SerializeField] TMP_Text percentText;        // "73%" (비워도 된다)
        [SerializeField] float fillSpeed = 4f;        // 채움이 실제 비율을 따라가는 빠르기 (1초에 차이의 몇 배)
        [SerializeField] float trailDelay = 0.4f;     // 잔상이 따라 줄기 시작할 때까지
        [SerializeField] float trailSpeed = 1.2f;     // 잔상이 줄어드는 빠르기 (1초에 비율)

        RoundStats round;      // 판이 바뀌면(측정기·벤치마크의 다시 시작) 처음부터
        Mushroom main;
        bool mainGone;
        float shown, trailShown, trailWait;
        string bossName;
        int shownClones = -1;

        void Awake() => Hide();

        void Update()
        {
            var stats = field.Stats;
            if (stats != round)
            {
                round = stats;
                main = null;
                mainGone = false;
                shown = trailShown = 1f;
                shownClones = -1;
                bossName = stats != null && stats.Boss != null && !string.IsNullOrEmpty(stats.Boss.name) ? stats.Boss.name : "보스";
            }
            if (stats == null || !stats.BossRound) { Hide(); return; }

            int clones = 0;
            if (!mainGone)
            {
                // 본체 = 처음 나온 보스 몸. 쓰러지면 풀로 돌아가 다른 버섯으로 다시 쓰일 수 있으니 그 뒤로는 보지 않는다
                if (main == null)
                    foreach (var m in field.Mushrooms)
                        if (m.IsBoss && m.Alive) { main = m; break; }
                if (main != null && (!main.Alive || !main.IsBoss)) mainGone = true;
            }
            foreach (var m in field.Mushrooms)
                if (m.IsBoss && m.Alive && m != main) clones++;
            if (main == null && !mainGone) { Hide(); return; }   // 보스가 아직 안 나왔다

            if (!root.activeSelf) root.SetActive(true);
            float target = mainGone ? 0f : Mathf.Clamp01((float)main.Hp / main.MaxHp);
            float dt = Time.deltaTime;

            // 채움: 줄 때는 빠르게, 회복(청소부)도 같은 빠르기로 따라간다
            shown = Mathf.MoveTowards(shown, target, Mathf.Max(Mathf.Abs(target - shown) * fillSpeed, 0.05f) * dt);
            if (trailShown < shown) { trailShown = shown; trailWait = 0f; }
            else if (trailShown > shown)
            {
                trailWait += dt;
                if (trailWait >= trailDelay) trailShown = Mathf.MoveTowards(trailShown, shown, trailSpeed * dt);
            }
            if (Mathf.Approximately(trailShown, shown)) trailWait = 0f;

            SetWidth(fill, shown);
            if (trail != null) SetWidth(trail, trailShown);
            if (percentText != null) percentText.text = Mathf.CeilToInt(target * 100f) + "%";
            if (clones != shownClones)
            {
                shownClones = clones;
                nameText.text = clones > 0 ? $"{bossName}  <size=75%>분신 ×{clones}</size>" : bossName;
            }
        }

        void Hide()
        {
            if (root != null && root.activeSelf) root.SetActive(false);
        }

        static void SetWidth(RectTransform r, float ratio) => r.anchorMax = new Vector2(ratio, r.anchorMax.y);
    }
}
