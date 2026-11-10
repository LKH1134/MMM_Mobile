# 2026-11-10 도감(버섯 수집) 세트 보너스. 사용: python add_dex_sets.py <원본.xlsx> <출력.xlsx>
#  - 새 시트 '도감' (18001~18015): 종류(식용·약용·독성) × ★1~5 한 행 = 버섯 무리 하나. 종 수·그림·등장 스테이지·보스 처치당 등장 확률
#  - 새 시트 '도감세트' (18101~18120): 한 무리를 다 모으면 붙는 능력치 % (식용 = 공격력, 약용 = 체력·체력 재생, 독성 = 공격 속도)
#  - info: 두 시트 설명·개편 기록
# 수치 근거는 밸런스설계/도감세트.md
# 엑셀 XML을 직접 고친다 (다른 시트의 서식·표를 건드리지 않게). add_attendance_consts.py와 같은 방식
import re, sys, zipfile
from xml.sax.saxutils import escape

SRC, DST = sys.argv[1], sys.argv[2]

DEX_COLS = [
    ('index', 'int', '도감 무리 ID'),
    ('key', 'string', '도감 무리 식별자'),
    ('kind', 'MushroomKind', '버섯 종류: Edible 식용 · Medicinal 약용 · Toxic 독성'),
    ('star', 'int', '별 개수 1~5 (종류마다 1~5가 하나씩)'),
    ('count', 'int', '이 무리의 버섯 종 수 = 그림 시트 조각 수 (조각 이름 "시트이름_0" ~ "시트이름_count-1"). 다 모으면 세트'),
    ('resourceId', 'int', '버섯 그림 시트 (리소스.index)'),
    ('unlockStage', 'int', '이 스테이지 보스부터 이 무리 버섯이 나온다 (방치 전투 보스 처치)'),
    ('dropChance', 'double', '보스 하나를 잡을 때 이 무리에서 새 버섯 1종이 나올 확률 0~1. 무리마다 따로 정한다 (이미 모은 종은 안 나온다)'),
    ('#memo', 'string', '메모 (게임이 읽지 않는다)'),
]
SET_COLS = [
    ('index', 'int', '세트 효과 ID'),
    ('key', 'string', '세트 효과 식별자'),
    ('kind', 'MushroomKind', '세트 무리의 버섯 종류 (도감.kind)'),
    ('star', 'int', '세트 무리의 별 개수 (도감.star)'),
    ('stat', 'DexStat', '오르는 능력치: Attack 공격력 · Hp 체력 · HpRegen 체력 재생 · Speed 공격 속도'),
    ('value', 'double', '오르는 양(%). Attack·Hp·HpRegen은 장비 보유 효과 %에 더한다, Speed는 최종 공격 속도에 (1 + 합/100)을 곱한다'),
    ('#memo', 'string', '메모 (게임이 읽지 않는다)'),
]

KINDS = [('Edible', 'edible', '식용', 7275, [12, 11, 7, 9, 5]),
         ('Medicinal', 'medicinal', '약용', 7280, [12, 11, 7, 9, 5]),
         ('Toxic', 'toxic', '독성', 7285, [12, 11, 6, 9, 5])]
UNLOCK = [1, 40, 120, 240, 360]
CHANCE = [0.6, 0.25, 0.13, 0.16, 0.1]
TIMING = ['보통 플레이 첫 1시간(스테이지 ~60)에 세트', 'D1.5 전후(스테이지 ~170) 세트', 'D3~4(스테이지 ~270) 세트',
          'D9~13(스테이지 ~400) 세트', '전설 장비 시기(D14~) 이후, D20~30(스테이지 ~470~510) 세트']
DEX_ROWS = []
for k, (kind, key, ko, res0, counts) in enumerate(KINDS):
    for s in range(5):
        DEX_ROWS.append((18001 + k * 5 + s, f'dex_{key}_{s + 1}', kind, s + 1, counts[s], res0 + s, UNLOCK[s], CHANCE[s],
                         f'{ko} ★{s + 1} {counts[s]}종 · {TIMING[s]}' if k == 0 else f'{ko} ★{s + 1} {counts[s]}종'))

