# 적용 대조 (라운드 3): 실제 표 값 vs 이론.md v3 (v2 6.2~6.10 + beak·evoExp 6·7) (이론.md 표를 검증이 직접 옮겨 적음 — theory/wt_tables.py를 보지 않고)
# + 검증기(GameBalanceValidator.cs:182-241, RequiredConsts:41-58) 세계수 규칙 + 바로 확인 식(이론.md 8절)
import sys, math
sys.stdout.reconfigure(encoding='utf-8')
import tables
T = tables.load()
bad = []
n_checked = 0


def eq(name, got, want):
    global n_checked
    n_checked += 1
    if isinstance(want, tuple):
        ok = len(got) == len(want) and all((abs(a - b) < 1e-9) if isinstance(b, (int, float)) and not isinstance(b, bool) else a == b
                                           for a, b in zip(got, want))
    elif isinstance(want, (int, float)):
        ok = got is not None and abs(got - want) < 1e-9
    else:
        ok = got == want
    if not ok:
        bad.append(f'{name}: 표 {got!r} ≠ 이론 {want!r}')


V = dict(  # 이론.md 7.2
    evoExp=[15000, 35000, 55000, 80000, 160000, 260000, 440000, 0],
    evoGoldKills=[1000, 4000, 7000, 10000, 20000, 30000, 50000, 0],
    evoMycelite=[0, 15, 30, 50, 80, 120, 160, 0],
    ringKills=[40, 150, 250, 400, 700, 1100, 1800, 3000],
    bagKillsPerHour=[18, 19.8, 21.8, 24, 26.4, 29, 32, 35.2],
    bagMycelitePerHour=[0, 0, 0.04, 0.07, 0.1, 0.14, 0.18, 0.22],
    craftSlots=[1, 1, 1, 1, 1, 2, 2, 2],
    rootSlots=[1, 2, 3, 4, 5, 6, 6, 6], trunkSlots=[0, 1, 2, 3, 4, 6, 6, 6],
    branchSlots=[0, 0, 1, 3, 5, 6, 6, 6], crownSlots=[0, 0, 0, 0, 1, 5, 6, 6],
    preferIds=[[1001, 1002, 1003], [1004, 1005, 1006], [1007, 1008, 1009], [1010, 1001, 1004], [1002, 1005, 1008],
               [1003, 1006, 1009], [1007, 1010, 1001], [1008, 1009, 1010]])
for col, vals in V.items():
    for r, v in zip(T['tree'], vals):
        eq(f"세계수 {r['key']}.{col}", r[col], v)
B = {'bless_1': ('Harvest', .05), 'bless_2': ('BagGold', .05), 'bless_3': ('Attack', .01), 'bless_4': ('Harvest', .05),
     'bless_5': ('Hp', .01), 'bless_6': ('CritDamage', .02), 'bless_7': ('BagMycelite', .1), 'bless_8': ('Attack', .01)}
for r in T['bless']:
    eq(f"가호 {r['key']}", (r['effect'], r['value']), B[r['key']])
K = {'leaf': ('1002,1003', 'Harvest', .03, 1), 'dew': ('1005,1001', 'BagGold', .03, 1), 'wind': ('1006,1008', 'CraftSpeed', .08, 2),
     'fire': ('1004,1007', 'Harvest', .04, 3), 'rock': ('1008,1010', 'BagHours', .5, 3), 'star': ('1009,1010', 'BagMycelite', .08, 4),
     'coin': ('1001,1009', 'BagGold', .04, 2), 'chest': ('1007,1003', 'BagMycelite', .05, 4), 'beak': ('1009,1010', 'BagMycelite', .11, 5)}
for r in T['kids']:
    eq(f"꼬마 {r['key']}", (','.join(map(str, r['favoriteIds'])), r['bonusEffect'], r['bonusValue'], r['evoStage']), K[r['key']])
S = [(0, 1, 1), (10, 1, 1.3), (30, 5, 1.6), (80, 12, 2.0)]
for r, s in zip(T['stars'], S):
    eq(f"꼬마진화 star{r['star']}", (r['mycelite'], r['needLevel'], r['bagMul']), s)
A = [(300, 0), (600, .02), (900, .04), (1200, .06), (1500, .08), (1800, .10), (2100, .12), (2400, .14), (2700, .16), (0, .18)]
for r, a in zip(T['aff'], A):
    eq(f"호감도 {r['level']}", (r['need'], r['bagBonus']), a)
D = {'leaf_lamp': ('BagGold', .02), 'leaf_house': ('BagHours', 1), 'leaf_crown': ('Harvest', .05), 'dew_spring': ('BagGold', .02),
     'dew_chime': ('BagMycelite', .05), 'dew_pond': ('Hp', .01), 'wind_mill': ('CraftSpeed', .1), 'wind_swing': ('Harvest', .03),
     'wind_catcher': ('CraftSpeed', .05), 'fire_candle': ('Harvest', .03), 'fire_lantern': ('BagGold', .02), 'fire_brazier': ('BagGold', .02),
     'rock_table': ('Harvest', .03), 'rock_moss': ('BagHours', .5), 'rock_geode': ('BagMycelite', .05), 'spark_orb': ('BagGold', .02),
     'spark_bell': ('CritDamage', .01), 'spark_lamp': ('Harvest', .03), 'moon_garland': ('BagGold', .02), 'moon_owl': ('BagMycelite', .03),
     'moon_crescent': ('BagGold', .02), 'star_rune': ('Harvest', .03), 'star_orb': ('BagMycelite', .03), 'star_crown': ('CraftSpeed', .1)}
