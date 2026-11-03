# -*- coding: utf-8 -*-
"""verify 독립 포팅 (2026-11-02): 방치형 공식·표를 C# 원본에서 직접 옮긴다. test/theory 코드는 보지 않았다.

출처 (파일:줄, Assets/Scripts/Mobile/ 기준)
- 표: Assets/Data/Tables/GameBalance.xlsx (= Resources/Tables/GameBalance.bytes, md5 같음 확인 11:21)
- 강화 값/비용: Idle/IdleProgress.cs:143-149 Cost, :167-172 Value, :121-128 CapLevel, :110-114 Unlocked
- 기대 곡선: IdleProgress.cs:229-279 (ExpectedLevel/ExpectedCapped/ExpectedHit/Dps/Hp/Regen)
- 적: IdleProgress.cs:283-301 (EnemyCount/EnemyHp/BossHp/EnemyAttack/BossAttack), :309-325 (골드·균사석)
- 표 밖 행: Data/GameBalance.cs:65-75 (Stage = 마지막 행으로 clamp, InStageTable)
- 편성: Idle/Formation.cs:11 (SlotCount 5), :294-300 (AttackMultiplier), 표 '꼬마' defaultSlot/initialOwned
- 뽑기 가격: UI/GachaPanel.cs:35-36 기본값 100/300, Prefabs/Mobile/Gacha_Panel/Gacha_Panel1.prefab cost11 100 cost35 300 currency 1(균사석),
  Prefabs/Mobile/Main/GachaUI.prefab 덮어쓰기는 kind만 (cost 덮어쓰기 없음), Main.unity에 cost 덮어쓰기 0건
- 퀘스트: Quest/DailyQuests.cs:83-88 (골드 = EnemyGold(지금 스테이지)×goldKills), :128-129, 표 '퀘스트'
"""
import math
import os
import openpyxl

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
XLSX = os.path.join(ROOT, 'Assets', 'Data', 'Tables', 'GameBalance.xlsx')

STATS = ['Attack', 'Hp', 'HpRegen', 'Speed', 'CritChance', 'CritDamage', 'AdvancedAttack', 'NormalEnemyDamage']
SLOT_COUNT = 5  # Formation.cs:11


def _sheet_rows(ws):
    rows = list(ws.iter_rows(values_only=True))
    head = rows[0]
    out = []
    for r in rows[3:]:  # 1행 키, 2행 자료형, 3행 한글 설명 (info 시트 설명)
        if r[0] is None:
            continue
        out.append({h: v for h, v in zip(head, r) if h is not None})
    return out


class Tables:
    def __init__(self, path=XLSX):
        wb = openpyxl.load_workbook(path, data_only=True)
        names = wb.sheetnames
        # 시트 이름은 한글 — 열 이름으로 찾는다
        by_head = {}
        for ws in wb.worksheets:
            first = next(ws.iter_rows(values_only=True))
            by_head[tuple(x for x in first if x is not None)] = ws
        def find(col):
            for h, ws in by_head.items():
                if col in h:
                    return ws
            raise KeyError(col)
        self.upgrades = {r['stat']: r for r in _sheet_rows(find('priceExponent'))}
        self.stages = _sheet_rows(find('wavesPerStage'))
        self.stages.sort(key=lambda r: r['stage'])
        self.consts = {r['key']: float(r['value']) for r in _sheet_rows(find('value'))}
        self.mascots = _sheet_rows(find('attackMultiplier'))
        self.grades = _sheet_rows(find('ownBase'))
        self.quests = _sheet_rows(find('goldKills'))
        self.dungeons = _sheet_rows(find('unlockStage'))


T = Tables()


def C(key):
    return T.consts[key]


def CI(key):  # GameBalance.cs:55 ConstInt = (int)Math.Round
    return int(cs_round(C(key)))


def cs_round(x):  # Math.Round 기본 = 은행가 반올림 (Python round와 같음)
    return float(round(x))


def clamp(v, lo, hi):
    return max(lo, min(hi, v))


def stage_row(stage):  # GameBalance.cs:65-69
    return T.stages[clamp(stage, 1, len(T.stages)) - 1]


def in_table(stage):  # GameBalance.cs:71-75
    return 1 <= stage <= len(T.stages)


# ── 강화 ──
def d(stat):
    return T.upgrades[stat]


def value(stat, lv):  # IdleProgress.cs:167-172
    r = d(stat)
    lv = clamp(lv, 0, r['maxLevel'])
    if r['valueMode'] == 'Multiply':
        return r['baseValue'] * (1 + r['perLevel'] * lv)
    return r['baseValue'] + r['perLevel'] * lv


