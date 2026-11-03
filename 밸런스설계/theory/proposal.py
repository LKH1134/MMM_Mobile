# 수치안 묶음: 표·상수 오버라이드 + 새 스테이지 시트. model.py가 current/proposal을 같은 시뮬로 돌린다.
import formulas as F
import gen_stage as G

SPEED_CAP = {'Speed': {'maxLv': 275}}          # 기준 5 (전제)

# 기준 7: 공격력·체력·체력 재생 = base × (1 + Lv/10)^3, 비용 지수 2.5 → 3.5 (골드도 × (1+1.5(s-1)))
# 시간 허들: priceScale = FARM × (기대 레벨/스테이지)
FARM = 75
UPGRADES = {
    'Attack': {'vexp': 3, 'vdiv': 10, 'exp': 3.5, 'scale': FARM * 15},
    'Hp': {'vexp': 3, 'vdiv': 10, 'exp': 3.5, 'scale': FARM * 15},
    'HpRegen': {'vexp': 3, 'vdiv': 10, 'exp': 3.5, 'scale': FARM * 9},
    'Speed': {'maxLv': 275, 'scale': 600},
    'CritChance': {'scale': FARM * 3},
    'CritDamage': {'scale': FARM * 2.25},
    'AdvancedAttack': {'scale': FARM * 1.875},
    'NormalEnemyDamage': {'scale': FARM * 0.9375},
}
CONST = {
    'offlineCapHours': 12,           # P6
    'equipLevelBonus': 0.006,        # 기준 8 v3: Lv100 = ×1.594 < 단계 간 ×5^(1/3)=1.710 (여유 7%; v2 0.007은 1%)
    'ggomaLevelBonus': 0.0015,       # 기준 8 v3: Lv100 = ×1.149 < 꼬마 등급 비 1.2 (여유 4.5%; v2 0.002는 0.2%)
    'equipTopGradeMul': 5,
    'adGachaPulls': 4,               # 광고 뽑기 11 → 4 (4종 × 4 = 16뽑 = 퀘스트 35뽑의 46%, P9)
    'dungeonAdPerDay': 2,
    'offlineGoldRate': 0.5,          # 앱 밖 골드 = 접속 초당 골드의 50% (P8 하한, 새 상수·코드 1줄)            # 기준 4: 광고 입장 2/일 (상수 9042)
}
OWN_BASE = [2, 10, 50, 250, 1250]     # 기준 8 v2: 등급 ×5, 단계 ×1.710 (v1 ×10은 전설 한 장 운 분산이 컸다)
QUEST_MYC = [75, 75, 75, 75, 50]       # 기준 2: 광고 없이 깨는 네 개 합 300 = 35뽑, 광고 퀘스트 50은 덤
QUEST_TARGET = [300, 3, 20, 11, 3]     # 스테이지 퀘스트 = 보스 도전 3회(코드), 강화 50 → 20 (벽에서도 하루에 깰 수 있게)


GGOMA_FORM = [0.75, 0.75, 1.08, 0.9, 0.9]   # 기준 8 꼬마 배율의 기본 편성 (새싹·이슬·불씨·조약돌·동전)
# D4 (v3): 전설 천장 없음. 뽑기 레벨마다 등급 해금 (잠긴 등급 0%). 시트 gacha 8201~8210 odds·expToNext
GACHA_V3 = [
    {'exp': 30, 'odds': [100, 0, 0, 0, 0]},
    {'exp': 60, 'odds': [70, 30, 0, 0, 0]},
    {'exp': 100, 'odds': [58, 30, 12, 0, 0]},
    {'exp': 150, 'odds': [55, 30, 15, 0, 0]},
    {'exp': 200, 'odds': [47, 30, 18, 5, 0]},
    {'exp': 300, 'odds': [43, 30, 20, 7, 0]},
    {'exp': 400, 'odds': [38.5, 30, 22, 8.5, 1.0]},
    {'exp': 500, 'odds': [33, 30, 25, 10.5, 1.5]},
    {'exp': 600, 'odds': [27.5, 30, 28, 12.5, 2.0]},
    {'exp': 0, 'odds': [21, 30, 30, 16, 3.0]},
]
V3 = dict(farm=150, table_end=700, early_gold_mul=2.0, smooth=5)
EXTRAS = {'adBoxGoldKills': 4000, 'adBoxMyceliteBosses': 5}   # v2: 보스 균사석 2 고정 × 5 = 상자당 10


def upgrades(farm):
    u = {k: dict(v) for k, v in UPGRADES.items()}
    per = {'Attack': 15, 'Hp': 15, 'HpRegen': 9, 'CritChance': 3, 'CritDamage': 2.25, 'AdvancedAttack': 1.875, 'NormalEnemyDamage': 0.9375}
    for k, v in per.items():
        u[k]['scale'] = farm * v
    return u


def apply(params=None, with_rows=True, farm=FARM, extras=False):
    """farm: priceScale 배수 (D1), extras: 광고 상자 4000/20 + 꼬마 배율(기준 8)까지 모델에 넣기"""
    const = dict(CONST)
    params = dict(params or {})
    params.setdefault('target_b', 60.0 * farm / FARM)     # 목표 곡선 기울기도 같은 비율로 (장비 기준 날짜)
    params.setdefault('myc_normal', 2)      # v2: 보스 균사석 2 고정 (챕터 보너스 없음 — 상자 균사석이 챕터에서 튀지 않게)
    params.setdefault('myc_wall', 2)
    if extras:
        const.update(EXTRAS)
        params.setdefault('ref_form', sum(GGOMA_FORM))
    v3 = params.pop('v3', False)
    if v3:
        for k in ('table_end', 'early_gold_mul', 'smooth'):
            params.setdefault(k, V3[k])
    ov = {'upgrade': upgrades(farm), 'const': const, 'ownBase': OWN_BASE,
          'quest_myc': QUEST_MYC, 'quest_target': QUEST_TARGET, 'quest_stage_boss': True}
    if extras:
        ov['formation'] = GGOMA_FORM
    if v3:
        ov['gacha'] = GACHA_V3
    F.set_overrides(ov)
    if with_rows:
        rows = G.make_rows(params)
        ov['stage_fn'] = G.stage_fn(rows)
        if extras:
            ov['formation'] = GGOMA_FORM
        F.set_overrides(ov)
        return rows
    if extras:
        ov['formation'] = GGOMA_FORM
        F.set_overrides(ov)
    return None


def current():
    F.set_overrides({'upgrade': SPEED_CAP})
