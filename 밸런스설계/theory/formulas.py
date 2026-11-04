# 게임 공식 포팅 (IdleProgress.cs · Equipment.cs · GachaProgress.cs) — 표는 GameBalance.xlsx에서 읽는다.
# 오버라이드: set_overrides({'const': {...}, 'upgrade': {stat: {col: val}}, 'stage_fn': f, 'quest_myc': [...]})로 수치안을 시험한다.
import math, os, sys
sys.path.insert(0, os.path.dirname(__file__))
import xlsx_dump

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
STATS = ['Attack', 'Hp', 'HpRegen', 'Speed', 'CritChance', 'CritDamage', 'AdvancedAttack', 'NormalEnemyDamage']
SLOTS = 5
DEFAULT_FORMATION = [0.8, 0.8, 1.0, 0.9, 0.9]   # 꼬마 시트 defaultSlot 0~4 (leaf·dew·fire·rock·coin)


def _rows(sheet):
    hdr = sheet[0]
    return [dict(zip(hdr, r)) for r in sheet[3:] if r and r[0] not in ('',)]


class Tables:
    def __init__(self):
        d = xlsx_dump.load(os.path.join(ROOT, 'Assets/Data/Tables/GameBalance.xlsx'))
        self.up = {}
        for r in _rows(d['업그레이드']):
            self.up[r['stat']] = dict(
                base=float(r['baseValue']), per=float(r['perLevel']), mul=r['valueMode'] == 'Multiply',
                price=float(r['price']), exp=float(r['priceExponent']), div=float(r['priceDivisor']),
                scale=float(r['priceScale'] or 0), maxLv=int(r['maxLevel']), unlockStat=r['unlockStat'],
                unlockLv=int(r['unlockLevel']), capEvery=int(r['capEvery'] or 0), capStep=int(r['capStep'] or 0))
        self.const = {r['key']: float(r['value']) for r in _rows(d['상수'])}
        self.stage = {}
        for r in _rows(d['스테이지']):
            s = int(r['stage'])
            self.stage[s] = dict(waves=int(r['wavesPerStage']), bossTime=float(r['bossTime']),
                                 cb=int(r['enemyCountBase']), cw=int(r['enemyCountPerWave']), cs=int(r['enemyCountPerStage']),
                                 cmax=int(r['enemyCountMax']), gold=int(r['enemyGold']), bossGold=int(r['bossGold']),
                                 myc=int(r['bossMycelite']), atk=float(r['enemyAttack']), bossAtk=float(r['bossAttack']))
        self.tableMax = max(self.stage)
        self.quests = [dict(kind=r['kind'], target=int(r['target']), goldKills=int(r['goldKills']), myc=int(r['mycelite']))
                       for r in _rows(d['퀘스트'])]
        # 등급 4개 (0 일반·1 희귀·2 영웅·3 전설, 2026-11-04 고급 삭제) — 확률 열도 4개
        self.gacha = [dict(exp=int(r['expToNext']), odds=[float(r[k]) for k in GRADE_ODDS]) for r in _rows(d['뽑기'])]
        self.ownBase = [float(r['ownBase']) for r in sorted(_rows(d['등급']), key=lambda r: int(r['grade']))]
        # 장비 배치: 종류별 itemIndex 순서 (grade, sub). 등급마다 장비 수가 다르다 (무기·방어구 4/3/2/1, 펫 3/3/3/3)
        self.equip = {}
        for r in sorted(_rows(d['장비']), key=lambda r: int(r['itemIndex'])):
            self.equip.setdefault(r['kind'], []).append((int(r['grade']), int(r['sub'])))
        self.dungeon = {int(r['dungeon']): int(r['unlockStage']) for r in _rows(d['던전'])}
        self.ggoma = [dict(grade=int(r['grade']), mul=float(r['attackMultiplier']), owned=int(r['initialOwned']))
                      for r in _rows(d['꼬마'])]


GRADE_ODDS = ('oddsNormal', 'oddsRare', 'oddsEpic', 'oddsLegend')
T = Tables()
OV = {}


