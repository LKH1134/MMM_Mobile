using TMPro;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 꾸미기 시트: 뿌리·줄기·가지·수관 탭(만든 장식 수/전체 장식 수). 고른 자리의 칸 수와 그 자리 장식 목록(놓기·빼기). 장식은 공방에서 만든다
    public class TreeDecoPanel : TreePanel
    {
        TreeZone zone = TreeZone.Root;

        // [UI 개편 2026-11-05] 카드 모양은 프리팹 SkinPopup의 Sheet/Content/Templates에서 고친다 (자식 이름을 지켜야 값이 들어간다).
        //   Tabs: (가로 묶음, 비워 둠)   ZoneTab: Text (고른 자리는 SelectedSprite 그림)   Info: Text   Empty: Text
        //   DecoCard: Icon · Title · Sub · Button
        static readonly string[] TemplateNames = { "Tabs", "ZoneTab", "Info", "Empty", "DecoCard" };
        const string SelectedTab = "UI/UI_Btn_Yellow";

        protected override void Build()
        {
            if (!HasTemplates(TemplateNames)) { Debug.LogError("[TreeDecoPanel] Templates 견본이 빠졌다", this); return; }

            var tabs = Use("Tabs");
            foreach (var z in (TreeZone[])System.Enum.GetValues(typeof(TreeZone)))
            {
                var zz = z;
                var tab = Use("ZoneTab", tabs);
                var b = tab.GetComponent<UnityEngine.UI.Button>();   // 탭 자체가 버튼
                if (b != null) { UiSkin.GreyWhenDisabled(b); b.interactable = true; b.onClick.RemoveAllListeners(); b.onClick.AddListener(() => { zone = zz; Rebuild(); }); if (z == zone) b.image.sprite = Resources.Load<Sprite>(SelectedTab); }
                // 탭 숫자 = 그 자리 장식 중 만든 수 / 그 자리 장식 전체 수
                int made = 0, total = 0;
                foreach (var d in GameBalance.Decos)
                {
                    if (d.zone != z) continue;
                    total++;
                    if (WorldTree.OwnsDeco(d.key)) made++;
                }
                SetText(tab, "Text", $"{WorldTree.ZoneName(z)}\n<size=24>{made}/{total}</size>");
            }

            int slots = WorldTree.ZoneSlots(zone);
            var info = Use("Info");
            CardOff(info, slots <= 0);
            SetText(info, "Text", slots > 0
                ? Loc.F("deco.zone_info", WorldTree.ZoneName(zone), WorldTree.PlacedIn(zone).Count, slots)
                : Loc.F("deco.zone_locked", WorldTree.ZoneName(zone), WorldTree.ZoneOpenStage(zone)));

            bool any = false;
            foreach (var d in GameBalance.Decos)
            {
                if (d.zone != zone) continue;
                any = true;
                var dd = d;
                bool owned = WorldTree.OwnsDeco(d.key), placed = WorldTree.IsPlaced(d.key);
                var card = Use("DecoCard");
                CardOff(card, !owned);
                var icon = SetIcon(card, "Icon", WorldTree.DecoSprite(d));
                if (icon != null && !owned) icon.color = new Color(0f, 0f, 0f, 0.35f);   // 아직 없는 장식은 그림자만
                SetText(card, "Title", $"{d.Name}  <size=26>{WorldTree.DecoGradeName(d.grade)}</size>");
                string sub = WorldTree.EffectText(d.effect, d.value) + $"  ·  {Formation.Name(d.owner)}";
                if (owned && !placed && WorldTree.FirstPlaceRewardLeft(d))
                    sub += TreeUi.Colored("\n" + Loc.F("deco.first_place", Mycelite(GameBalance.ConstInt("decoFirstMycelite"))), TreeUi.Good);
                SetText(card, "Sub", sub);
                if (!owned) SetButton(card, "Button", Loc.T("deco.needs_craft"), false, null);
                else if (placed) SetButton(card, "Button", Loc.T("deco.remove"), true, () => WorldTree.Remove(dd), "UI/UI_Btn_Red");
                else SetButton(card, "Button", Loc.T("deco.place"), WorldTree.CanPlace(d), () => { if (WorldTree.Place(dd)) Toast(Loc.F("deco.placed_toast", dd.Name)); });
            }
            if (!any) SetText(Use("Empty"), "Text", Loc.T("deco.no_decos"));
        }
    }
}