for r in T['decos']:
    eq(f"장식 {r['key']}", (r['effect'], r['value']), D[r['key']])
G = {0: (1500, 5, 6), 1: (4000, 15, 24), 2: (10000, 30, 48)}
for g, r in T['dgrades'].items():
    eq(f"장식등급 {g}", (r['goldKills'], r['mycelite'], r['hours']), G[g])
for r in T['enemies']:
    eq(f"적 {r['key']}.value", r['value'], 3 if r['index'] in (1009, 1010) else 1)
    eq(f"적 {r['key']}.requestCount", r['requestCount'], 200)
C = dict(treeRingMax=10, treeRingGrowth=1.35, treeRingBagBonus=0.01, treeHarvestPerKill=1, tributePreferMul=2, giftFavoriteMul=3,
         giftMaxCount=300, requestPerDay=3, requestAffinity=200, requestGoldKills=800, requestWeekGoal=14, requestWeekMycelite=60,
         kidBagShare=0.1, decoSetBagMul=0.2, decoFirstMycelite=5, decoFirstAffinity=300, craftRushSeconds=1800, craftRushPerDay=3,
         bagCapHours=12, kidEvoLevelGradeMul=0.7)
for k, v in C.items():
    eq(f'상수 {k}', T['const'].get(k), v)
print('적용 대조: 대조 %d건 →' % n_checked, '전부 일치' if not bad else '불일치 %d' % len(bad))
for b in bad:
    print('  ', b)

# ── 검증기 규칙 (세계수 부분, GameBalanceValidator.cs:183-241 + RequiredConsts) ──
iss = []
eidx = {e['index'] for e in T['enemies']}
for e in T['enemies']:
    if not (0 <= e['value']): iss.append('적 value')
    if not (1 <= e['requestCount']): iss.append('적 requestCount')
for k in T['kids']:
    if k['evoStage'] < 1: iss.append('evoStage')
    if any(i not in eidx for i in k['favoriteIds']): iss.append('favoriteIds')
for r in T['tree']:
    for c in ['evoExp', 'evoGoldKills', 'evoMycelite', 'ringKills', 'bagKillsPerHour', 'bagMycelitePerHour', 'craftSlots',
              'rootSlots', 'trunkSlots', 'branchSlots', 'crownSlots']:
        if r[c] < 0: iss.append('tree neg ' + c)
    if r['stage'] < len(T['tree']) and r['evoExp'] <= 0: iss.append('evoExp 0')
    if any(i not in eidx for i in r['preferIds']): iss.append('preferIds')
for s in T['stars']:
    if s['needLevel'] < 1 or s['mycelite'] < 0 or s['bagMul'] <= 0: iss.append('star')
for a in T['aff']:
    if a['level'] < len(T['aff']) and a['need'] <= 0: iss.append('aff need')
for g in T['dgrades'].values():
    if g['hours'] <= 0: iss.append('deco hours')
kidkeys = {k['key'] for k in T['kids']}
for d in T['decos']:
    if d['owner'] not in kidkeys or d['grade'] not in T['dgrades']: iss.append('deco ref')
REQ = [('treeRingMax', 1, True), ('treeRingGrowth', 1, False), ('giftMaxCount', 1, True), ('requestPerDay', 0, True),
       ('requestAffinity', 0, True), ('requestWeekGoal', 1, True), ('requestWeekMycelite', 0, True), ('decoFirstMycelite', 0, True),
       ('decoFirstAffinity', 0, True), ('craftRushSeconds', 1, False), ('craftRushPerDay', 0, True), ('bagCapHours', 0.001, False),
       ('kidEvoLevelGradeMul', 0.001, False)]
for k, mn, integ in REQ:
    v = T['const'][k]
    if v < mn or (integ and v != math.floor(v)): iss.append('const ' + k)
print('검증기 규칙(세계수·상수):', '통과' if not iss else iss)

# ── 바로 확인할 식 (이론.md 8절) ──
g = T['const']['treeRingGrowth']; n = int(T['const']['treeRingMax']) - 1
print('나이테 합 배수 = %.2f (이론 39.7)' % sum(g ** i for i in range(n)))
gm = T['const']['kidEvoLevelGradeMul']
for st in T['stars'][1:]:
    print('★%d 필요 레벨 등급0~4 (K1 식):' % st['star'], [max(1, math.ceil(st['needLevel'] * gm ** gr - 1e-4)) for gr in range(5)])
# int 열 범위 (long·int 넘침)
print('int 열 최대: evoExp %d, 호감도 합 %d, requestCount %d' % (max(r['evoExp'] for r in T['tree']), sum(a['need'] for a in T['aff']),
      max(e['requestCount'] for e in T['enemies'])))
