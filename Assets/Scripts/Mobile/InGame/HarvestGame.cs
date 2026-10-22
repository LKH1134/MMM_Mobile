using System.Collections;
using UnityEngine;

namespace MoreMush.Mobile
{
    // InGame 씬 진행. 필드에서 버섯을 캐면 HUD에 돈·콤보를 올리고, 제한 시간이 다 되면 필드와 입력을 멈추고 결과 팝업을 연다.
    // 게임 오버 조건은 시간뿐이다. 콤보 = 마지막으로 바에 닿은 뒤 캔 버섯 수 (바에 닿으면 0부터)
    public class HarvestGame : MonoBehaviour
    {
        [SerializeField] StageHud hud;
        [SerializeField] HarvestField field;
        [SerializeField] FieldInput input;
        [SerializeField] PopupSlot resultPopup;      // 프리팹. 시간이 다 되면 HUD 캔버스 아래에 만든다
        [SerializeField] float resultDelay = 0.6f;   // 멈춘 화면을 잠깐 보여 준 뒤 결과를 연다

        public long Earned { get; private set; }
        public int HarvestCount { get; private set; }
        public bool Finished { get; private set; }
        public event System.Action<long> GameOver;   // 이번 판에 번 돈

        void Awake()
        {
            field.Harvested += OnHarvested;
            field.Launched += h =>
            {
                if (h != field.Main) return;
                hud.SetCombo(0);
                hud.StartTimer();   // 시간은 첫 투척부터 흐른다
            };
            field.BarHit += h => { if (h == field.Main) hud.SetCombo(0); };
            hud.TimeUp += Finish;
        }

        void OnHarvested(Mushroom m, Harvester h)
        {
            if (Finished) return;
            Earned += m.Reward;
            HarvestCount++;
            hud.AddGold(m.Reward);
            if (h == field.Main) hud.SetCombo(h.Combo);
        }

        void Finish()
        {
            if (Finished) return;
            Finished = true;
            field.Stop();
            input.Interactable = false;
            GameOver?.Invoke(Earned);
            StartCoroutine(ShowResult());
        }

        IEnumerator ShowResult()
        {
            yield return new WaitForSecondsRealtime(resultDelay);
            resultPopup.Get(hud.transform).GetComponent<ResultPopup>().Show(Earned, HarvestCount);
        }
    }
}