def set_overrides(ov):
    """수치안 적용. None이면 원래 표로."""
    global OV
    OV = ov or {}
    _cache.clear()


def C(key):
    return OV.get('const', {}).get(key, T.const[key])


def U(stat):
    u = T.up[stat]
    o = OV.get('upgrade', {}).get(stat)
    return {**u, **o} if o else u


def stage_row(s):
    """표 안이면 표 값(오버라이드 stage_fn 우선), 밖이면 None"""
    f = OV.get('stage_fn')
    if f:
        r = f(s)
        if r is not None:
            return r
    if s <= T.tableMax:
        return T.stage[s]
    return None


# ── 강화 ──
def cost(stat, lv):
    u = U(stat)
    c = u['price'] * (1 + lv / u['div']) ** u['exp'] * OV.get('cost_mul', 1.0)   # D5: 수입이 는 만큼 비용 baseCost × cost_mul
    if u['scale'] > 0:
        c *= math.exp(lv / u['scale'])
    return min(9e18, math.ceil(c))


def value(stat, lv):
    u = U(stat)
    lv = max(0, min(lv, u['maxLv']))
    if u.get('vexp'):      # 수치안(기준 7): valueMode Power = baseValue × (1 + Lv/valueDivisor)^valueExponent
        return u['base'] * (1 + lv / u['vdiv']) ** u['vexp']
    return u['base'] * (1 + u['per'] * lv) if u['mul'] else u['base'] + u['per'] * lv


