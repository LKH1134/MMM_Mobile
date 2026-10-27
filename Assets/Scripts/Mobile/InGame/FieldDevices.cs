using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 환경 복원 스킬로 필드에 놓는 장치. 판 시작 때 군락지보다 먼저 빈 자리를 잡는다.
    //   Lv.1 그루터기: 수확기가 튕기고, 닿은 수확기에 공격력 버프 (몇 초, 다시 닿으면 시간을 새로 채움)
    //   Lv.2 꽃밭: 지나가는 수확기에 잠깐 가속
    //   Lv.3 도토리 3개: 수확기가 닿으면 불이 켜지고, 셋 다 켜지면 몇 초 동안 점수 배율 (판마다 한 번)
    // 그림은 임시 동그라미다 (장치 그림이 생기면 바꾼다)
    public class FieldDevices : MonoBehaviour
    {
        [SerializeField] HarvestField field;
        [SerializeField] SkillEffects effects;
        [SerializeField] Sprite circle;
        [SerializeField] float stumpRadius = 55f;
        [SerializeField] float flowerRadius = 80f;
        [SerializeField] float acornRadius = 30f;
        [SerializeField] int acornCount = 3;
        [SerializeField] Color stumpColor = new Color(0.55f, 0.38f, 0.22f);
        [SerializeField] Color flowerColor = new Color(1f, 0.6f, 0.8f, 0.55f);
        [SerializeField] Color acornOff = new Color(0.45f, 0.32f, 0.2f);
        [SerializeField] Color acornOn = new Color(1f, 0.85f, 0.3f);

        readonly List<Image> acorns = new List<Image>();
        readonly List<Image> made = new List<Image>();    // 이번 판에 놓은 장치 그림
        readonly Stack<Image> spare = new Stack<Image>(); // 지난 판에 쓰고 꺼 둔 그림 (다시 쓴다)
        int lit;
        bool acornUsed;

        void Awake() => field.BeforeColonies += Place;

        // 판마다 새로 놓는다 (측정기가 필드를 Restart하면 다시 불린다).
        // 지난 판 그림은 Destroy하지 않고 꺼서 다시 쓴다 — 측정기는 한 프레임 안에서 수천 판을 돌려
        // Destroy(프레임 끝에 지워짐)가 쌓이면 필드 자식이 수만 개로 늘어 판이 점점 느려졌다
        void Place()
        {
            foreach (var image in made)
            {
                if (image == null) continue;
                image.gameObject.SetActive(false);
                spare.Push(image);
            }
            made.Clear();
            acorns.Clear();
            lit = 0;
            acornUsed = false;
            var s = field.Stats;
            if (s.Restoration >= 1 && field.FindFreeSpot(stumpRadius, out var stump))
            {
                field.AddObstacle(stump, stumpRadius, true, h => h.SetBuff(HarvesterBuff.Stump, s.Base.stumpDuration));
                Make("Stump", stump, stumpRadius, stumpColor);
            }
            if (s.Restoration >= 2 && field.FindFreeSpot(flowerRadius, out var flower))
            {
                field.AddObstacle(flower, flowerRadius, false, h => h.SetBuff(HarvesterBuff.Haste, s.Base.flowerHaste));
                Make("Flower", flower, flowerRadius, flowerColor);
            }
            if (s.Restoration >= 3)
                for (int i = 0; i < acornCount; i++)
                {
                    if (!field.FindFreeSpot(acornRadius, out var spot)) break;
                    var image = Make("Acorn", spot, acornRadius, acornOff);
                    acorns.Add(image);
                    field.AddObstacle(spot, acornRadius, false, h => Light(image));
                }
        }

        void Light(Image acorn)
        {
            if (acornUsed || acorn.color == acornOn) return;
            acorn.color = acornOn;
            if (++lit < acorns.Count) return;
            acornUsed = true;
            if (effects != null) effects.StartAcornBonus();
        }

        Image Make(string name, Vector2 position, float radius, Color color)
        {
            Image image;
            if (spare.Count > 0)
            {
                image = spare.Pop();
                image.gameObject.name = name;
                image.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                ((RectTransform)go.transform).SetParent(field.Rect, false);
                image = go.GetComponent<Image>();
                image.sprite = circle;
                image.raycastTarget = false;
            }
            var rt = image.rectTransform;
            rt.SetSiblingIndex(0);   // 버섯·수확기 밑에
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(radius * 2f, radius * 2f);
            image.color = color;
            made.Add(image);
            return image;
        }
    }
}
