# 11-04 펫 삭제 보정 (manager#19). 라이브 표(MainTable.xlsx) 그대로 진행 모델을 돌린다 — 생성기(gen_stage) 없이 표 값.
#   python run_pet.py                 → 기준선(펫 있음, 7b17e68 장비 배치) · 지금(펫 없음) · 후보 손잡이들, 무광고/보통/많이
#   python run_pet.py cfg/pet.json    → 설정 파일의 후보만
# 결과: out_pet.json, 화면 표. 난수 없음 — 같은 표·설정이면 같은 결과.
#
# 모델이 게임과 같게 맞춘 것 (라이브 표·코드 11-04):
#   업그레이드 Power(valueExponent·valueDivisor), 공속 비용 가중(speedCostRamp*), 스테이지 enemyHp·bossHp 열(표 우선, C1),
#   표 끝 뒤 꼬리(TailRow 699 × tail*^(s−699), 챕터 ×tailWallMul), 등급 4개 ownBase 2/50/250/1250, 뽑기 레벨 확률 4열,
#   장비 무기·방어구 4/3/2/1 (펫 3/3/3/3 = 7b17e68 장비 시트 11021~11032), 편성 꼬마 0.9×5, 퀘스트 300/3/20/11/3·75×4+50,
#   던전은 구현된 던전 1만(기준 s5, 레벨당 +10, rewardKills 12000, 소탕 ×0.5).
# 정책: 균사석 뽑기 = 종류를 돌아가며(펫 있음 무기·펫·방어구·꼬마 / 없음 무기·방어구·꼬마 — test 시뮬 SpendMycelite와 같다),
#   광고 뽑기 = 앞에서 round(종류 수 × 시청률)종 (펫 있음 보통 = 무기·펫 / 없음 보통 = round(1.5)=2 → 무기·방어구).
import sys, os, json, math, copy
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import formulas as F
import progress as P
import dungeon
import xlsx_dump

HERE = os.path.dirname(os.path.abspath(__file__))
PET_ITEMS = [(g, s) for g in range(4) for s in range(3)]     # 7b17e68 장비 시트 pet_00~11: 등급 0~3 × 단계 0~2
ADS = {'무광고': 0.0, '보통': 0.5, '많이': 1.0}
DAYS = 31


def live_rows(path=None):
    d = xlsx_dump.load(path or os.path.join(F.ROOT, 'Assets/Data/Tables/MainTable.xlsx'))
    h = d['스테이지'][0]
    rows = {}
    for r in d['스테이지'][3:]:
        r = dict(zip(h, r))
        s = int(r['stage'])
        rows[s] = dict(waves=int(r['wavesPerStage']), bossTime=float(r['bossTime']), cb=int(r['enemyCountBase']),
                       cw=int(r['enemyCountPerWave']), cs=int(r['enemyCountPerStage']), cmax=int(r['enemyCountMax']),
                       gold=int(r['enemyGold']), bossGold=int(r['bossGold']), myc=int(r['bossMycelite']),
                       atk=float(r['enemyAttack']), bossAtk=float(r['bossAttack']), ehp=float(r['enemyHp']), bhp=float(r['bossHp']))
    hu = d['업그레이드'][0]
    up = {}
    for r in d['업그레이드'][3:]:
        r = dict(zip(hu, r))
        if r['valueMode'] == 'Power':
            up[r['stat']] = {'vexp': float(r['valueExponent']), 'vdiv': float(r['valueDivisor'])}
    return rows, up