# 장비 등급 1레벨 보유 효과(등급.ownBase 2·50·250·1250)를 별에 맞춘다: ★1 일반(10)·★2 희귀 50·★3 영웅 250·★4 영웅과 전설의 중간(√(250×1250)≈560)·★5 전설 1250
# 공격력은 무기처럼 × weaponEffectMul 1.75. 공격 속도는 같은 시기 공격력 세트가 주는 상대 DPS를 맞추되 희석되지 않는 배율이라 낮춘다 (밸런스설계/도감세트.md)
HP = [10, 50, 250, 560, 1250]
ATK = [v * 1.75 for v in HP]
SPD = [3, 6, 10, 16, 30]
SET_ROWS = []
n = 18101
for s in range(5):
    memo = '전설 방어구(세계수 갑옷) 1레벨 보유 효과 1250%와 같다' if s == 4 else ''
    SET_ROWS.append((n, f'set_edible_{s + 1}_attack', 'Edible', s + 1, 'Attack', ATK[s], '전설 무기(날다람쥐) 1레벨 보유 효과 1250×1.75 = 2187.5%와 같다' if s == 4 else '')); n += 1
for s in range(5):
    SET_ROWS.append((n, f'set_medicinal_{s + 1}_hp', 'Medicinal', s + 1, 'Hp', HP[s], memo if s == 4 else '')); n += 1
    SET_ROWS.append((n, f'set_medicinal_{s + 1}_regen', 'Medicinal', s + 1, 'HpRegen', HP[s], '')); n += 1
for s in range(5):
    SET_ROWS.append((n, f'set_toxic_{s + 1}_speed', 'Toxic', s + 1, 'Speed', SPD[s], 'D20~30 전설 무기가 주는 상대 DPS(+35% 정도)에 맞춤' if s == 4 else '')); n += 1

INFO_ROWS = [
    ('시트', '도감', '버섯 도감의 무리(종류 × 별). 방치 전투 보스를 잡을 때마다 unlockStage가 지난 무리마다 dropChance로 아직 없는 버섯 1종을 얻는다. 무리의 count종을 다 모으면 도감세트 효과가 켜진다. 그림 조각 = 리소스 시트 이름_번호.', '15행'),
    ('시트', '도감세트', '도감 무리를 다 모았을 때 붙는 능력치. 공격력·체력·체력 재생 %는 장비 보유 효과 %에 더하고, 공격 속도 %는 최종 공격 속도에 곱한다. 한 무리에 여러 행(약용 = 체력·체력 재생)을 둘 수 있다.', '20행'),
    ('개편', '2026-11-10 도감 세트 보너스', '도감·도감세트 시트 추가 (18001~18015, 18101~18120). 별 등장 시기·확률과 세트 수치 설명은 밸런스설계/도감세트.md. 저장 키 Dex.<도감.index> = 모은 종 비트.', '★5 = 전설 장비 1레벨 보유 효과'),
]


def letter(n):
    out = ''
    while n:
        n, r = divmod(n - 1, 26)
        out = chr(65 + r) + out
    return out


def cstr(ref, text, style=None):
    s = f' s="{style}"' if style is not None else ''
    return f'<c r="{ref}"{s} t="inlineStr"><is><t xml:space="preserve">{escape(str(text))}</t></is></c>'


def cnum(ref, v, style=None):
    s = f' s="{style}"' if style is not None else ''
    return f'<c r="{ref}"{s} t="n"><v>{v}</v></c>'


def cell(ref, v, style=None):
    return cnum(ref, v, style) if isinstance(v, (int, float)) and not isinstance(v, bool) else cstr(ref, v, style)


def sheet_path(files, name):
    wb = files['xl/workbook.xml'].decode()
    rels = files['xl/_rels/workbook.xml.rels'].decode()
    rid = re.search(r'<sheet name="%s" sheetId="\d+"[^>]*r:id="(rId\d+)"' % name, wb).group(1)
    target = re.search(r'Target="([^"]+)" Id="%s"' % rid, rels).group(1).lstrip('/')
    return target if target.startswith('xl/') else 'xl/' + target


