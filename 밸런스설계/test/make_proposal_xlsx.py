# theory#1 수치안(이론.md 10.2 + theory/stage_proposal.csv) 표 사본을 만든다. Assets의 표는 건드리지 않는다.
# 사용: python 밸런스설계/test/make_proposal_xlsx.py  →  밸런스설계/test/out/GameBalance_proposal.xlsx
import csv, openpyxl, os
ROOT = r'C:\Project\MMM_Mobile'
SRC = ROOT + r'\Assets\Data\Tables\GameBalance.xlsx'
CSV = ROOT + r'\밸런스설계\theory\stage_proposal.csv'
OUT = ROOT + r'\밸런스설계\test\out\GameBalance_proposal.xlsx'
wb = openpyxl.load_workbook(SRC)

def hdr(ws): return [c.value for c in ws[1]]
def col(ws, name): return hdr(ws).index(name) + 1
def add_col(ws, name, typ, desc):
    c = ws.max_column + 1
    ws.cell(1, c, name); ws.cell(2, c, typ); ws.cell(3, c, desc)
    return c

# 스테이지 시트: #enemyHpExample→enemyHp, #bossHp→bossHp, 값은 csv
ws = wb['스테이지']
# 관리자가 14:42에 enemyHp·bossHp 열(T·U, 값 0)을 이미 추가했다. 없으면 만든다 (옛 #메모 열은 그대로 둔다)
for nm, desc in (('enemyHp', '일반 적 1웨이브 체력 (수치안)'), ('bossHp', '보스 체력 (수치안)')):
    if nm not in hdr(ws): add_col(ws, nm, 'double', desc)
rows = list(csv.DictReader(open(CSV, encoding='utf-8')))
assert len(rows) == 1000
intcols = {'wavesPerStage', 'enemyCountBase', 'enemyCountPerWave', 'enemyCountPerStage', 'enemyCountMax', 'enemyGold', 'bossGold', 'bossMycelite'}
for r in rows:
    s = int(r['stage']); xr = s + 3
    assert ws.cell(xr, col(ws, 'stage')).value == s
    for k, v in r.items():
        if k == 'stage': continue
        f = float(v)
        ws.cell(xr, col(ws, k), int(round(f)) if k in intcols else f)

# 업그레이드: valueExponent·valueDivisor 열 + 10.2 값
ws = wb['업그레이드']
cve = col(ws, 'valueExponent') if 'valueExponent' in hdr(ws) else add_col(ws, 'valueExponent', 'double', '값 지수 (Power 모드: baseValue × (1 + Lv/valueDivisor)^valueExponent)')
cvd = col(ws, 'valueDivisor') if 'valueDivisor' in hdr(ws) else add_col(ws, 'valueDivisor', 'double', '값 레벨 단위 (Power 모드)')
byStat = {ws.cell(r, col(ws, 'stat')).value: r for r in range(4, ws.max_row + 1) if ws.cell(r, col(ws, 'stat')).value}
for st, r in byStat.items():
    ws.cell(r, cve, 3 if st in ('Attack', 'Hp', 'HpRegen') else 0)
    ws.cell(r, cvd, 10 if st in ('Attack', 'Hp', 'HpRegen') else 0)
for st in ('Attack', 'Hp', 'HpRegen'):
    r = byStat[st]
    ws.cell(r, col(ws, 'valueMode'), 'Power')
    ws.cell(r, col(ws, 'priceExponent'), 3.5)
ws.cell(byStat['Attack'], col(ws, 'priceScale'), 1125); ws.cell(byStat['Hp'], col(ws, 'priceScale'), 1125); ws.cell(byStat['HpRegen'], col(ws, 'priceScale'), 675)
for st, v in (('CritChance', 225), ('CritDamage', 169), ('AdvancedAttack', 141), ('NormalEnemyDamage', 70)):
    ws.cell(byStat[st], col(ws, 'priceScale'), v)
ws.cell(byStat['Attack'], col(ws, 'maxLevel'), 17000); ws.cell(byStat['Hp'], col(ws, 'maxLevel'), 17000); ws.cell(byStat['HpRegen'], col(ws, 'maxLevel'), 11000)
ws.cell(byStat['Speed'], col(ws, 'maxLevel'), 275)

# 퀘스트: 균사석 75/75/75/75/50, 업그레이드 목표 50→20
ws = wb['퀘스트']
for r, m in zip(range(4, 9), (75, 75, 75, 75, 50)): ws.cell(r, col(ws, 'mycelite'), m)
for r in range(4, 9):
    if ws.cell(r, col(ws, 'kind')).value == 'upgrade': ws.cell(r, col(ws, 'target'), 20)

# 등급·꼬마
ws = wb['등급']
for r, v in zip(range(4, 9), (2, 20, 200, 2000, 20000)): ws.cell(r, col(ws, 'ownBase'), v)
ws = wb['꼬마']
for r, v in zip(range(4, 13), (0.75, 0.75, 0.9, 1.08, 0.9, 1.30, 0.9, 1.08, 1.56)): ws.cell(r, col(ws, 'attackMultiplier'), v)

# 상수
ws = wb['상수']
vals = {'equipLevelBonus': 0.01, 'equipTopGradeMul': 10, 'ggomaLevelBonus': 0.002, 'offlineCapHours': 12, 'adBoxGoldKills': 4000, 'adBoxMyceliteBosses': 20,
        'goldBase': 6.75, 'goldStep': 1.5, 'goldExp': 3.5, 'expAttackMulPerStage': 11.2, 'expHpMulPerStage': 5.6,
        'enemyAtkHpRate': 0.0029, 'enemyAtkRegenRate': 0.063, 'bossAtkHpRate': 0.0105}
kv = {ws.cell(r, 2).value: r for r in range(4, ws.max_row + 1)}
for k, v in vals.items(): ws.cell(kv[k], 3, v)
last = max(kv.values())
for i, (k, v, d) in enumerate((('offlineGoldRate', 0.5, '앱 밖 골드 = 골드/초 × 시간 × 이 값 (수치안)'), ('adGachaPulls', 4, '광고 뽑기 한 번에 뽑는 수 (수치안, 지금 11)'))):
    if k in kv: ws.cell(kv[k], 3, v); continue
    r = last + 1 + i
    ws.cell(r, 1, 9044 + i); ws.cell(r, 2, k); ws.cell(r, 3, v); ws.cell(r, 4, d)

# 던전: 새 열 7개 (이론.md 8.2)
ws = wb['던전']
spec = [('currency', 'string', '보상 재화 (Gold/Mycelite/Both)', ['Gold', 'Mycelite', 'Gold', 'Mycelite', 'Both']),
        ('baseStage', 'int', '레벨 1의 기준 방치 스테이지', [5, 15, 30, 80, 150]),
        ('stageStep', 'int', '레벨당 기준 스테이지 증가', [3] * 5),
        ('hpMul', 'double', '적 체력 배수', [1.0] * 5),
        ('rewardKills', 'long', '골드 보상 = 기준 스테이지 처치 골드 × 이 횟수', [12000, 0, 15000, 0, 15000]),
        ('rewardMyc', 'long', '균사석 보상 기본', [0, 5, 0, 6, 4]),
        ('rewardGrowth', 'double', '균사석 레벨당 증가율', [0, 0.01, 0, 0.01, 0.01])]
for name, typ, desc, v5 in spec:
    c = col(ws, name) if name in hdr(ws) else add_col(ws, name, typ, desc)
    for r, v in zip(range(4, 9), v5): ws.cell(r, c, v)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
wb.save(OUT)
print('saved', OUT)
