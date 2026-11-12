# 던전 강함 (11-03 사용자 "던전 1~5 보상·강함 대충"): 던전 시트에 hpMul·atkMul 열, 상수 harvestHpRateExp를 붙인다.
#   던전 2~5(보스전): 레벨 L의 보스 체력·공격 = 기준 스테이지(baseStage + stageStep × (L − 1)) 방치 보스 × hpMul·atkMul.
#   던전 1(수확 판): 플레이어 수확 힘이 방치 성장과 따로라(스킬 트리 제거) 레벨 체력 계수(Stage.xlsx hpRate)를 다 쓰면 레벨 수십에서 못 깬다
#   → 체력 계수^harvestHpRateExp만 쓴다. 사용: python add_dungeon_strength.py <출력.xlsx>
import re, sys, zipfile
from xml.sax.saxutils import escape

SRC = r'C:\Project\MMM_Mobile\Assets\Data\Tables\MainTable.xlsx'
COLS = [
    ('hpMul', 'double', '보스전 던전(2~5): 레벨 보스 체력 = 기준 스테이지 방치 보스 체력 × 이 값 (던전 1 수확 판은 상수 harvestHpRateExp)'),
    ('atkMul', 'double', '보스전 던전(2~5): 레벨 보스 공격 = 기준 스테이지 방치 보스 공격 × 이 값'),
]
VALS = {9101: (1.0, 1.0), 9102: (1.0, 1.0), 9103: (1.0, 1.0), 9104: (1.1, 1.0), 9105: (1.2, 1.1)}
CONST = ('harvestHpRateExp', 0.2, '수확 판(던전 1) 체력 계수 반영 지수: 버섯·보스 체력 = 레벨 체력 계수(Stage.xlsx hpRate)^이 값. 1 = 그대로, 0 = 레벨이 올라도 같음')


def letter(n):
    out = ''
    while n: n, r = divmod(n - 1, 26); out = chr(65 + r) + out
    return out


def num(ref): return sum((ord(c) - 64) * 26 ** i for i, c in enumerate(reversed(re.match(r'[A-Z]+', ref).group(0))))


z = zipfile.ZipFile(SRC); order = z.namelist(); files = {n: z.read(n) for n in order}
wb = files['xl/workbook.xml'].decode(); rels = files['xl/_rels/workbook.xml.rels'].decode()


def path(name):
    rid = re.search(r'r:id="(rId\d+)"', re.search(r'<sheet [^>]*name="%s"[^>]*/>' % name, wb).group(0)).group(1)
    t = re.search(r'Target="([^"]+)"', re.search(r'<Relationship [^>]*Id="%s"[^>]*/>' % rid, rels).group(0)).group(1)
    return 'xl/' + t.lstrip('/').replace('xl/', '', 1)


# 던전 시트
p = path('던전'); s = files[p].decode()
dim = re.search(r'<dimension ref="A1:([A-Z]+)(\d+)"/>', s); last = num(dim.group(1))
for name, _, _ in COLS: assert f'<t>{name}</t>' not in s, name
lastL = letter(last)


def style(r):
    return re.search(r'<c r="%s%d" s="(\d+)"' % (lastL, r), s).group(1)


def add(m):
    row = m.group(0); r = int(re.match(r'<row r="(\d+)"', row).group(1)); extra = ''
    st = style(r)
    key = re.search(r'<c r="A%d"[^>]*><v>(\d+)</v>' % r, row)
    for k, (name, typ, desc) in enumerate(COLS):
        ref = f'{letter(last + 1 + k)}{r}'
        if r <= 3:
            text = (name, typ, desc)[r - 1]
            extra += f'<c r="{ref}" s="{st}" t="inlineStr"><is><t>{escape(text)}</t></is></c>'
        else:
            extra += f'<c r="{ref}" s="{st}" t="n"><v>{VALS[int(key.group(1))][k]}</v></c>'
    return row[:-len('</row>')] + extra + '</row>'


s = re.sub(r'<row r="\d+"[^>]*>.*?</row>', add, s, flags=re.S)
s = s.replace(dim.group(0), f'<dimension ref="A1:{letter(last + len(COLS))}{dim.group(2)}"/>')
files[p] = s.encode()

# 상수 시트: 마지막 행을 본떠 한 행
p = path('상수'); s = files[p].decode()
assert f'<t>{CONST[0]}</t>' not in s
dim = re.search(r'<dimension ref="A1:D(\d+)"/>', s); lastrow = int(dim.group(1))
rows = re.findall(r'<row r="\d+"[^>]*>.*?</row>', s, flags=re.S)
tmpl = rows[-1]
nxt = max(int(v) for v in re.findall(r'<c r="A\d+"[^>]*><v>(9\d{3})</v></c>', s)) + 1
ln = lastrow + 1
sa, sb, sc, sd = (re.search(r'<c r="%s%d" s="(\d+)"' % (c, lastrow), tmpl).group(1) for c in 'ABCD')
row = (f'<row r="{ln}"><c r="A{ln}" s="{sa}" t="n"><v>{nxt}</v></c><c r="B{ln}" s="{sb}" t="inlineStr"><is><t>{CONST[0]}</t></is></c>'
       f'<c r="C{ln}" s="{sc}" t="n"><v>{CONST[1]}</v></c><c r="D{ln}" s="{sd}" t="inlineStr"><is><t>{escape(CONST[2])}</t></is></c></row>')
s = s.replace('</sheetData>', row + '</sheetData>').replace(dim.group(0), f'<dimension ref="A1:D{ln}"/>')
files[p] = s.encode()

# info 시트 상수 행 수
ROW = '&#54665;'     # "행" (이 파일은 글자를 숫자 참조로 저장한다)
p = path('info'); s = files[p].decode()
old, new = f'>{lastrow - 3}{ROW}<', f'>{lastrow - 2}{ROW}<'
assert s.count(old) == 1, ('info 상수 행 수', s.count(old))
s = s.replace(old, new)
if s.count(f'>1000{ROW}<') == 1: s = s.replace(f'>1000{ROW}<', f'>700{ROW}<')     # 스테이지 시트는 v3부터 700행
files[p] = s.encode()

with zipfile.ZipFile(sys.argv[1], 'w', zipfile.ZIP_DEFLATED) as out:
    for n in order: out.writestr(n, files[n])
print('dungeon cols', last, '->', last + len(COLS), 'const', nxt, CONST[0])
