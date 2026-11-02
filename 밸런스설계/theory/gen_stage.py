# 수치안: 스테이지 시트 1~1000을 다시 만든다 (enemyHp·bossHp 새 열 + 적 수·공격·골드·균사석).
# 생각: 적 강도 = "목표 날짜 d(s)에 도착한 무광고 보통 플레이어"의 전력 × 벽 배수.
#   - 강화 레벨은 지금 기대 곡선(levelsPerStage 15 등, 공속 상한 275 반영)
#   - 장비 보유 효과는 1+0.012s 대신 그 날짜까지 뽑은 수(하루 REF_PULLS)로 계산한 값
#   - 적 공격은 동시에 있는 적 수를 고려해 CROWD로 나눈다 (웨이브 사이 체력 회복 없음)
#   - 골드는 그 목표 날짜에 곡선 레벨을 살 수 있게 하루 골드(앱 밖 포함)에서 역산한 배수 m(s) (≤ 1)
# 같은 입력이면 같은 표.
import math
import formulas as F
from progress import Gear

PARAMS = dict(
    target_a=80.0, target_b=60.0,      # 목표: 하루 d 끝 스테이지 = a + b·ln(d)  (무광고 보통 하루) — 장비 기준 날짜에만 쓴다
    gold_q=1.0,                        # 기준 7: 골드 × (1 + 1.5(s-1))^q (공격력·체력 비용 지수 +q와 짝)
    gold_m_start=10, gold_m_end=40, gold_m_inf=0.1,   # 골드 배수: start까지 1 → end까지 로그로 줄어 m_inf
    ref_pulls=55,                      # 보통 구간 하루 장비 뽑기: 퀘스트 35 + 광고 뽑기 2종×4 + 던전·상자·보스 균사석 약 12
    ref_form=4.4,                      # 기준 편성 배율 합 (기본 편성)
    day_seconds=22.5 * 3600,           # 하루 골드 시간: 플레이 1.5h + 앱 밖 21h (상한 12h 기준)
    run_seconds=35.0,                  # 한 판 평균 시간 (시뮬 실측 근사)
    wall_every=10, wall_mul=2.0,       # 10스테이지마다(챕터 보스) 보스 체력 ×2.0 → 기준 플레이어 보스 시간 36초(>30초) = 벽
    first_wall=10,
    crowd=1.0, target_hp=0.3,         # 적 공격은 calibrate_attack이 k(s)로 맞춘다                         # 적 공격 ÷ 이 값 (동시에 4마리 몫을 1마리 몫으로)
    boss_atk_div=1.0,
    myc_normal=1, myc_wall=20,         # 보스 균사석: 보통 1, 챕터 보스 20
    early=5,                           # 1~early는 무강화 구간
)

_gear_cache = {}


def ref_gear(day):
    """무광고 기준 플레이어가 day일까지 뽑은 장비의 공격 %, 체력 % (하루 ref_pulls, 종류 3개에 고르게)"""
    n = int(PARAMS['ref_pulls'] * min(3650.0, max(0.0, day)))
    n -= n % 3
    if n in _gear_cache:
        return _gear_cache[n]
    g = Gear()
    per = n // 3
    for k in Gear.KINDS:
        left = per
        while left > 0:
            c = min(left, 100)
            g.pull(k, c)
            left -= c
    r = (g.atk_pct(), g.hp_pct())
    _gear_cache[n] = r
    return r


def target_day(s):
    tbl = PARAMS.get('day_table')       # 보정: 이전 실행의 실제 도착 일수 (iterate.py)
    if tbl:
        if s in tbl:
            return tbl[s]
        last = max(tbl)
        if s > last:
            return tbl[last] * math.exp((s - last) / PARAMS['target_b'])
        lo = max(k for k in tbl if k <= s) if any(k <= s for k in tbl) else min(tbl)
        return tbl[lo]
    a, b = PARAMS['target_a'], PARAMS['target_b']
    return math.exp((s - a) / b) if s > a else max(0.0, (s - PARAMS['early']) / (a - PARAMS['early']))



def ref_levels(s):
    return {st: F.expected_level(st, s) for st in F.STATS}


