# 세계수 표 읽기 (지금 Assets/Data/Tables/MainTable.xlsx) — 코드(WorldTree*.cs · DailyQuests.cs · Formation.cs · IdleBattle.cs)가 읽는 열만.
# 2026-11-12 현행화: 보따리(11-03)·꼬마 등급(11-04)·의뢰 시트(11-10)·호감도·선물(11-12)을 뺐다. 옛 수치안 V1~V4 오버라이드는
# 지금 표에 없는 열(bagKillsPerHour·bagMul·호감도·kidBagShare …)을 가리켜 지웠다(기록은 이론.md v1~v6 절).
import os, sys, re, copy
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, os.path.join(ROOT, '밸런스설계', 'theory'))
import xlsx_dump

XLSX = os.path.join(ROOT, 'Assets', 'Data', 'Tables', 'MainTable.xlsx')
ENUMY = os.path.join(ROOT, 'Assets', 'Resources', 'Art', 'Dream', 'Characters', 'Enumy')   # IdleBattle.kindFolder (Main.unity)
ZONES = ['Root', 'Trunk', 'Branch', 'Crown']                        # WorldTree.cs:11 TreeZone
EFFECTS = ['Attack', 'Hp', 'Speed', 'CritChance', 'CritDamage', 'KillGold', 'BossMycelite', 'Harvest', 'CraftSpeed']   # WorldTree.cs:8 TreeEffect


def _rows(sheet):
    hdr = sheet[0]
    return [dict(zip(hdr, r)) for r in sheet[3:] if r and r[0] not in ('', None)]


def _ids(s):
    return [int(x) for x in str(s).split(',') if str(x).strip()] if s not in (None, '') else []


def _slices(sheet_name):
    """Resources 군락 시트의 조각 이름 (.meta sprites 목록)"""
    p = os.path.join(ENUMY, sheet_name + '.png.meta')
    if not os.path.exists(p):
        return []
    names = re.findall(r'^\s+name: (' + re.escape(sheet_name) + r'_\d+)\s*$', open(p, encoding='utf-8').read(), re.M)
    return sorted(set(names))


def store_weights(d, C):
    """일반 적 1마리 처치 → 창고에 들어가는 적.index 확률 (IdleBattle.PickEnemyPool :784 + NewEnemy :738-739 + WorldTree.AddHarvest).
    독성 enemyToxicRate · 약용 enemyMedicinalRate · 나머지 식용. 묶음 안에서는 조각(보스 조각 제외, GameBalance.IsBossSprite)마다 균등.
    조각 이름 '시트이름_번호' → 시트 이름 → 리소스.index → 적.resourceId 로 적.index를 찾는다(AddHarvest :147-153)."""
    res = {int(r['index']): r for r in _rows(d['리소스'])}
    boss = set()
    for b in _rows(d['보스']):
        path = res[int(b['resourceId'])]['path'] if int(b['resourceId']) in res else None
        if path:
            boss.add(path.rsplit('/', 1)[-1] + '_' + str(b['cell']))
    by_sheet = {}
    for e in _rows(d['적']):
        r = res.get(int(e['resourceId']))
        if r:
            by_sheet[r['path'].rsplit('/', 1)[-1]] = int(e['index'])
    pools = {'Edible': [], 'Toxic': [], 'Medicinal': []}
    for sheet, idx in by_sheet.items():
        kind = 'Toxic' if sheet.startswith('Mush_Toxic') else 'Medicinal' if sheet.startswith('Mush_Medicinal') else 'Edible'
        for sl in _slices(sheet):
            if sl not in boss:
                pools[kind].append(idx)
    tx, md = C['enemyToxicRate'], C['enemyMedicinalRate']
    share = {'Toxic': tx if pools['Toxic'] else 0, 'Medicinal': md if pools['Medicinal'] else 0}
    share['Edible'] = 1 - share['Toxic'] - share['Medicinal']
    w = {}
    for kind, ids in pools.items():
        for i in ids:
            w[i] = w.get(i, 0) + share[kind] / len(ids)
    return w, {k: len(v) for k, v in pools.items()}


