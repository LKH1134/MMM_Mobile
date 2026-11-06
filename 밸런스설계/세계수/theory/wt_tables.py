# 세계수 표 읽기 (지금 GameBalance.xlsx) + 수치안 v1 오버라이드. 코드(WorldTree*.cs)가 읽는 열만.
import os, sys, copy
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
sys.path.insert(0, os.path.join(ROOT, '밸런스설계', 'theory'))
import xlsx_dump

ZONES = ['Root', 'Trunk', 'Branch', 'Crown']


def _rows(sheet):
    hdr = sheet[0]
    return [dict(zip(hdr, r)) for r in sheet[3:] if r and r[0] not in ('', None)]


def _ids(s):
    return [int(x) for x in str(s).split(',') if str(x).strip()] if s not in (None, '') else []


def load_current():
    d = xlsx_dump.load(os.path.join(ROOT, 'Assets/Data/Tables/GameBalance.xlsx'))
    T = {}
    T['tree'] = [dict(stage=int(r['stage']), evoExp=int(r['evoExp']), evoGoldKills=float(r['evoGoldKills']),
                      evoMycelite=int(r['evoMycelite']), ringKills=float(r['ringKills']),
                      bagKillsPerHour=float(r['bagKillsPerHour']), bagMycelitePerHour=float(r['bagMycelitePerHour']),
                      craftSlots=int(r['craftSlots']), slots=[int(r['rootSlots']), int(r['trunkSlots']), int(r['branchSlots']), int(r['crownSlots'])],
                      preferIds=_ids(r['preferIds'])) for r in _rows(d['세계수'])]
    T['bless'] = [dict(key=r['key'], stage=int(r['stage']), effect=r['effect'], value=float(r['value'])) for r in _rows(d['가호'])]
    T['kids'] = [dict(key=r['key'], name=r['name'], grade=int(r['grade']), initialOwned=int(r['initialOwned']),
                      favoriteIds=_ids(r['favoriteIds']), bonusEffect=r['bonusEffect'], bonusValue=float(r['bonusValue']),
                      evoStage=int(r['evoStage'])) for r in _rows(d['꼬마'])]
    T['stars'] = [dict(star=int(r['star']), mycelite=int(r['mycelite']), needLevel=int(r['needLevel']), bagMul=float(r['bagMul']))
                  for r in _rows(d['꼬마진화'])]
    T['aff'] = [dict(level=int(r['level']), need=int(r['need']), bagBonus=float(r['bagBonus'])) for r in _rows(d['호감도'])]
    T['decos'] = [dict(key=r['key'], name=r['name'], owner=r['owner'], grade=int(r['grade']), zone=r['zone'],
                       effect=r['effect'], value=float(r['value'])) for r in _rows(d['장식'])]
    T['dgrades'] = [dict(grade=int(r['grade']), goldKills=float(r['goldKills']), mycelite=int(r['mycelite']), hours=float(r['hours']))
                    for r in _rows(d['장식등급'])]
    T['enemies'] = [dict(index=int(r['index']), name=r['name'], value=int(r['value']), requestCount=int(r['requestCount']))
                    for r in _rows(d['적'])]
    T['const'] = {r['key']: float(r['value']) for r in _rows(d['상수'])}
    # 등급 4개 (0 일반·1 희귀·2 영웅·3 전설, 2026-11-04 고급 삭제): [확률 4개..., expToNext] — 경험치는 마지막 칸
    T['gacha'] = [[float(r[k]) for k in ('oddsNormal', 'oddsRare', 'oddsEpic', 'oddsLegend')] + [int(r['expToNext'])]
                  for r in _rows(d['뽑기'])]
    T['kidEvoLevelGradeMul'] = T['const'].get('kidEvoLevelGradeMul')     # K1 (v1부터 코드·상수에 있음; 없으면 None)
    return T


