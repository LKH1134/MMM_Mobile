using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 편성 화면 위쪽 장비 칸: 지금 장착한 무기·방어구 그림을 보여 준다. 장착을 바꾸면(Equipment.Changed) 바로 다시 그린다.
    // 아무것도 장착하지 않았으면 그림을 숨기고 "없음"을 띄운다
    public class EquippedGearView : MonoBehaviour
    {
        [SerializeField] Image weaponIcon;
        [SerializeField] Image armorIcon;
        [SerializeField] TMP_Text weaponEmpty;     // 장착 안 했을 때 문구
        [SerializeField] TMP_Text armorEmpty;

        void OnEnable()
        {
            Equipment.Weapon.Changed += Refresh;
            Equipment.Armor.Changed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            Equipment.Weapon.Changed -= Refresh;
            Equipment.Armor.Changed -= Refresh;
        }

        void Refresh()
        {
            Show(Equipment.Weapon, weaponIcon, weaponEmpty);
            Show(Equipment.Armor, armorIcon, armorEmpty);
        }

        static void Show(Equipment eq, Image icon, TMP_Text empty)
        {
            var sprite = eq.Sprite(eq.Equipped);
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
            if (empty != null) empty.gameObject.SetActive(sprite == null);
        }
    }
}