def cost(stat, lv):  # IdleProgress.cs:143-149
    r = d(stat)
    c = r['price'] * math.pow(1 + lv / r['priceDivisor'], r['priceExponent'])
    if r['priceScale'] > 0:
        x = lv / r["priceScale"]
        c = float("inf") if x > 700 else c * math.exp(x)   # C# Math.Exp는 inf → Min(9e18)
    return int(9e18) if c >= 9e18 else int(math.ceil(c))


def unlocked(stat, levels):  # :110-114
    r = d(stat)
    return r['unlockLevel'] <= 0 or levels.get(r['unlockStat'], 0) >= r['unlockLevel']


def cap_level(stat, levels):  # :119-128
    r = d(stat)
    if not unlocked(stat, levels):
        return 0
    if r['capEvery'] <= 0:
        return r['maxLevel']
    gate = levels.get(r['unlockStat'], 0)
    if gate < r['unlockLevel']:
        return 0
    cap = ((gate - r['unlockLevel']) // r['capEvery'] + 1) * r['capStep']  # long 나눗셈, 양수라 // 같음
    return int(min(r['maxLevel'], cap))


# ── 기대 성장 곡선 (IdleProgress.cs:197-279) ──
def expected_level(stat, stage, depth=0):
    if depth >= 8:
        return 0
    s = max(0, stage - 1)
    r = d(stat)
    lps = CI('levelsPerStage')
    if stat == 'Speed':
        return min(r['maxLevel'], int(C('expSpeedPerStage') * s))
    if stat == 'HpRegen':
        return min(r['maxLevel'], int(lps * s * C('expRegenRatio')))
    if stat in ('CritChance', 'CritDamage', 'AdvancedAttack'):
        ratio = {'CritChance': C('expCritChanceRatio'), 'CritDamage': C('expCritDamageRatio'),
                 'AdvancedAttack': C('expAdvancedAttackRatio')}[stat]
        gate = expected_level(r['unlockStat'], stage, depth + 1)
        if gate < r['unlockLevel']:
            return 0
        if r['capEvery'] <= 0:
            return r['maxLevel'] if ratio > 0 else 0
        cap = ((gate - r['unlockLevel']) / r['capEvery'] + 1) * r['capStep']
        return int(min(r['maxLevel'], cap * ratio))
    if stat == 'NormalEnemyDamage':
        div = max(1, CI('expNormalDamageDivisor'))
        num = expected_level(r['unlockStat'], stage, depth + 1) - r['unlockLevel']
        q = int(num / div)  # C# int 나눗셈 = 0쪽 버림
        return clamp(q, 0, r['maxLevel'])
    return min(r['maxLevel'], lps * s)


def expected_hit(s):  # :269-275 (인자 s = 스테이지 번호 그대로, 배율은 1+k*s)
    cc = min(1, value('CritChance', expected_level('CritChance', s)))
    cd = value('CritDamage', expected_level('CritDamage', s))
    return (value('Attack', expected_level('Attack', s)) * (1 + C('expAttackMulPerStage') * s)
            * value('AdvancedAttack', expected_level('AdvancedAttack', s)) * (1 + cc * (cd - 1)))


def expected_dps(s):  # :277
    return expected_hit(s) * SLOT_COUNT * value('Speed', expected_level('Speed', s))


def expected_hp(s):
    return value('Hp', expected_level('Hp', s)) * (1 + C('expHpMulPerStage') * s)


def expected_regen(s):
    return value('HpRegen', expected_level('HpRegen', s)) * (1 + C('expHpMulPerStage') * s)


# ── 적 (:283-325) ──
def waves_per_stage(stage):  # :17
    return max(1, int(stage_row(stage)['wavesPerStage']))


def boss_time(stage):  # :18
    return max(1.0, float(stage_row(stage)['bossTime']))


def enemy_count(stage, wave):  # :283-287
    r = stage_row(stage)
    n = r['enemyCountBase'] + r['enemyCountPerWave'] * (wave - 1) + r['enemyCountPerStage'] * (stage - 1)
    return clamp(n, r['enemyCountBase'], r['enemyCountMax'])


def enemy_hp(stage, wave):  # :290-291
    return (expected_hit(stage) * value('NormalEnemyDamage', expected_level('NormalEnemyDamage', stage))
            * C('enemyHpHits') * (1 + C('enemyHpWaveGrowth') * (wave - 1)))


def boss_hp(stage):  # :292
    return expected_dps(stage) * C('bossKillSeconds')


def enemy_attack(stage):  # :296-298
    if in_table(stage):
        return float(stage_row(stage)['enemyAttack'])
    return C('enemyAtkHpRate') * expected_hp(stage) + C('enemyAtkRegenRate') * expected_regen(stage)


def boss_attack(stage):  # :299-301
    if in_table(stage):
        return float(stage_row(stage)['bossAttack'])
    return expected_regen(stage) + C('bossAtkHpRate') * expected_hp(stage)


def clamp_to_long(v, lo=0):  # :329-333
    if v != v or v <= lo:
        return lo
    return int(9e18) if v >= 9e18 else int(v)


def enemy_gold(stage):  # :309-314
    if in_table(stage):
        return int(stage_row(stage)['enemyGold'])
    return clamp_to_long(cs_round(C('goldBase') * math.pow(1 + (stage - 1) / C('goldStep'), C('goldExp'))), 1)


def boss_gold(stage):  # :316-322
    if in_table(stage):
        return int(stage_row(stage)['bossGold'])
    g, mul = enemy_gold(stage), int(C('bossGoldMul'))
    if mul <= 0:
        return 0
    return int(9e18) if g > int(9e18) // mul else g * mul


def boss_mycelite(stage):  # :325 (C# long/int 나눗셈)
    if in_table(stage):
        return int(stage_row(stage)['bossMycelite'])
    return int(C('mycelBase')) + stage // max(1, CI('mycelStageDiv'))


# ── 편성 (Formation.cs) ──
def default_formation():
    """기본 편성 칸별 공격 배율 (Lv1 → 레벨 보너스 0). 빈 칸 = 0 (던지지 않음)"""
    slots = [0.0] * SLOT_COUNT
    for m in T.mascots:
        ds = m['defaultSlot']
        if 0 <= ds < SLOT_COUNT and m['initialOwned'] > 0:
            slots[ds] = float(m['attackMultiplier'])
    return slots


# ── 뽑기·퀘스트 ──
GACHA_COST11, GACHA_COST35 = 100, 300   # 균사석, 프리팹 직렬화 값 (위 출처)
GACHA_KINDS = 4                          # GachaUI.prefab: 무기·방어구·펫·꼬마 (각각 같은 가격)


def quest_mycelite_per_day():
    return sum(int(q['mycelite']) for q in T.quests)


if __name__ == '__main__':
    print('기본 편성 배율', default_formation(), '합', sum(default_formation()))
    for s in range(1, 8):
        print(s, 'hit', round(expected_hit(s), 3), 'eHP w1', round(enemy_hp(s, 1), 2), 'bossHP', round(boss_hp(s), 2),
              'cnt', [enemy_count(s, w) for w in range(1, waves_per_stage(s) + 1)],
              'eAtk', round(enemy_attack(s), 3), 'bAtk', round(boss_attack(s), 3), 'gold', enemy_gold(s), boss_gold(s), 'myc', boss_mycelite(s))
    print('퀘스트 균사석/일', quest_mycelite_per_day(), '35뽑 가격', GACHA_COST35)


# ── 장비 (Idle/Equipment.cs:18 SubPerGrade 3, :210-225) ──
SUB_PER_GRADE = 3


def own_base(grade):  # GachaProgress.cs:34-38
    gs = sorted(T.grades, key=lambda r: r['grade'])
    return float(gs[clamp(grade, 0, min(5, len(gs)) - 1)]['ownBase'])


def equip_base_percent(i):  # Equipment.cs:210-216
    g, sub = i // SUB_PER_GRADE, i % SUB_PER_GRADE
    frm = own_base(g)
    to = own_base(g + 1) if g + 1 < 5 else frm * C('equipTopGradeMul')
    return frm * math.pow(to / frm, sub / SUB_PER_GRADE)


def equip_level_mul(lv):  # :218
    return 1 + C('equipLevelBonus') * (lv - 1) if lv > 0 else 0


def equip_own_percent(i, lv):  # :222
    return equip_base_percent(i) * equip_level_mul(lv)


def criterion8():
    """기준 8: 등급 g의 가장 좋은 장비 최대 레벨 < 등급 g+1 이상 가장 약한 장비 Lv1 인지 (보유 효과; 장착 = 보유 × equipRatio라 순서 같음)"""
    mx = CI('equipMaxLevel')
    rows = []
    for g in range(4):
        low_top = equip_own_percent(g * 3 + 2, mx)
        for h in range(g + 1, 5):
            hi_l1 = equip_own_percent(h * 3, 1)
            rows.append((g, h, low_top, hi_l1, hi_l1 > low_top))
    return rows