def append_rows(files, name, rows, style=None):
    path = sheet_path(files, name)
    s = files[path].decode()
    r = max(int(n) for n in re.findall(r'<row r="(\d+)"', s))
    out = ''
    for values in rows:
        r += 1
        out += f'<row r="{r}">' + ''.join(cell(f'{letter(i + 1)}{r}', v, style) for i, v in enumerate(values)) + '</row>'
    s = s.replace('</sheetData>', out + '</sheetData>')
    dim = re.search(r'<dimension ref="A1:([A-Z]+)(\d+)"\s*/>', s)
    s = s.replace(dim.group(0), f'<dimension ref="A1:{dim.group(1)}{r}" />')
    files[path] = s.encode()


def add_sheet(files, order, name, cols, data):
    assert f'<sheet name="{name}"' not in files['xl/workbook.xml'].decode(), name
    widths = ''.join(f'<col width="{14 if not c[0].startswith("#") else 48}" customWidth="1" style="30" min="{i + 1}" max="{i + 1}" />' for i, c in enumerate(cols))
    rows = ''
    for line, style in ((1, 15), (2, 16), (3, 17)):
        rows += f'<row r="{line}">' + ''.join(cstr(f'{letter(i + 1)}{line}', col[line - 1], style) for i, col in enumerate(cols)) + '</row>'
    for k, values in enumerate(data):
        line = 4 + k
        rows += f'<row r="{line}">' + ''.join(cell(f'{letter(i + 1)}{line}', v) for i, v in enumerate(values) if v != '') + '</row>'
    last = f'{letter(len(cols))}{3 + len(data)}'
    sheet = ('<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetPr><outlinePr summaryBelow="1" summaryRight="1" /><pageSetUpPr /></sheetPr>'
             f'<dimension ref="A1:{last}" /><sheetViews><sheetView workbookViewId="0"><selection activeCell="A1" sqref="A1" /></sheetView></sheetViews>'
             f'<sheetFormatPr baseColWidth="8" defaultRowHeight="15" /><cols>{widths}</cols><sheetData>{rows}</sheetData>'
             '<pageMargins left="0.7" right="0.7" top="0.75" bottom="0.75" header="0.3" footer="0.3" /></worksheet>')
    n = 1
    while f'xl/worksheets/sheet{n}.xml' in files:
        n += 1
    part = f'xl/worksheets/sheet{n}.xml'
    files[part] = sheet.encode()
    order.insert(order.index('xl/styles.xml'), part)

    rels = files['xl/_rels/workbook.xml.rels'].decode()
    rid = 'rId%d' % (max(int(x) for x in re.findall(r'Id="rId(\d+)"', rels)) + 1)
    rels = rels.replace('</Relationships>', f'<Relationship Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="/{part}" Id="{rid}" /></Relationships>')
    files['xl/_rels/workbook.xml.rels'] = rels.encode()

    wb = files['xl/workbook.xml'].decode()
    sheet_id = max(int(x) for x in re.findall(r'sheetId="(\d+)"', wb)) + 1
    wb = wb.replace('</sheets>', f'<sheet name="{name}" sheetId="{sheet_id}" state="visible" r:id="{rid}" /></sheets>')
    files['xl/workbook.xml'] = wb.encode()

    ct = files['[Content_Types].xml'].decode()
    ct = ct.replace('</Types>', f'<Override PartName="/{part}" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" /></Types>')
    files['[Content_Types].xml'] = ct.encode()
    print('sheet', name, part, rid, 'sheetId', sheet_id)


z = zipfile.ZipFile(SRC)
order = z.namelist()
files = {n: z.read(n) for n in order}

add_sheet(files, order, '도감', DEX_COLS, DEX_ROWS)
add_sheet(files, order, '도감세트', SET_COLS, SET_ROWS)
append_rows(files, 'info', INFO_ROWS, style=39)

with zipfile.ZipFile(DST, 'w', zipfile.ZIP_DEFLATED) as out:
    for name in order:
        out.writestr(name, files[name])
print('ok')
