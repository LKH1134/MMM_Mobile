# 2026-11-10 KHD 룰렛(8e6fb58까지)을 LKH 표에 합친다 (엑셀은 git이 못 합쳐서 시트 단위로).
# 사용: python merge_khd_roulette.py <LKH GameBalance.xlsx> <KHD GameBalance.xlsx> <출력> <LKH String.xlsx> <KHD String.xlsx> <출력>
#  - GameBalance: KHD 룰렛 시트를 더한다. ID가 LKH와 겹쳐 바꾼다 — 룰렛 18001~18006 → 18201~18206 (도감 18001~와 겹침),
#    상수 rouletteDailyCount·rouletteCooldownMinutes 9099·9100 → 9104·9105 (LKH 광고 상자 상수 9099·9100과 겹침). info에 룰렛 줄
#  - String: KHD가 고친 data.reset_title·data.reset_msg는 KHD 값으로, KHD가 더한 key는 뒤에 붙인다
import re, sys, zipfile
from xml.sax.saxutils import escape

GB_LKH, GB_KHD, GB_OUT, ST_LKH, ST_KHD, ST_OUT = sys.argv[1:7]
ROULETTE_SHIFT = 200
CONST_IDS = {'rouletteDailyCount': 9104, 'rouletteCooldownMinutes': 9105}


def read(path):
    z = zipfile.ZipFile(path)
    order = z.namelist()
    return order, {n: z.read(n) for n in order}


def write(path, order, files):
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as o:
        for n in order:
            o.writestr(n, files[n])


def sheet_part(files, name):
    wb = files['xl/workbook.xml'].decode()
    rels = files['xl/_rels/workbook.xml.rels'].decode()
    rid = re.search(r'<sheet name="%s" sheetId="\d+"[^>]*r:id="(rId\d+)"' % name, wb).group(1)
    rel = re.search(r'<Relationship [^>]*Id="%s"[^>]*/>' % rid, rels).group(0)
    t = re.search(r'Target="([^"]+)"', rel).group(1).lstrip('/')
    return t if t.startswith('xl/') else 'xl/' + t


def cstr(ref, text, style=None):
    s = f' s="{style}"' if style is not None else ''
    return f'<c r="{ref}"{s} t="inlineStr"><is><t xml:space="preserve">{escape(str(text))}</t></is></c>'


def append_rows(files, part, rows_xml):
    s = files[part].decode()
    r = max(int(n) for n in re.findall(r'<row r="(\d+)"', s))
    out = ''
    for build in rows_xml:
        r += 1
        out += build(r)
    s = s.replace('</sheetData>', out + '</sheetData>')
    dim = re.search(r'<dimension ref="A1:([A-Z]+)(\d+)"\s*/>', s)
    s = s.replace(dim.group(0), f'<dimension ref="A1:{dim.group(1)}{r}" />')
    files[part] = s.encode()


def renumber_row(row, n):
    old = int(re.match(r'<row r="(\d+)"', row).group(1))
    row = re.sub(r'<row r="%d"' % old, f'<row r="{n}"', row, count=1)
    return re.sub(r'(<c r="[A-Z]+)%d"' % old, r'\g<1>%d"' % n, row)


# ── GameBalance ──
order, files = read(GB_LKH)
_, khd = read(GB_KHD)
assert '<sheet name="룰렛"' not in files['xl/workbook.xml'].decode(), 'already merged'

sheet = khd[sheet_part(khd, '룰렛')].decode()
for i in range(1, 100):
    sheet = sheet.replace(f'<v>{18000 + i}</v>', f'<v>{18000 + ROULETTE_SHIFT + i}</v>')
n = 1
while f'xl/worksheets/sheet{n}.xml' in files:
    n += 1