def load_current(path=XLSX):
    d = xlsx_dump.load(path)
    T = {}
    T['const'] = {r['key']: float(r['value']) for r in _rows(d['상수'])}
    T['tree'] = [dict(stage=int(r['stage']), evoExp=int(r['evoExp']), evoGoldKills=float(r['evoGoldKills']),
                      evoMycelite=int(r['evoMycelite']), ringKills=float(r['ringKills']), craftSlots=int(r['craftSlots']),
                      slots=[int(r['rootSlots']), int(r['trunkSlots']), int(r['branchSlots']), int(r['crownSlots'])],
                      preferIds=_ids(r['preferIds'])) for r in _rows(d['세계수'])]
    T['bless'] = [dict(key=r['key'], stage=int(r['stage']), effect=r['effect'], value=float(r['value'])) for r in _rows(d['가호'])]
    # 꼬마: 등급 없음(11-04). 꼬마 뽑기 = 8종 같은 확률 (GachaPanel.cs:13, GachaProgress.PickCharacters)
    T['kids'] = [dict(key=r['key'], name=r['name'], initialOwned=int(r['initialOwned']), favoriteIds=_ids(r['favoriteIds']),
                      bonusEffect=r['bonusEffect'], bonusValue=float(r['bonusValue']), evoStage=int(r['evoStage']))
                 for r in _rows(d['꼬마'])]
    T['stars'] = [dict(star=int(r['star']), mycelite=int(r['mycelite']), needLevel=int(r['needLevel'])) for r in _rows(d['꼬마진화'])]
    T['decos'] = [dict(key=r['key'], name=r['name'], owner=r['owner'], grade=int(r['grade']), zone=r['zone'],
                       effect=r['effect'], value=float(r['value'])) for r in _rows(d['장식'])]
    T['dgrades'] = [dict(grade=int(r['grade']), goldKills=float(r['goldKills']), mycelite=int(r['mycelite']), hours=float(r['hours']))
                    for r in _rows(d['장식등급'])]
    T['enemies'] = [dict(index=int(r['index']), name=r['name'], value=int(r['value']), requestCount=int(r['requestCount']))
                    for r in _rows(d['적'])]
    # 메인 일일·주간 퀘스트 (DailyQuests.cs, 11-10 세계수 의뢰를 합침): 보상 골드(goldKills)·균사석·세계수 경험치(treeExp)
    T['quests'] = [dict(index=int(r['index']), key=r['key'], period=r['period'], kind=r['kind'], target=int(r['target']),
                        goldKills=float(r['goldKills']), mycelite=int(r['mycelite']), treeExp=int(r['treeExp'] or 0))
                   for r in _rows(d['퀘스트'])]
    T['store_w'], T['store_slices'] = store_weights(d, T['const'])
    ad = [r for r in _rows(d['뽑기상품']) if str(r['requiresAd']) in ('1', 'True', 'TRUE')]
    T['ad_gacha'] = {r['kind']: (int(r['pullCount']), int(r['dailyLimit'])) for r in ad}
    return T


def apply(T, P):
    """수치안 P를 표 T에 덮어쓴다. 키는 지금 표에 있는 열만:
    tree={열: [8칸]} · bless={key: (effect, value)} · kids={key: (effect, value)} · evoStage={key: n} ·
    stars=[(mycelite, needLevel) ×4] · decos={key: (effect, value)} · dgrades=[(goldKills, mycelite, hours) ×3] ·
    request_count={적.index: n} · quest_exp={퀘스트 key: treeExp} · const={key: value}"""
    T = copy.deepcopy(T)
    for col, vals in P.get('tree', {}).items():
        for r, v in zip(T['tree'], vals):
            r[col] = list(v) if col == 'slots' else v
    for b in T['bless']:
        if b['key'] in P.get('bless', {}):
            b['effect'], b['value'] = P['bless'][b['key']]
    for k in T['kids']:
        if k['key'] in P.get('kids', {}):
            k['bonusEffect'], k['bonusValue'] = P['kids'][k['key']]
        if k['key'] in P.get('evoStage', {}):
            k['evoStage'] = P['evoStage'][k['key']]
    for s, v in zip(T['stars'], P.get('stars', [])):
        s['mycelite'], s['needLevel'] = v
    for dco in T['decos']:
        if dco['key'] in P.get('decos', {}):
            dco['effect'], dco['value'] = P['decos'][dco['key']]
    for g, v in zip(T['dgrades'], P.get('dgrades', [])):
        g['goldKills'], g['mycelite'], g['hours'] = v
    for e in T['enemies']:
        if e['index'] in P.get('request_count', {}):
            e['requestCount'] = P['request_count'][e['index']]
    for q in T['quests']:
        if q['key'] in P.get('quest_exp', {}):
            q['treeExp'] = P['quest_exp'][q['key']]
    T['const'].update(P.get('const', {}))
    return T


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    T = load_current()
    print('창고 조각 수', T['store_slices'])
    print('창고 확률', {k: round(v, 4) for k, v in sorted(T['store_w'].items())}, '합', round(sum(T['store_w'].values()), 6))
    print('광고 뽑기', T['ad_gacha'])
    for q in T['quests']:
        print(q)
