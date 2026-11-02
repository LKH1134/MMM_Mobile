# 던전 시트에 레벨 보상 열을 붙인다 (이론 v3 10.2 던전 행). 사용: python add_dungeon_cols.py <출력.xlsx>
import re, sys, zipfile
from xml.sax.saxutils import escape
SRC = r'C:\Project\MMM_Mobile\Assets\Data\Tables\GameBalance.xlsx'
COLS = [
    ('baseStage', 'int', '레벨 1의 기준 스테이지. 레벨 L 기준 = baseStage + stageStep × (L − 1). 방치 스테이지가 이보다 낮으면 소탕'),
    ('stageStep', 'int', '레벨마다 기준 스테이지가 오르는 수'),
    ('rewardKills', 'int', '레벨을 깨면 골드 = 기준 스테이지 적 처치 골드 × 이 값 (던전 1은 판에서 번 골드에 더한다)'),
    ('rewardMyc', 'int', '레벨을 깨면 균사석 = 이 값 × (1 + rewardGrowth × (L − 1))'),
    ('rewardGrowth', 'double', '레벨당 균사석 증가율'),
]
VALS = {9101: (5, 10, 12000, 0, 0), 9102: (25, 10, 0, 5, 0.01), 9103: (145, 10, 15000, 0, 0), 9104: (245, 10, 0, 6, 0.01), 9105: (395, 10, 15000, 4, 0.01)}

def letter(n):
    out = ''
    while n: n, r = divmod(n - 1, 26); out = chr(65 + r) + out
    return out

def cstr(ref, text, s): return f'<c r="{ref}" s="{s}" t="inlineStr"><is><t xml:space="preserve">{escape(text)}</t></is></c>'

z = zipfile.ZipFile(SRC); order = z.namelist(); files = {n: z.read(n) for n in order}
wb = files['xl/workbook.xml'].decode(); rels = files['xl/_rels/workbook.xml.rels'].decode()
rid = re.search(r'<sheet name="던전" sheetId="\d+" r:id="(rId\d+)"', wb).group(1)
path = 'xl/' + re.search(r'Id="%s"[^>]*Target="([^"]+)"' % rid, rels).group(1)
s = files[path].decode()
dim = re.search(r'<dimension ref="A1:([A-Z]+)(\d+)"/>', s)
last = ord(dim.group(1)) - 64
for name, _, _ in COLS: assert f'>{name}<' not in s, name

def add(m):
    row = m.group(0); r = int(re.match(r'<row r="(\d+)"', row).group(1)); extra = ''
    key = re.search(r'<c r="A%d"[^>]*><v>(\d+)</v>' % r, row)
    for k, (name, typ, desc) in enumerate(COLS):
        ref = f'{letter(last + 1 + k)}{r}'
        if r == 1: extra += cstr(ref, name, 3)
        elif r == 2: extra += cstr(ref, typ, 4)
        elif r == 3: extra += cstr(ref, desc, 5)
        else: extra += f'<c r="{ref}" s="{6 if typ == "int" else 7}"><v>{VALS[int(key.group(1))][k]}</v></c>'
    row = re.sub(r'spans="1:\d+"', f'spans="1:{last + len(COLS)}"', row, count=1)
    return row[:-len('</row>')] + extra + '</row>'

s = re.sub(r'<row r="\d+"[^>]*>.*?</row>', add, s, flags=re.S)
s = s.replace(dim.group(0), f'<dimension ref="A1:{letter(last + len(COLS))}{dim.group(2)}"/>')
if '</cols>' in s: s = s.replace('</cols>', f'<col min="{last + 1}" max="{last + len(COLS)}" width="16" customWidth="1"/></cols>')
files[path] = s.encode()
with zipfile.ZipFile(sys.argv[1], 'w', zipfile.ZIP_DEFLATED) as out:
    for n in order: out.writestr(n, files[n])
print('dungeon', path, 'cols', last, '->', last + len(COLS))
