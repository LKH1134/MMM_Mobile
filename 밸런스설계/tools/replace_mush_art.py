# 버섯 임시 그림 → 새 그림 (11-05): 옛 식용 10종(ed0~9)·부리 보스(boss1)·꼬마 아이콘(icon_*) 리소스를 빼고 새 시트만 쓴다.
#   적: 식용 1001~1005 = 식용버섯 (일반·레어·에픽·유니크·전설) — 독·약용과 같은 5등급. 1006~1010 삭제
#   리소스: 7001~7005 → Enumy/Mush_Edible_*_Colony. 7006~7011·7013~7020 삭제
#   보스 2001 → 7036 (독버섯 전설 군락). 꼬마 resourceId → 7029 (진화 1단계 시트, 진화 칸을 못 찾을 때만 쓰는 예비 그림)
#   옛 적 ID i → 1001 + (i-1001)//2 (두 종씩 한 등급) 로 꼬마 favoriteIds·세계수 preferIds·스테이지 enemyIds를 다시 맞춘다 (중복은 뺀다)
#   사용: python replace_mush_art.py <출력.xlsx>   (원본은 Assets/Data/Tables/MainTable.xlsx)
import re, sys, zipfile

SRC = r'C:\Project\MMM_Mobile\Assets\Data\Tables\MainTable.xlsx'
z = zipfile.ZipFile(SRC); order = z.namelist(); files = {n: z.read(n) for n in order}
wb = files['xl/workbook.xml'].decode(); rels = files['xl/_rels/workbook.xml.rels'].decode()


def path(name):
    rid = re.search(r'r:id="(rId\d+)"', re.search(r'<sheet [^>]*name="%s"[^>]*/>' % name, wb).group(0)).group(1)
    t = re.search(r'Target="([^"]+)"', re.search(r'<Relationship [^>]*Id="%s"[^>]*/>' % rid, rels).group(0)).group(1)
    return 'xl/' + t.lstrip('/').replace('xl/', '', 1)


def ent(text):      # 이 파일은 한글을 &#코드; 로 적는다
    return ''.join(ch if ord(ch) < 128 else f'&#{ord(ch)};' for ch in text)


def rows_of(s):
    return re.findall(r'<row r="\d+"[^>]*>.*?</row>', s, re.S)


def row_by_id(s, idx):
    m = [r for r in rows_of(s) if re.search(r'<c r="A\d+"[^>]*><v>%d</v></c>' % idx, r)]
    assert len(m) == 1, ('row', idx, len(m))
    return m[0]


def set_cell(row, col, value, text):
    rn = re.match(r'<row r="(\d+)"', row).group(1)
    c = re.search(r'<c r="%s%s"([^>]*?)(?: t="[^"]*")?>(?:<v>[^<]*</v>|<is><t>[^<]*</t></is>)</c>' % (col, rn), row)
    assert c, ('cell', col, rn)
    attrs = re.sub(r' t="[^"]*"', '', c.group(1))
    new = (f'<c r="{col}{rn}"{attrs} t="inlineStr"><is><t>{ent(value)}</t></is></c>' if text
           else f'<c r="{col}{rn}"{attrs} t="n"><v>{value}</v></c>')
    return row.replace(c.group(0), new)


def cell_text(row, col):
    rn = re.match(r'<row r="(\d+)"', row).group(1)
    return re.search(r'<c r="%s%s"[^>]*>(?:<v>([^<]*)</v>|<is><t>([^<]*)</t></is>)</c>' % (col, rn), row)


def delete_rows(s, ids):
    """A열 ID가 ids인 행을 지우고 아래 행 번호를 당긴다"""
    out, shift = [], 0
    head, body, tail = re.match(r'(.*<sheetData>)(.*)(</sheetData>.*)', s, re.S).groups()
    for row in rows_of(body):
        rn = int(re.match(r'<row r="(\d+)"', row).group(1))
        a = re.search(r'<c r="A%d"[^>]*><v>(\d+)</v></c>' % rn, row)
        if a and int(a.group(1)) in ids:
            shift += 1
            continue
        if shift:
            n = rn - shift
            row = re.sub(r'^<row r="%d"' % rn, f'<row r="{n}"', row)
            row = re.sub(r'<c r="([A-Z]+)%d"' % rn, lambda m: f'<c r="{m.group(1)}{n}"', row)
        out.append(row)
    assert shift == len(ids), ('deleted', shift, len(ids))
    s = head + ''.join(out) + tail
    dim = re.search(r'<dimension ref="A1:([A-Z]+)(\d+)"/>', s)
    return s.replace(dim.group(0), f'<dimension ref="A1:{dim.group(1)}{int(dim.group(2)) - shift}"/>')