def setup(hp_mul=None, path=None):
    """hp_mul: s → 적·보스 체력 배수 (손잡이 A: 스테이지 시트 enemyHp·bossHp × k(s)). None = 표 그대로"""
    rows, up = live_rows(path)
    if hp_mul:
        rows = copy.deepcopy(rows)
        for s, r in rows.items():
            k = hp_mul(s)
            r['ehp'] *= k; r['bhp'] *= k
    end = max(rows)
    tail_s = end - 1 if end % 10 == 0 else end
    T = rows[tail_s]
    c = F.T.const
    cache = {}

    def stage_fn(s):
        if s <= end:
            return rows[s]
        if s not in cache:
            n = s - tail_s
            r = dict(T)
            r['ehp'] = T['ehp'] * c['tailEnemyHpGrowth'] ** n
            r['bhp'] = T['bhp'] * c['tailBossHpGrowth'] ** n * (c['tailWallMul'] if s % 10 == 0 else 1)
            r['atk'] = T['atk'] * c['tailAttackGrowth'] ** n
            r['bossAtk'] = T['bossAtk'] * c['tailAttackGrowth'] ** n
            r['gold'] = max(1, round(T['gold'] * c['tailGoldGrowth'] ** n))
            r['bossGold'] = r['gold'] * int(c['bossGoldMul'])
            cache[s] = r
        return cache[s]

    F.set_overrides({'upgrade': up, 'stage_fn': stage_fn, 'quest_stage_boss': True, 'formation': [0.9] * 5})
    # 던전: 구현된 던전 1만 (던전 시트 9101)
    dungeon.KINDS = {1}
    dungeon.DUNGEONS[1].update(baseStage=5, stageStep=10, rewardKills=12000, hpMul=1.0)
    dungeon.SWEEP_MUL = c['dungeonSweepMul']
    return rows


_base_cost = F.cost


def cost(stat, lv):
    """IdleProgress.Cost + SpeedCostRamp (공속 Lv 87 뒤 비용 가중)"""
    c = _base_cost(stat, lv)
    if stat == 'Speed':
        mx = F.U('Speed')['maxLv']
        frm = int(F.C('speedCostRampFromLv'))
        if lv > frm and mx - 1 > frm:
            c = min(9e18, math.ceil(c * (1 + F.C('speedCostRamp') * (min(lv, mx - 1) - frm) / (mx - 1 - frm))))
    return c


F.cost = cost


def mode(pet, atk_mul=1.0, own_mul=1.0, ad_kinds=None):
    """pet: 펫 있음(7b17e68) / 없음(지금). atk_mul: 무기 보유 효과 배율(손잡이 B, 코드 상수). own_mul: 등급 ownBase 전체 배율(손잡이 C)"""
    if pet:
        F.T.equip['Pet'] = list(PET_ITEMS)
        P.Gear.KINDS = ['Weapon', 'Pet', 'Accessory']
        P.Sim.MYC_ROT = ['Weapon', 'Pet', 'Accessory', 'Character']
        P.Sim.AD_ORDER = ['Weapon', 'Pet', 'Accessory', 'Character']
    else:
        F.T.equip.pop('Pet', None)
        P.Gear.KINDS = ['Weapon', 'Accessory']
        P.Sim.MYC_ROT = ['Weapon', 'Accessory', 'Character']
        P.Sim.AD_ORDER = ad_kinds or ['Weapon', 'Accessory', 'Character']
    P.Gear.ATK_KINDS = ('Weapon', 'Pet')
    P.Gear.ATK_MUL = atk_mul
    ov = dict(F.OV)
    ov['ownBase'] = [x * own_mul for x in F.T.ownBase]
    F.OV.clear(); F.OV.update(ov)
    F._cache.clear()


def run(ads, days=DAYS):
    sim = P.Sim(pattern='daily', ads=ads)
    sim.dungeon = dungeon
    sim.dungeon_after_play = True
    by_day, gear = [], []
    for d in range(days):
        sim.run_days(1)
        by_day.append(sim.stage)
        g = sim.gear
        gear.append(dict(w=round(g.percent('Weapon')), p=round(g.percent('Pet')) if 'Pet' in g.KINDS else 0,
                         a=round(g.percent('Accessory')), atk=round(1 + g.atk_pct() / 100, 2), hp=round(1 + g.hp_pct() / 100, 2),
                         lv={k: g.level[k] for k in g.level}))
    return sim, by_day, gear


def waits(sim):
    """스테이지마다 머문 플레이 초 [(s, 도착 플레이 초, 도착 달력 초, 머문 플레이 초)]"""
    st = sorted(sim.arrive)
    out = []
    for a, b in zip(st, st[1:]):
        out.append((a, sim.arrive[a][0], sim.arrive[a][1], sim.arrive[b][0] - sim.arrive[a][0]))
    return out