# ── 수치안 v1 ──
V1 = dict(
    tree=dict(   # stage: evoExp, evoGoldKills, evoMycelite, ringKills, bagKillsPerHour, bagMycelitePerHour, craftSlots, slots(R,T,B,C)
        evoExp=[15000, 35000, 55000, 80000, 150000, 240000, 380000, 0],
        evoGoldKills=[1000, 4000, 7000, 10000, 20000, 30000, 50000, 0],
        evoMycelite=[0, 15, 30, 50, 80, 120, 160, 0],
        ringKills=[40, 150, 250, 400, 700, 1100, 1800, 3000],
        bagKillsPerHour=[25, 28, 32, 36, 40, 42, 45, 48],
        bagMycelitePerHour=[0, 0, 0.05, 0.1, 0.15, 0.2, 0.25, 0.3],
        craftSlots=[1, 1, 1, 2, 2, 2, 3, 3],
        slots=[[1, 0, 0, 0], [2, 1, 0, 0], [2, 2, 1, 0], [3, 2, 2, 1], [3, 3, 3, 3], [4, 4, 4, 4], [5, 5, 5, 5], [6, 6, 6, 6]],
    ),
    bless={'bless_1': ('Harvest', 0.05), 'bless_2': ('KillGold', 0.05), 'bless_3': ('Attack', 0.05), 'bless_4': ('Harvest', 0.05),
           'bless_5': ('Hp', 0.1), 'bless_6': ('CritDamage', 0.1), 'bless_7': ('BossMycelite', 0.15), 'bless_8': ('Attack', 0.1)},
    kids={'leaf': ('Harvest', 0.05), 'dew': ('Hp', 0.025), 'wind': ('CraftSpeed', 0.1), 'fire': ('Attack', 0.025),
          'rock': ('BagHours', 0.5), 'star': ('CritChance', 0.01), 'coin': ('KillGold', 0.03), 'chest': ('BagMycelite', 0.05),
          'beak': ('CritDamage', 0.1)},
    stars=[(0, 1, 1.0), (10, 1, 1.3), (30, 5, 1.6), (80, 12, 2.0)],    # (mycelite, needLevel, bagMul)
    aff_need=[300, 600, 900, 1200, 1500, 1800, 2100, 2400, 2700, 0],
    aff_bonus=[0, 0.02, 0.04, 0.06, 0.08, 0.1, 0.12, 0.14, 0.16, 0.18],
    decos={'leaf_lamp': ('BagGold', 0.05), 'leaf_house': ('BagHours', 1), 'leaf_crown': ('Harvest', 0.05),
           'dew_spring': ('KillGold', 0.03), 'dew_chime': ('BagMycelite', 0.05), 'dew_pond': ('Hp', 0.05),
           'wind_mill': ('CraftSpeed', 0.1), 'wind_swing': ('Harvest', 0.03), 'wind_catcher': ('Hp', 0.03),
           'fire_candle': ('CritChance', 0.02), 'fire_lantern': ('Attack', 0.03), 'fire_brazier': ('BagGold', 0.1),
           'rock_table': ('Harvest', 0.03), 'rock_moss': ('Hp', 0.03), 'rock_geode': ('BagMycelite', 0.05),
           'spark_orb': ('KillGold', 0.02), 'spark_bell': ('CritDamage', 0.1), 'spark_lamp': ('KillGold', 0.03),
           'moon_garland': ('BagGold', 0.1), 'moon_owl': ('BossMycelite', 0.05), 'moon_crescent': ('BagGold', 0.1),
           'star_rune': ('Attack', 0.03), 'star_orb': ('CritChance', 0.03), 'star_crown': ('CraftSpeed', 0.1)},
    dgrades=[(1500, 5, 4), (4000, 15, 12), (10000, 40, 24)],    # (goldKills, mycelite, hours)
    request_count={i: 200 for i in range(1001, 1011)},
    const={'treeRingMax': 10, 'treeRingGrowth': 1.35, 'treeRingBagBonus': 0.03, 'treeHarvestPerKill': 1,
           'tributePreferMul': 2, 'giftFavoriteMul': 3, 'giftMaxCount': 300, 'requestPerDay': 3, 'requestAffinity': 200,
           'requestGoldKills': 800, 'requestWeekGoal': 14, 'requestWeekMycelite': 60, 'kidBagShare': 0.1,
           'decoSetBagMul': 0.2, 'decoFirstMycelite': 5, 'decoFirstAffinity': 300, 'craftRushSeconds': 1200,
           'craftRushPerDay': 3, 'bagCapHours': 12},
    evoStage={'star': 4, 'beak': 5},
    kidEvoLevelGradeMul=0.7,     # 코드 변경 K1
)


def apply(T, P):
    T = copy.deepcopy(T)
    tp = P['tree']
    for i, r in enumerate(T['tree']):
        for k in ('evoExp', 'evoGoldKills', 'evoMycelite', 'ringKills', 'bagKillsPerHour', 'bagMycelitePerHour', 'craftSlots', 'slots'):
            r[k] = tp[k][i]
    for b in T['bless']:
        b['effect'], b['value'] = P['bless'][b['key']]
    for k in T['kids']:
        k['bonusEffect'], k['bonusValue'] = P['kids'][k['key']]
        if 'evoStage' in P and k['key'] in P['evoStage']:
            k['evoStage'] = P['evoStage'][k['key']]
    for s, (m, lv, bm) in zip(T['stars'], P['stars']):
        s['mycelite'], s['needLevel'], s['bagMul'] = m, lv, bm
    for a, n, b in zip(T['aff'], P['aff_need'], P['aff_bonus']):
        a['need'], a['bagBonus'] = n, b
    for dco in T['decos']:
        dco['effect'], dco['value'] = P['decos'][dco['key']]
    for g, (gk, m, h) in zip(T['dgrades'], P['dgrades']):
        g['goldKills'], g['mycelite'], g['hours'] = gk, m, h
    for e in T['enemies']:
        e['requestCount'] = P['request_count'][e['index']]
        if 'enemy_value' in P:
            e['value'] = P['enemy_value'][e['index']]
    T['const'].update(P['const'])
    T['kidEvoLevelGradeMul'] = P.get('kidEvoLevelGradeMul')
    return T