def remap(ids):
    out = []
    for i in ids:
        j = 1001 + (i - 1001) // 2 if 1001 <= i <= 1010 else i
        if j not in out: out.append(j)
    return out


GRADES = [('Common', '일반', 1), ('Rare', '레어', 1), ('Epic', '에픽', 2), ('Unique', '유니크', 2), ('Legend', '전설', 3)]

# 리소스
p = path('리소스'); s = files[p].decode()
for k, (g, _, _) in enumerate(GRADES):
    r = row_by_id(s, 7001 + k)
    assert 'Critters/Mushrooms/ed%d' % k in r
    s = s.replace(r, set_cell(r, 'D', f'Art/Dream/Characters/Enumy/Mush_Edible_{g}_Colony', True))
assert 'Critters/Bosses/boss1' in row_by_id(s, 7011) and 'Ggomas/icon_chest' in row_by_id(s, 7020)
s = delete_rows(s, set(range(7006, 7012)) | set(range(7013, 7021)))
assert 'Critters/Mushrooms' not in s and 'Critters/Bosses' not in s and 'Ggomas/icon_' not in s.replace('Ggomas/icon_GGoma', '')
files[p] = s.encode()

# 적
p = path('적'); s = files[p].decode()
for k, (_, kr, value) in enumerate(GRADES):
    r = row_by_id(s, 1001 + k)
    n = set_cell(set_cell(r, 'E', f'식용버섯 ({kr})', True), 'F', value, False)
    s = s.replace(r, n)
s = delete_rows(s, set(range(1006, 1011)))
files[p] = s.encode()

# 보스
p = path('보스'); s = files[p].decode()
r = row_by_id(s, 2001); assert cell_text(r, 'E').group(1) == '7011'
files[p] = s.replace(r, set_cell(r, 'E', 7036, False)).encode()

# 꼬마
p = path('꼬마'); s = files[p].decode()
for idx in range(3001, 3009):
    r = row_by_id(s, idx)
    fav = [int(x) for x in cell_text(r, 'H').group(2).split(',')]
    n = set_cell(set_cell(r, 'G', 7029, False), 'H', ','.join(map(str, remap(fav))), True)
    print('kid', idx, fav, '->', remap(fav))
    s = s.replace(r, n)
files[p] = s.encode()

# 세계수 preferIds (P열)
p = path('세계수'); s = files[p].decode()
for idx in range(9301, 9309):
    r = row_by_id(s, idx)
    old = [int(x) for x in cell_text(r, 'P').group(2).split(',')]
    print('tree', idx, old, '->', remap(old))
    s = s.replace(r, set_cell(r, 'P', ','.join(map(str, remap(old))), True))
files[p] = s.encode()

# 스테이지 enemyIds + info 예시
OLD10 = '1001,1002,1003,1004,1005,1006,1007,1008,1009,1010'
p = path('스테이지'); s = files[p].decode()
n = s.count(f'<t>{OLD10}</t>'); assert n == 700, n
files[p] = s.replace(f'<t>{OLD10}</t>', '<t>1001,1002,1003,1004,1005</t>').encode()

p = path('info'); s = files[p].decode()
s = s.replace(OLD10, '1001,1002,1003,1004,1005')
s = s.replace('Art/Dream/Characters/Critters/Mushrooms/ed0', 'Art/Dream/Characters/Enumy/Mush_Edible_Common_Colony')
a, b = ent('보스 2001 / 적 1001~1010'), ent('보스 2001 / 적 1001~1005·1011~1020')
assert a in s; s = s.replace(a, b)
for old in ('Critters/Mushrooms', 'Critters/Bosses', OLD10):
    assert old not in s, old
files[p] = s.encode()

with zipfile.ZipFile(sys.argv[1], 'w', zipfile.ZIP_DEFLATED) as out:
    for nm in order: out.writestr(nm, files[nm])
print('ok')