def cap_level(stat, gate_lv):
    u = U(stat)
    if u['capEvery'] <= 0:
        return u['maxLv']
    if gate_lv < u['unlockLv']:
        return 0
    return min(u['maxLv'], ((gate_lv - u['unlockLv']) // u['capEvery'] + 1) * u['capStep'])


# ── 기대 성장 곡선 ──
_cache = {}


def expected_level(stat, stage):
    key = (stat, stage)
    if key in _cache:
        return _cache[key]
    s = max(0, stage - 1)
    u = U(stat)
    lps = int(C('levelsPerStage'))
    if stat == 'Speed':
        v = min(u['maxLv'], int(C('expSpeedPerStage') * s))
    elif stat == 'HpRegen':
        v = min(u['maxLv'], int(lps * s * C('expRegenRatio')))
    elif stat in ('CritChance', 'CritDamage', 'AdvancedAttack'):
        ratio = C({'CritChance': 'expCritChanceRatio', 'CritDamage': 'expCritDamageRatio',
                   'AdvancedAttack': 'expAdvancedAttackRatio'}[stat])
        gate = expected_level(u['unlockStat'], stage)
        if gate < u['unlockLv']:
            v = 0
        elif u['capEvery'] <= 0:
            v = u['maxLv'] if ratio > 0 else 0
        else:
            cap = ((gate - u['unlockLv']) / u['capEvery'] + 1) * u['capStep']
            v = int(min(u['maxLv'], cap * ratio))
    elif stat == 'NormalEnemyDamage':
        div = max(1, int(C('expNormalDamageDivisor')))
        v = max(0, min(u['maxLv'], int((expected_level(u['unlockStat'], stage) - u['unlockLv']) / div)))
    else:
        v = min(u['maxLv'], lps * s)
    _cache[key] = v
    return v


def expected_hit(s):
    cc = min(1, value('CritChance', expected_level('CritChance', s)))
    cd = value('CritDamage', expected_level('CritDamage', s))
    return (value('Attack', expected_level('Attack', s)) * (1 + C('expAttackMulPerStage') * s)
            * value('AdvancedAttack', expected_level('AdvancedAttack', s)) * (1 + cc * (cd - 1)))


def expected_dps(s):
    return expected_hit(s) * SLOTS * value('Speed', expected_level('Speed', s))


def expected_hp(s):
    return value('Hp', expected_level('Hp', s)) * (1 + C('expHpMulPerStage') * s)


def expected_regen(s):
    return value('HpRegen', expected_level('HpRegen', s)) * (1 + C('expHpMulPerStage') * s)


# ── 적 ──
def waves(s):
    r = stage_row(s) or T.stage[T.tableMax]
    return max(1, r['waves'])


def boss_time(s):
    r = stage_row(s) or T.stage[T.tableMax]
    return max(1.0, r['bossTime'])


def enemy_count(s, w):
    r = stage_row(s) or T.stage[T.tableMax]
    return max(r['cb'], min(r['cmax'], r['cb'] + r['cw'] * (w - 1) + r['cs'] * (s - 1)))


def enemy_hp(s, w):
    r = stage_row(s)
    if r is not None and 'ehp' in r:      # 수치안: 스테이지 시트 enemyHp 열 (코드 변경 후)
        return r['ehp'] * (1 + C('enemyHpWaveGrowth') * (w - 1))
    k = OV.get('enemy_hp_mul', lambda s: 1.0)(s)
    return (expected_hit(s) * value('NormalEnemyDamage', expected_level('NormalEnemyDamage', s))
            * C('enemyHpHits') * (1 + C('enemyHpWaveGrowth') * (w - 1))) * k


def boss_hp(s):
    r = stage_row(s)
    if r is not None and 'bhp' in r:      # 수치안: 스테이지 시트 bossHp 열 (코드 변경 후)
        return r['bhp']
    k = OV.get('boss_hp_mul', lambda s: 1.0)(s)
    return expected_dps(s) * C('bossKillSeconds') * k


def enemy_attack(s):
    r = stage_row(s)
    if r is not None:
        return r['atk']
    return C('enemyAtkHpRate') * expected_hp(s) + C('enemyAtkRegenRate') * expected_regen(s)


def boss_attack(s):
    r = stage_row(s)
    if r is not None:
        return r['bossAtk']
    return expected_regen(s) + C('bossAtkHpRate') * expected_hp(s)


def enemy_gold(s):
    r = stage_row(s)
    if r is not None:
        return r['gold']
    return max(1, round(C('goldBase') * (1 + (s - 1) / C('goldStep')) ** C('goldExp')))


def boss_gold(s):
    r = stage_row(s)
    if r is not None:
        return r['bossGold']
    return min(9e18, enemy_gold(s) * int(C('bossGoldMul')))


def boss_myc(s):
    r = stage_row(s)
    if r is not None:
        return r['myc']
    return int(C('mycelBase')) + s // max(1, int(C('mycelStageDiv')))


def run_units(s):
    """한 판 골드를 처치당 골드 단위로 (웨이브 적 수 합 + 보스 배수)"""
    return sum(enemy_count(s, w) for w in range(1, waves(s) + 1)) + boss_gold(s) / max(1, enemy_gold(s))


# ── 장비 (보유 효과) ──
def base_own_percent(grade, sub, size):
    """Equipment.BaseOwnPercent: 등급 기본값 사이를 그 등급 장비 수(size)로 나눠 잇는다"""
    ob = OV.get('ownBase', T.ownBase)
    frm = ob[grade]
    to = ob[grade + 1] if grade + 1 < len(ob) else frm * C('equipTopGradeMul')
    return frm * (to / frm) ** (sub / max(1, size))


def equip_items(kind):
    """장비 시트 배치 → [(grade, sub, 그 등급 장비 수)] (itemIndex 순서)"""
    items = T.equip[kind]
    size = {}
    for g, _ in items:
        size[g] = size.get(g, 0) + 1
    return [(g, s, size[g]) for g, s in items]


def equip_base(kind, i):
    g, s, n = equip_items(kind)[i]
    return base_own_percent(g, s, n)


def level_from_copies(n):
    """총 장수 n(첫 장 포함) → 합성으로 오를 수 있는 레벨 (copiesBase + copiesPerLevel × lv)"""
    if n < 1:
        return 0
    lv, left = 1, n - 1
    cb, cp, mx = C('copiesBase'), C('copiesPerLevel'), C('equipMaxLevel')
    while lv < mx and left >= cb + cp * lv:
        left -= cb + cp * lv
        lv += 1
    return lv