part = f'xl/worksheets/sheet{n}.xml'
files[part] = sheet.encode()
order.insert(order.index('xl/styles.xml'), part)
rels = files['xl/_rels/workbook.xml.rels'].decode()
rid = 'rId%d' % (max(int(x) for x in re.findall(r'Id="rId(\d+)"', rels)) + 1)
files['xl/_rels/workbook.xml.rels'] = rels.replace('</Relationships>', f'<Relationship Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="/{part}" Id="{rid}" /></Relationships>').encode()
wb = files['xl/workbook.xml'].decode()
sid = max(int(x) for x in re.findall(r'sheetId="(\d+)"', wb)) + 1
# KHD처럼 뽑기상품 뒤에 둔다
anchor = re.search(r'<sheet name="뽑기상품"[^>]*/>', wb).group(0)
files['xl/workbook.xml'] = wb.replace(anchor, anchor + f'<sheet name="룰렛" sheetId="{sid}" state="visible" r:id="{rid}" />').encode()
ct = files['[Content_Types].xml'].decode()
files['[Content_Types].xml'] = ct.replace('</Types>', f'<Override PartName="/{part}" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" /></Types>').encode()

kc = khd[sheet_part(khd, '상수')].decode()
cpart = sheet_part(files, '상수')
lc = files[cpart].decode()
builders = []
for key, new_id in CONST_IDS.items():
    assert f'<t>{key}</t>' not in lc, key
    assert f'<v>{new_id}</v>' not in lc, new_id
    row = re.search(r'<row r="\d+"[^>]*>(?:(?!</row>).)*<t>%s</t>.*?</row>' % key, kc, re.S).group(0)
    row = re.sub(r'(<c r="A\d+"[^>]*><v>)\d+(</v>)', r'\g<1>%d\g<2>' % new_id, row, count=1)
    builders.append(lambda r, row=row: renumber_row(row, r))
append_rows(files, cpart, builders)

INFO = ('시트', '룰렛', '룰렛 원판 6칸 (Roulette, KHD 8e6fb58). 행 순서 = 원판 시계 방향 0번부터. kind Gold = 지금 스테이지 적 1마리 골드 × amount, Mycelite = amount개, weight = 나올 비중. 하루 횟수·대기 시간은 상수 rouletteDailyCount·rouletteCooldownMinutes.',
        '6행 · 18201~ (LKH 합칠 때 18001~에서 옮김)')
append_rows(files, sheet_part(files, 'info'), [lambda r: f'<row r="{r}">' + ''.join(cstr(f'{c}{r}', v, 39) for c, v in zip('ABCD', INFO)) + '</row>'])
write(GB_OUT, order, files)
print('GameBalance ok', part, rid)

# ── String ──
order, files = read(ST_LKH)
_, khd = read(ST_KHD)
P = 'xl/worksheets/sheet1.xml'
ls, ks = files[P].decode(), khd[P].decode()


def rows_by_key(s):
    out = {}
    for row in re.findall(r'<row r="\d+"[^>]*>.*?</row>', s, re.S):
        m = re.search(r'<c r="A\d+"[^>]*><is><t[^>]*>([^<]*)</t>', row)
        if m:
            out[m.group(1)] = row
    return out


lk, kk = rows_by_key(ls), rows_by_key(ks)
for key in ('data.reset_title', 'data.reset_msg'):
    n = int(re.match(r'<row r="(\d+)"', lk[key]).group(1))
    ls = ls.replace(lk[key], renumber_row(kk[key], n))
files[P] = ls.encode()
added = [k for k in kk if k not in lk]
append_rows(files, P, [lambda r, row=kk[k]: renumber_row(row, r) for k in added])
write(ST_OUT, order, files)
print('String ok, added', len(added), added)

# 합친 뒤 손으로 한 것 (기록): 리소스 7252 asset_sprite_bg_imsi · Sprite/BG/imsi → asset_sprite_bg_image · Sprite/BG/image
# (KHD 8e6fb58이 imsi.png를 image.png로 바꿔 검사기가 '찾지 못했다'를 냄. 7252를 쓰는 곳은 없다)