def ref_hit_dps(s):
    lv = ref_levels(s)
    atk_pct, hp_pct = ref_gear(target_day(s))
    cc = min(1, F.value('CritChance', lv['CritChance']))
    cd = F.value('CritDamage', lv['CritDamage'])
    hit = F.value('Attack', lv['Attack']) * (1 + atk_pct / 100) * F.value('AdvancedAttack', lv['AdvancedAttack']) * (1 + cc * (cd - 1))
    dps = hit * PARAMS['ref_form'] * F.value('Speed', lv['Speed'])
    hp = F.value('Hp', lv['Hp']) * (1 + hp_pct / 100)
    regen = F.value('HpRegen', lv['HpRegen']) * (1 + hp_pct / 100)
    return hit, dps, hp, regen, lv


def need_gold(s):
    """곡선 레벨까지 누적 강화 비용 (모든 강화)"""
    lv = ref_levels(s)
    tot = 0.0
    for st in F.STATS:
        tot += cum_cost(st, lv[st])
    return tot


_cum = {}


def cum_cost(st, L):
    key = (st, L)
    if key in _cum:
        return _cum[key]
    # 1000 단위로 캐시
    base = (L // 200) * 200
    prev = _cum.get((st, base))
    if prev is None:
        prev = 0.0
        for b in range(0, base, 200):
            prev += sum(F.cost(st, x) for x in range(b, b + 200))
            _cum[(st, b + 200)] = prev
    v = prev + sum(F.cost(st, x) for x in range(base, L))
    _cum[key] = v
    return v


def gold_mul(s, units, eg):
    """목표 속도 ds/dd에서 하루 골드로 곡선을 따라가게 하는 골드 배수 (1 이하)"""
    if s <= PARAMS['target_a']:
        return 1.0
    b = PARAMS['target_b']
    d = target_day(s)
    dsdd = b / d
    dneed = (need_gold(s + 1) - need_gold(s))
    per_day = PARAMS['day_seconds'] / PARAMS['run_seconds'] * units * eg
    return min(1.0, dneed * dsdd / per_day)


def gold_ramp(s):
    a, b, inf = PARAMS['gold_m_start'], PARAMS['gold_m_end'], PARAMS['gold_m_inf']
    if s <= a:
        return 1.0
    if s >= b:
        return inf
    return math.exp(math.log(inf) * (s - a) / (b - a))


CHECK = [6, 8, 11, 15, 21, 31, 51, 81, 121, 181, 186, 251, 301, 401, 501, 701, 1000]


def check_points():
    """check_step 0 = 처음 17곳(v1), n = 6·8 + n간격 (챕터 보스 자리는 +1)"""
    step = PARAMS.get('check_step', 0)
    if not step:
        return CHECK
    pts = [6, 8] + [s + 1 if s % 10 == 0 else s for s in range(11, 1000, step)] + [1000]
    return sorted(set(pts))


def ref_player(s):
    import battle as B
    hit, dps, hp, regen, lv = ref_hit_dps(s)
    a, h = ref_gear(target_day(s))
    return B.Player(lv, atk_pct=a, hp_pct=h)


def calibrate_attack(rows):
    """기준 플레이어(목표 날짜의 곡선 레벨·장비, 기본 편성)가 보통 스테이지를 체력 TARGET_HP만 남기고 깨도록
    적·보스 공격에 곱할 k(s)를 체크 지점마다 이분 탐색 → 로그 보간 (웨이브 사이 체력 회복 없음 반영)"""
    import battle as B
    import formulas as F
    ks = {}
    checks = check_points()
    for c in checks:
        p = ref_player(c)
        if not PARAMS.get('atk_hp_gear', True):     # 적 공격을 강화(체력·재생 레벨)만으로 보정: 장비 체력 % = 0인 기준 플레이어
            p.hp_pct = 0.0
        lo, hi = 1e-4, 4.0
        a0, b0 = rows[c]['atk'], rows[c]['bossAtk']
        for _ in range(22):
            mid = math.sqrt(lo * hi)
            rows[c]['atk'], rows[c]['bossAtk'] = a0 * mid, b0 * mid
            r = B.attempt(c, p)
            if r.ok and r.min_hp >= PARAMS['target_hp']:
                lo = mid
            else:
                hi = mid
        rows[c]['atk'], rows[c]['bossAtk'] = a0, b0
        ks[c] = lo if lo > 2e-4 or not ks else ks[max(ks)]     # 기준 플레이어가 공격 0이어도 못 깨면(보스 시간) 앞 값을 쓴다
    PARAMS['_k'] = ks
    for s in range(checks[0], F.T.tableMax + 1):
        lo_c = max(c for c in checks if c <= s)
        hi_c = min([c for c in checks if c >= s] or [checks[-1]])
        if hi_c == lo_c:
            k = ks[lo_c]
        else:
            t = (s - lo_c) / (hi_c - lo_c)
            k = math.exp(math.log(ks[lo_c]) * (1 - t) + math.log(ks[hi_c]) * t)
        rows[s]['atk'] *= k
        rows[s]['bossAtk'] *= k


def make_rows(params=None):
    if params:
        PARAMS.update(params)
    _gear_cache.clear()
    rows = {}
    k_hits, k_boss = F.C('enemyHpHits'), F.C('bossKillSeconds')
    prev_m = 1.0
    for s in range(1, F.T.tableMax + 1):
        base = dict(F.T.stage[s])
        if s <= PARAMS['early']:
            # 무강화 구간: 적 3~7마리, 체력·공격 평평하게 조금씩 (theory/model.py c1 검사로 맞춤)
            base.update(cb=3, cw=1, cs=0, cmax=7)
            base['ehp'] = 14.0 + 1.5 * (s - 1)
            base['bhp'] = 520.0 + 40 * (s - 1)
            base['atk'] = 0.55 + 0.03 * (s - 1)
            base['bossAtk'] = 2.0 + 0.1 * (s - 1)
            base['myc'] = PARAMS['myc_normal']
            base['gold'] = max(1, round(base['gold'] * (1 + 1.5 * (s - 1)) ** PARAMS['gold_q']))
            base['bossGold'] = base['gold'] * 20
            rows[s] = base
            continue
        hit, dps, hp, regen, lv = ref_hit_dps(s)
        ned = F.value('NormalEnemyDamage', lv['NormalEnemyDamage'])
        wall = PARAMS['wall_mul'] if (s >= PARAMS['first_wall'] and s % PARAMS['wall_every'] == 0) else 1.0
        base['ehp'] = hit * ned * k_hits
        base['bhp'] = dps * k_boss * wall
        base['atk'] = (F.C('enemyAtkHpRate') * hp + F.C('enemyAtkRegenRate') * regen) / PARAMS['crowd']
        base['bossAtk'] = (regen + F.C('bossAtkHpRate') * hp) / PARAMS['boss_atk_div']
        eg0 = base['gold']
        m = gold_ramp(s)
        base['gold'] = max(1, round(eg0 * (1 + 1.5 * (s - 1)) ** PARAMS['gold_q'] * m))
        base['bossGold'] = base['gold'] * 20
        base['myc'] = PARAMS['myc_wall'] if s % PARAMS['wall_every'] == 0 else PARAMS['myc_normal']
        base['m'] = m
        rows[s] = base
    import formulas as F2
    F2.OV['stage_fn'] = lambda x: rows.get(x)      # 보정 시뮬이 새 행을 쓰게
    calibrate_attack(rows)
    # 골드 단조 보정
    for s in range(2, F.T.tableMax + 1):
        if rows[s]['gold'] <= rows[s - 1]['gold']:
            rows[s]['gold'] = rows[s - 1]['gold'] + 1
            rows[s]['bossGold'] = rows[s]['gold'] * 20
    return rows


if __name__ == '__main__':
    F.set_overrides({'upgrade': {'Speed': {'maxLv': 275}}})
    rows = make_rows()
    for s in [1, 5, 6, 10, 20, 40, 50, 100, 184, 185, 200, 300, 500, 1000]:
        r = rows[s]
        print(s, f"d={target_day(s):.2f} ehp={r['ehp']:.4g} bhp={r['bhp']:.4g} atk={r['atk']:.4g} batk={r['bossAtk']:.4g} gold={r['gold']} m={r.get('m',1):.3g} gear={ref_gear(target_day(s))}")
