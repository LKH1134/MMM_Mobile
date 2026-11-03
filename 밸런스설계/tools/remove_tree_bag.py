# 세계수 수확 보따리 제거 (11-03 사용자: 앱 밖 보상과 겹침 → 빼고 비슷한 효과로 바꾼다). 사용: python remove_tree_bag.py <출력.xlsx>
#   효과: BagGold → KillGold(값 그대로), BagMycelite → BossMycelite(값 그대로), BagHours(시간) → CraftSpeed(시간 × 0.1, 1시간 = +10%)
#   보따리 전용 열(세계수 bagKillsPerHour·bagMycelitePerHour, 꼬마진화 bagMul, 호감도 bagBonus)은 이름 앞에 #을 붙여 메모 열로
#   상수 treeRingBagBonus·kidBagShare·decoSetBagMul·bagCapHours 행은 지운다
import re, sys, zipfile

SRC = r'C:\Project\MMM_Mobile\Assets\Data\Tables\GameBalance.xlsx'
EFFECT = {'BagGold': ('KillGold', 1.0), 'BagMycelite': ('BossMycelite', 1.0), 'BagHours': ('CraftSpeed', 0.1)}
EFFECT_SHEETS = {'꼬마': ('bonusEffect', 'bonusValue'), '가호': ('effect', 'value'), '장식': ('effect', 'value')}
MEMO_COLS = {'세계수': ['bagKillsPerHour', 'bagMycelitePerHour'], '꼬마진화': ['bagMul'], '호감도': ['bagBonus']}
DROP_CONSTS = ['treeRingBagBonus', 'kidBagShare', 'decoSetBagMul', 'bagCapHours']
ROW = '&#54665;'

z = zipfile.ZipFile(SRC); order = z.namelist(); files = {n: z.read(n) for n in order}
wb = files['xl/workbook.xml'].decode(); rels = files['xl/_rels/workbook.xml.rels'].decode()


def path(name):
    rid = re.search(r'r:id="(rId\d+)"', re.search(r'<sheet [^>]*name="%s"[^>]*/>' % name, wb).group(0)).group(1)
    t = re.search(r'Target="([^"]+)"', re.search(r'<Relationship [^>]*Id="%s"[^>]*/>' % rid, rels).group(0)).group(1)
    return 'xl/' + t.lstrip('/').replace('xl/', '', 1)


def header_col(s, name):
    m = re.search(r'<c r="([A-Z]+)1"[^>]*t="inlineStr"><is><t>%s</t></is></c>' % name, s)
    assert m, name
    return m.group(1)


changed = 0
for sheet, (ecol_name, vcol_name) in EFFECT_SHEETS.items():
    p = path(sheet); s = files[p].decode()
    ec, vc = header_col(s, ecol_name), header_col(s, vcol_name)
    for old, (new, scale) in EFFECT.items():
        for m in list(re.finditer(r'<c r="%s(\d+)"([^>]*)t="inlineStr"><is><t>%s</t></is></c>' % (ec, old), s)):
            r = m.group(1)
            s = s.replace(m.group(0), f'<c r="{ec}{r}"{m.group(2)}t="inlineStr"><is><t>{new}</t></is></c>', 1)
            vm = re.search(r'<c r="%s%s"([^>]*)><v>([^<]+)</v></c>' % (vc, r), s)
            if scale != 1.0:
                s = s.replace(vm.group(0), f'<c r="{vc}{r}"{vm.group(1)}><v>{round(float(vm.group(2)) * scale, 6)}</v></c>', 1)
            changed += 1
    assert not re.search(r'<t>Bag(Gold|Mycelite|Hours)</t>', s), sheet
    files[p] = s.encode()

for sheet, cols in MEMO_COLS.items():
    p = path(sheet); s = files[p].decode()
    for c in cols:
        old = f'<is><t>{c}</t></is>'
        assert s.count(old) == 1, (sheet, c)
        s = s.replace(old, f'<is><t>#{c}</t></is>')
    files[p] = s.encode()

p = path('상수'); s = files[p].decode()
head, rest = s.split('<sheetData>', 1)
data, tail = rest.split('</sheetData>', 1)
rows = re.findall(r'<row r="\d+"[^>]*>.*?</row>', data, flags=re.S)
kept, dropped = [], 0
for row in rows:
    if any(f'<t>{k}</t>' in row for k in DROP_CONSTS): dropped += 1; continue
    kept.append(row)
assert dropped == len(DROP_CONSTS), dropped
out = []
for i, row in enumerate(kept, start=1):
    old = int(re.match(r'<row r="(\d+)"', row).group(1))
    if old != i:
        row = re.sub(r'<row r="%d"' % old, f'<row r="{i}"', row, count=1)
        row = re.sub(r'(<c r="[A-Z]+)%d"' % old, r'\g<1>%d"' % i, row)
    out.append(row)
s = head + '<sheetData>' + ''.join(out) + '</sheetData>' + tail
dim = re.search(r'<dimension ref="A1:D(\d+)"/>', s)
last = int(dim.group(1))
s = s.replace(dim.group(0), f'<dimension ref="A1:D{len(out)}"/>')
files[p] = s.encode()

p = path('info'); s = files[p].decode()
old, new = f'>{last - 3}{ROW}<', f'>{len(out) - 3}{ROW}<'
assert s.count(old) == 1, old
files[p] = s.replace(old, new).encode()

with zipfile.ZipFile(sys.argv[1], 'w', zipfile.ZIP_DEFLATED) as o:
    for n in order: o.writestr(n, files[n])
print('effects', changed, 'consts dropped', dropped, 'const rows', last, '->', len(out))