def summary(sim, by_day, gear):
    w = waits(sim)
    first_hour = [(s, round(t0 / 60, 1), round(dt / 60, 1)) for s, t0, _, dt in w if t0 < 3600 and dt >= 300]
    stage_1h = max(s for s, (t0, _) in sim.arrive.items() if t0 <= 3600)
    hurdle = {}
    for m in (5, 30, 60):
        hit = next(((s, round(t0 / 60), round(c0 / 86400, 2)) for s, t0, c0, dt in w if dt >= m * 60), None)
        hurdle[m] = hit
    D = lambda d: by_day[d] if d < len(by_day) else None
    return dict(stage_1h=stage_1h, first_hour_5min=first_hour, D0=D(0), D1=D(1), D3=D(3), D7=D(7), D30=D(30),
                hurdle=hurdle, walls=[(s, round(dt / 60), round(c0 / 86400, 2)) for s, t0, c0, dt in w if dt >= 15 * 60],
                gear={d: gear[d] for d in (0, 1, 3, 7, 30) if d < len(gear)},
                myc_per_day={k: round(v / len(by_day), 1) for k, v in sim.myc_src.items()},
                pulls={k: v for k, v in sim.gear.pulls.items()})


def case(name, pet, ads_list=ADS, **kw):
    out = {}
    for an, a in ads_list.items():
        mode(pet, atk_mul=kw.get('atk_mul', 1.0), own_mul=kw.get('own_mul', 1.0), ad_kinds=kw.get('ad_kinds'))
        sim, by_day, gear = run(a, kw.get('days', DAYS))
        out[an] = summary(sim, by_day, gear)
        r = out[an]
        print(f"{name:14s} {an}: 1h s{r['stage_1h']} 5분↑{r['first_hour_5min']} D0 {r['D0']} D1 {r['D1']} D3 {r['D3']} D7 {r['D7']} D30 {r['D30']} "
              f"허들5/30/60 {r['hurdle'][5]} {r['hurdle'][30]} {r['hurdle'][60]} | 공격× D1 {r['gear'].get(1, {}).get('atk')} D7 {r['gear'].get(7, {}).get('atk')} D30 {r['gear'].get(30, {}).get('atk')}"
              f" 체력× D7 {r['gear'].get(7, {}).get('hp')} D30 {r['gear'].get(30, {}).get('hp')}", flush=True)
    return out


def seg_mul(points):
    """[(s, k), …] 구간 배수: 스테이지 사이 로그 보간, 첫 점 앞은 1(무강화 구간 보호), 끝 점 뒤는 끝 값"""
    def f(s):
        if s <= points[0][0]:
            return 1.0 if s <= 5 else points[0][1]
        for (a, ka), (b, kb) in zip(points, points[1:]):
            if s <= b:
                return math.exp(math.log(ka) + (math.log(kb) - math.log(ka)) * (s - a) / (b - a))
        return points[-1][1]
    return f


if __name__ == '__main__':
    cfg = json.load(open(sys.argv[1], encoding='utf-8')) if len(sys.argv) > 1 else {}
    res = {}
    ads = {k: v for k, v in ADS.items() if k in cfg.get('ads', list(ADS))}
    if not cfg.get('skip_base'):
        setup()
        res['기준선(펫)'] = case('기준선(펫)', True, ads)
        res['지금(펫 삭제)'] = case('지금(펫 삭제)', False, ads)
    for c in cfg.get('cases', []):
        setup(seg_mul(c['hp_points']) if c.get('hp_points') else None)
        res[c['name']] = case(c['name'], False, ads, atk_mul=c.get('atk_mul', 1.0), own_mul=c.get('own_mul', 1.0), ad_kinds=c.get('ad_kinds'))
    out = os.path.join(HERE, cfg.get('out', 'out_pet.json'))
    json.dump(res, open(out, 'w', encoding='utf-8'), ensure_ascii=False, indent=1, default=str)