# ── 수치안 v2 (라운드 1 반려 R1~R6 반영) ──
V2 = copy.deepcopy(V1)
V2['tree'].update(
    bagKillsPerHour=[18, 19.8, 21.8, 24, 26.4, 29, 32, 35.2],          # R5·R6: 단계마다 ×1.1 ≥ 나이테 10의 ×1.09
    bagMycelitePerHour=[0, 0, 0.04, 0.07, 0.1, 0.14, 0.18, 0.22],      # 보따리 균사석 효과가 0.75로 커진 만큼 낮춤
)
V2['bless'] = {'bless_1': ('Harvest', 0.05), 'bless_2': ('KillGold', 0.01), 'bless_3': ('Attack', 0.02), 'bless_4': ('Harvest', 0.05),
               'bless_5': ('Hp', 0.03), 'bless_6': ('CritDamage', 0.05), 'bless_7': ('BossMycelite', 0.05), 'bless_8': ('Attack', 0.03)}
V2['kids'] = {'leaf': ('Harvest', 0.05), 'dew': ('Hp', 0.005), 'wind': ('CraftSpeed', 0.1), 'fire': ('Attack', 0.005),
              'rock': ('BagHours', 0.5), 'star': ('BagMycelite', 0.1), 'coin': ('BagGold', 0.05), 'chest': ('BagMycelite', 0.05),
              'beak': ('Harvest', 0.1)}
V2['decos'] = {'leaf_lamp': ('BagGold', 0.05), 'leaf_house': ('BagHours', 1), 'leaf_crown': ('Harvest', 0.05),
               'dew_spring': ('KillGold', 0.01), 'dew_chime': ('BagMycelite', 0.05), 'dew_pond': ('Hp', 0.01),
               'wind_mill': ('CraftSpeed', 0.1), 'wind_swing': ('Harvest', 0.03), 'wind_catcher': ('Hp', 0.01),
               'fire_candle': ('Attack', 0.01), 'fire_lantern': ('Attack', 0.01), 'fire_brazier': ('BagGold', 0.05),
               'rock_table': ('Harvest', 0.03), 'rock_moss': ('Hp', 0.01), 'rock_geode': ('BagMycelite', 0.05),
               'spark_orb': ('KillGold', 0.01), 'spark_bell': ('CritDamage', 0.05), 'spark_lamp': ('KillGold', 0.01),
               'moon_garland': ('BagGold', 0.05), 'moon_owl': ('BossMycelite', 0.05), 'moon_crescent': ('BagGold', 0.05),
               'star_rune': ('Attack', 0.01), 'star_orb': ('BagMycelite', 0.05), 'star_crown': ('CraftSpeed', 0.1)}
V2['const'] = dict(V1['const'], treeRingBagBonus=0.01)
V2['tree'].update(
    craftSlots=[1, 1, 1, 1, 1, 2, 2, 2],                               # R3: 공방이 6단계까지 이어지게
    slots=[[1, 0, 0, 0], [2, 1, 0, 0], [3, 2, 1, 0], [4, 3, 3, 0], [5, 4, 5, 1], [6, 6, 6, 5], [6, 6, 6, 6], [6, 6, 6, 6]],  # R3: 칸 없는 장식 ≤ 3
    evoExp=[15000, 35000, 55000, 80000, 150000, 250000, 420000, 0],    # 버섯 획득(부리 특기)이 커진 만큼 7단계를 늘려 8단계 ≈ 45일 유지
)
V2['dgrades'] = [(1500, 5, 6), (4000, 15, 24), (10000, 40, 48)]        # R3: 일반 6h(다음 접속) · 고급 하루 · 희귀 이틀
V2['const']['craftRushSeconds'] = 1800                                  # 이틀 제작 가속 = 96
# R1·R2 최종: 전투 효과는 공격 0.03 · 체력 0.03 · 처치 골드 0.01 · 치명 피해 0.05만 (가호·장식), 보스 균사석·치명 확률 없음.
# 꼬마 특기는 모두 세계수 안 효과(버섯 획득·보따리·제작)로 — 같은 효과면 높은 등급이 더 크게.
V2['bless'] = {'bless_1': ('Harvest', 0.05), 'bless_2': ('KillGold', 0.01), 'bless_3': ('Attack', 0.01), 'bless_4': ('Harvest', 0.05),
               'bless_5': ('Hp', 0.02), 'bless_6': ('CritDamage', 0.03), 'bless_7': ('BagMycelite', 0.1), 'bless_8': ('Attack', 0.01)}
