using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 세계수 그림: 지금 성장 단계(WorldTreeGrowth.Stage)의 그림을 보여 준다. 단계가 바뀌면 그림만 바꾼다.
    // 단계마다 그림 크기가 달라 preserveAspect로 이 칸(패널) 안에 비율을 지켜 맞춘다
    [RequireComponent(typeof(Image))]
    public class WorldTreeView : MonoBehaviour
    {
        [SerializeField] Sprite[] stageSprites;   // 0 = 처음(새싹), 성장할 때마다 다음 번호

        Image image;

        void Awake()
        {
            image = GetComponent<Image>();
            image.preserveAspect = true;
        }

        void OnEnable()
        {
            WorldTreeGrowth.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => WorldTreeGrowth.Changed -= Refresh;

        void Refresh()
        {
            if (stageSprites == null || stageSprites.Length == 0) return;
            image.sprite = stageSprites[Mathf.Clamp(WorldTreeGrowth.Stage, 0, stageSprites.Length - 1)];
        }
    }
}
