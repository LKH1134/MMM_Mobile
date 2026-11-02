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
    'equipLevelBonus': 0.007,        # 기준 8: Lv100 = ×1.693 < 단계 간 ×5^(1/3)=1.710 (v2: 분산 줄이려 등급 ×10 → ×5)
    'equipTopGradeMul': 5,
    'adGachaPulls': 4,               # 광고 뽑기 11 → 4 (4종 × 4 = 16뽑 = 퀘스트 35뽑의 46%, P9)
    'dungeonAdPerDay': 2,
    'offlineGoldRate': 0.5,          # 앱 밖 골드 = 접속 초당 골드의 50% (P8 하한, 새 상수·코드 1줄)            # 기준 4: 광고 입장 2/일 (상수 9042)
}
OWN_BASE = [2, 10, 50, 250, 1250]     # 기준 8 v2: 등급 ×5, 단계 ×1.710 (v1 ×10은 전설 한 장 운 분산이 컸다)
QUEST_MYC = [75, 75, 75, 75, 50]       # 기준 2: 광고 없이 깨는 네 개 합 300 = 35뽑, 광고 퀘스트 50은 덤
QUEST_TARGET = [300, 3, 20, 11, 3]     # 스테이지 퀘스트 = 보스 도전 3회(코드), 강화 50 → 20 (벽에서도 하루에 깰 수 있게)


GGOMA_FORM = [0.75, 0.75, 1.08, 0.9, 0.9]   # 기준 8 꼬마 배율의 기본 편성 (새싹·이슬·불씨·조약돌·동전)
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
    ov = {'upgrade': upgrades(farm), 'const': const, 'ownBase': OWN_BASE,
          'quest_myc': QUEST_MYC, 'quest_target': QUEST_TARGET, 'quest_stage_boss': True}
    if extras:
        ov['formation'] = GGOMA_FORM
    F.set_overrides(ov)
    if with_rows:
        rows = G.make_rows(params)
        ov['stage_fn'] = lambda s: rows.get(s)
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