V2['kids'] = {'leaf': ('Harvest', 0.03), 'dew': ('BagGold', 0.03), 'wind': ('CraftSpeed', 0.08), 'fire': ('Harvest', 0.04),
              'rock': ('BagHours', 0.5), 'star': ('BagMycelite', 0.08), 'coin': ('BagGold', 0.04), 'chest': ('BagMycelite', 0.05),
              'beak': ('Harvest', 0.08)}
V2['decos'] = {'leaf_lamp': ('BagGold', 0.03), 'leaf_house': ('BagHours', 1), 'leaf_crown': ('Harvest', 0.05),
               'dew_spring': ('BagGold', 0.03), 'dew_chime': ('BagMycelite', 0.05), 'dew_pond': ('Hp', 0.01),
               'wind_mill': ('CraftSpeed', 0.1), 'wind_swing': ('Harvest', 0.03), 'wind_catcher': ('CraftSpeed', 0.05),
               'fire_candle': ('Harvest', 0.03), 'fire_lantern': ('Attack', 0.01), 'fire_brazier': ('BagGold', 0.03),
               'rock_table': ('Harvest', 0.03), 'rock_moss': ('BagHours', 0.5), 'rock_geode': ('BagMycelite', 0.05),
               'spark_orb': ('BagGold', 0.03), 'spark_bell': ('CritDamage', 0.02), 'spark_lamp': ('Harvest', 0.03),
               'moon_garland': ('BagGold', 0.03), 'moon_owl': ('BagMycelite', 0.03), 'moon_crescent': ('BagGold', 0.03),
               'star_rune': ('Harvest', 0.03), 'star_orb': ('BagMycelite', 0.03), 'star_crown': ('CraftSpeed', 0.1)}
V2['tree']['evoExp'] = [15000, 35000, 55000, 80000, 160000, 270000, 470000, 0]   # 버섯 획득 합 0.41 → 0.82: 5~7단계를 늘려 8단계 ≈ 45일
# 라이브 되먹임(30일 ≤ 3일)까지 맞춘 최종: 공격 0.02 · 체력 0.02 · 치명 피해 0.03, 처치 골드 없음
V2['bless'].update(bless_2=('BagGold', 0.05), bless_5=('Hp', 0.01), bless_6=('CritDamage', 0.02))
V2['decos'].update(fire_lantern=('Attack', 0.01), spark_bell=('CritDamage', 0.01))
V2['bless'].update(bless_3=('Attack', 0.01), bless_8=('Attack', 0.01))
V2['decos'].update(fire_lantern=('BagGold', 0.03))
V2['decos'].update(star_rune=('Attack', 0.01))     # 별 룬석 = 공격 (불씨 등롱 대신; 공격 합 0.03 → 아래에서 확인)
V2['decos'].update(star_rune=('Harvest', 0.03))
for _k in ('leaf_lamp', 'dew_spring', 'fire_lantern', 'fire_brazier', 'spark_orb', 'moon_garland', 'moon_crescent'):
    V2['decos'][_k] = ('BagGold', 0.02)
V2['dgrades'] = [(1500, 5, 6), (4000, 15, 24), (10000, 30, 48)]        # 희귀 균사석 40 → 30: 15~30일 순 균사석 ≤ 10%

# ── 수치안 v3 (라운드 2 C4): 부리 특기 = 보따리 균사석 0.11/★ (8단계 뒤에도 쓰임, ★3 0.33 ≥ 별똥 0.32) ──
V3 = copy.deepcopy(V2)
V3['kids']['beak'] = ('BagMycelite', 0.11)
V3['tree']['evoExp'] = [15000, 35000, 55000, 80000, 160000, 260000, 440000, 0]   # 부리 버섯 획득(6·7단계 +0.08·0.16)이 빠진 만큼 6·7단계를 줄여 8단계 날짜 유지

# ── 수치안 v4 (라운드 4 D2, KHD 방치 v3 머지 뒤): 5단계 가지 칸 5 → 6 (칸 합 15 → 16) ──
# 5단계에 9일 머무는 동안 가장 먼저(13일)·가장 오래(27접속 중 20) 못 놓는 자리가 가지(고급 장식 5개 + 일반 1개 = 6개, 칸 5).
# 6단계 가지 6과 같아져 단계가 올라도 칸이 줄지 않는다 (가지 0/0/1/3/6/6/6/6).
V4 = copy.deepcopy(V3)
V4['tree']['slots'] = [list(s) for s in V3['tree']['slots']]
V4['tree']['slots'][4] = [5, 4, 6, 1]
