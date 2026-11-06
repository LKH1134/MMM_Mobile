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
            if (!HasTemplates(TemplateNames)) { BuildByCode(); return; }

            var tabs = Use("Tabs");
            foreach (var z in (TreeZone[])System.Enum.GetValues(typeof(TreeZone)))
            {
                var zz = z;
                var tab = Use("ZoneTab", tabs);
                var b = tab.GetComponent<UnityEngine.UI.Button>();   // 탭 자체가 버튼
                if (b != null) { UiSkin.GreyWhenDisabled(b); b.interactable = true; b.onClick.RemoveAllListeners(); b.onClick.AddListener(() => { zone = zz; Rebuild(); }); if (z == zone) b.image.sprite = Resources.Load<Sprite>(SelectedTab); }
                SetText(tab, "Text", $"{WorldTree.ZoneName(z)}\n<size=24>{WorldTree.PlacedIn(z).Count}/{WorldTree.ZoneSlots(z)}</size>");
            }

            int slots = WorldTree.ZoneSlots(zone);
            var info = Use("Info");
            CardOff(info, slots <= 0);
            SetText(info, "Text", slots > 0
                ? $"{WorldTree.ZoneName(zone)} 칸 {WorldTree.PlacedIn(zone).Count}/{slots}  ·  놓은 장식만 효과가 있어요"
                : $"{WorldTree.ZoneName(zone)}는 세계수 {WorldTree.ZoneOpenStage(zone)}단계에 열려요");

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
                SetText(card, "Title", $"{d.name}  <size=26>{WorldTree.DecoGradeName(d.grade)}</size>");
                string sub = WorldTree.EffectText(d.effect, d.value) + $"  ·  {Formation.Name(d.owner)}";
                if (owned && !placed && WorldTree.FirstPlaceRewardLeft(d))
                    sub += TreeUi.Colored($"\n처음 놓으면 {Mycelite(GameBalance.ConstInt("decoFirstMycelite"))} · 호감도 +{GameBalance.ConstInt("decoFirstAffinity")}", TreeUi.Good);
                SetText(card, "Sub", sub);
                if (!owned) SetButton(card, "Button", "공방에서\n제작", false, null);
                else if (placed) SetButton(card, "Button", "빼기", true, () => WorldTree.Remove(dd), "UI/UI_Btn_Red");
                else SetButton(card, "Button", "놓기", WorldTree.CanPlace(d), () => { if (WorldTree.Place(dd)) Toast($"{dd.name}을(를) 놓았어요!"); });
            }
            if (!any) SetText(Use("Empty"), "Text", "이 자리에 놓는 장식이 없어요");
        }

        // 견본이 없을 때: 예전 코드 화면
        void BuildByCode()
        {
            // 탭
            var tabs = TreeUi.New("Tabs", list);
            tabs.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 120f;
            var zones = (TreeZone[])System.Enum.GetValues(typeof(TreeZone));
            float w = 1f / zones.Length;
            for (int i = 0; i < zones.Length; i++)
            {
                var z = zones[i];
                var holder = TreeUi.New(z.ToString(), tabs);
                TreeUi.Place(holder, new Vector2(w * i, 0f), new Vector2(w * (i + 1), 1f), new Vector2(6f, 0f), new Vector2(-6f, 0f));
                // 탭 숫자 = 그 자리 장식 중 만든 수 / 그 자리 장식 전체 수
                int made = 0, total = 0;
                foreach (var d in GameBalance.Decos)
                {
                    if (d.zone != z) continue;
                    total++;
                    if (WorldTree.OwnsDeco(d.key)) made++;
                }
                var b = TreeUi.Button(holder, $"{WorldTree.ZoneName(z)}\n<size=24>{made}/{total}</size>", true, () =>
                {
                    zone = z;
                    Rebuild();
                }, font, right: 0f, width: 0f, height: 0f, tint: z == zone ? TreeUi.ButtonAlt : TreeUi.ButtonOff);
                TreeUi.Stretch((RectTransform)b.transform);
            }

            int slots = WorldTree.ZoneSlots(zone);
            var info = TreeUi.Card(list, 110f, slots > 0);
            TreeUi.Line(info, slots > 0
                    ? $"{WorldTree.ZoneName(zone)} 칸 {WorldTree.PlacedIn(zone).Count}/{slots}  ·  놓은 장식만 효과가 있어요"
                    : $"{WorldTree.ZoneName(zone)}는 세계수 {WorldTree.ZoneOpenStage(zone)}단계에 열려요",
                30f, TreeUi.Ink, font, 32f, 24f, 0f, 1f);

            bool any = false;
            foreach (var d in GameBalance.Decos)
            {
                if (d.zone != zone) continue;
                any = true;
                DecoCard(d);
            }
            if (!any)
            {
                var empty = TreeUi.Card(list, 110f);
                TreeUi.Line(empty, "이 자리에 놓는 장식이 없어요", 30f, TreeUi.Faint, font, 32f, 24f, 0f, 1f);
            }
        }

        void DecoCard(DecoRow d)
        {
            bool owned = WorldTree.OwnsDeco(d.key), placed = WorldTree.IsPlaced(d.key);
            var card = TreeUi.Card(list, 180f, owned);
            var icon = TreeUi.Icon(card, WorldTree.DecoSprite(d), 130f);
            if (!owned) icon.color = new Color(0f, 0f, 0f, 0.35f);       // 아직 없는 장식은 그림자만
            TreeUi.Line(card, $"{d.name}  <size=26>{WorldTree.DecoGradeName(d.grade)}</size>", 34f, TreeUi.Ink, font, 180f, 270f, 0.58f, 0.92f).fontStyle = FontStyles.Bold;
            string sub = WorldTree.EffectText(d.effect, d.value) + $"  ·  {Formation.Name(d.owner)}";
            if (owned && !placed && WorldTree.FirstPlaceRewardLeft(d))
                sub += TreeUi.Colored($"\n처음 놓으면 {Mycelite(GameBalance.ConstInt("decoFirstMycelite"))} · 호감도 +{GameBalance.ConstInt("decoFirstAffinity")}", TreeUi.Good);
            TreeUi.Line(card, sub, 26f, TreeUi.Faint, font, 180f, 270f, 0.06f, 0.58f);

            if (!owned)
                TreeUi.Button(card, "공방에서\n제작", false, null, font);
            else if (placed)
                TreeUi.Button(card, "빼기", true, () => WorldTree.Remove(d), font, tint: TreeUi.Bad);
            else
                TreeUi.Button(card, "놓기", WorldTree.CanPlace(d), () =>
                {
                    if (WorldTree.Place(d)) Toast($"{d.name}을(를) 놓았어요!");
                }, font);
        }
    }
}
