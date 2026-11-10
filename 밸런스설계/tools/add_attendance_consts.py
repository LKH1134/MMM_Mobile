# 2026-11-10 코드에 있던 콘텐츠 수치를 표로 옮긴다. 사용: python add_attendance_consts.py <원본.xlsx> <출력.xlsx>
#  - 새 시트 '출석' (7일 출석 보상, 코드 Attendance.Rewards였던 값)
#  - 상수: 광고 상자 보상 확률·연속 상한, 보스 러시 쉬운 레벨, 기능 해금 스테이지 (코드 const였던 값)
#  - 리소스: UI/Icon/advertisement_mycelite (광고 상자 균사석 그림, 미등록)
#  - info: 출석 시트 설명·개편 기록
# 엑셀 XML을 직접 고친다 (다른 시트의 서식·표를 건드리지 않게)
import re, sys, zipfile
from xml.sax.saxutils import escape

SRC, DST = sys.argv[1], sys.argv[2]

ATTENDANCE_COLS = [
    ('index', 'int', '출석 ID'),
    ('key', 'string', '출석 식별자'),
    ('day', 'int', 'N일째 (1부터 빠짐없이). 행 수 = 출석 일수 (출석 창 칸 수와 같아야 한다)'),
    ('kind', 'AttendanceRewardKind', 'Gold = 골드 (받는 순간 스테이지 적 처치 골드 × amount) · Mycelite = 균사석 amount개 · Weapon = 무기 (equipId, 이미 있으면 조각 +1)'),
    ('amount', 'long', '골드는 적 처치 수, 균사석은 개수, 무기는 1'),
    ('equipId', 'int', 'Weapon일 때 장비.index (무기). 다른 종류는 0'),
    ('#memo', 'string', '메모 (게임이 읽지 않는다)'),
]
ATTENDANCE_ROWS = [
    (17001, 'day_1', 1, 'Mycelite', 500, 0, ''),
    (17002, 'day_2', 2, 'Mycelite', 500, 0, ''),
    (17003, 'day_3', 3, 'Mycelite', 500, 0, ''),
    (17004, 'day_4', 4, 'Mycelite', 1000, 0, ''),
    (17005, 'day_5', 5, 'Mycelite', 1500, 0, ''),
    (17006, 'day_6', 6, 'Mycelite', 2000, 0, '1~6일 균사석 합계 6,000'),
    (17007, 'day_7', 7, 'Weapon', 1, 11010, '전설 무기 날다람쥐 (weapon_09)'),
]
CONSTS = [
    (9090, 'adBoxGoldChance', 0.5, '광고 상자가 골드 상자로 나올 확률 (나머지는 균사석 상자). 0~1'),
    (9091, 'adBoxMaxSameInRow', 2, '광고 상자 같은 보상이 연속으로 나올 수 있는 최대 횟수 (넘으면 다른 쪽이 나온다). 0 = 제한 없음'),
    (9092, 'bossRushEasyLevels', 5, '보스 러시(던전 4): 이 레벨까지는 기준 스테이지 그대로, 그 뒤부터 레벨마다 stageStep씩 강해진다'),
    (9093, 'unlockTeamStage', 3, '편성 탭 해금 스테이지 (진입 기준 — 3 = 1-2를 깨고 1-3 진입). 소환 탭은 편성 화면을 처음 열면 열린다'),
    (9094, 'unlockWorldTreeStage', 7, '세계수 탭 해금 스테이지 (진입 기준). 던전 탭은 던전 시트 unlockStage 중 가장 작은 값'),
]
RESOURCE = (7425, 'asset_ui_icon_advertisement_mycelite', 'Sprite', 'UI/Icon/advertisement_mycelite')
INFO_ROWS = [
    ('시트', '출석', '7일 출석 체크 보상. 한 행 = 하루 (day 1부터). 행 수가 출석 일수다 — 출석 창(AttendanceScreen) 칸 수와 같아야 한다. 무기 보상은 장비.index로 고른다.', '7행'),
    ('개편', '2026-11-10 코드 수치 표로', '코드에 있던 콘텐츠 수치를 표로 옮김: 출석 보상 7일 → 출석 시트(17001~17007, 무기는 이름 대신 장비.index 11010). 상수 9090~9094 adBoxGoldChance·adBoxMaxSameInRow·bossRushEasyLevels·unlockTeamStage·unlockWorldTreeStage. 리소스 7425 광고 상자 균사석 그림. 방치 보스 체력 꼬리의 챕터 길이는 방치전투.stagesPerChapter를 읽는다.', '값은 옮기기 전과 같다'),
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
    existing = [int(n) for n in re.findall(r'<row r="(\d+)"', s)]
    r = max(existing)
    width = 0
    out = ''
    for values in rows:
        r += 1
        width = max(width, len(values))
        out += f'<row r="{r}">' + ''.join(cell(f'{letter(i + 1)}{r}', v, style) for i, v in enumerate(values)) + '</row>'
    s = s.replace('</sheetData>', out + '</sheetData>')
    dim = re.search(r'<dimension ref="A1:([A-Z]+)(\d+)"\s*/>', s)
    s = s.replace(dim.group(0), f'<dimension ref="A1:{dim.group(1)}{r}" />')
    files[path] = s.encode()


def has_key(files, name, key):
    return f'<t>{key}</t>' in files[sheet_path(files, name)].decode() or f'>{key}</t>' in files[sheet_path(files, name)].decode()


z = zipfile.ZipFile(SRC)
order = z.namelist()
files = {n: z.read(n) for n in order}

# 상수·리소스·info
for c in CONSTS:
    assert not has_key(files, '상수', c[1]), c[1]
append_rows(files, '상수', CONSTS)
assert not has_key(files, '리소스', RESOURCE[3])
append_rows(files, '리소스', [RESOURCE], style=53)
append_rows(files, 'info', INFO_ROWS, style=39)

# 새 시트 출석 (꼬마진화 시트와 같은 모양: 1행 열 이름 s15, 2행 자료형 s16, 3행 설명 s17)
assert '<sheet name="출석"' not in files['xl/workbook.xml'].decode()
cols = ''.join(f'<col width="{14 if i < 6 else 36}" customWidth="1" style="30" min="{i + 1}" max="{i + 1}" />' for i in range(len(ATTENDANCE_COLS)))
rows = ''
for line, style in ((1, 15), (2, 16), (3, 17)):
    rows += f'<row r="{line}">' + ''.join(cstr(f'{letter(i + 1)}{line}', col[line - 1], style) for i, col in enumerate(ATTENDANCE_COLS)) + '</row>'
for k, values in enumerate(ATTENDANCE_ROWS):
    line = 4 + k
    rows += f'<row r="{line}">' + ''.join(cell(f'{letter(i + 1)}{line}', v) for i, v in enumerate(values) if v != '') + '</row>'
last = f'{letter(len(ATTENDANCE_COLS))}{3 + len(ATTENDANCE_ROWS)}'
sheet = ('<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetPr><outlinePr summaryBelow="1" summaryRight="1" /><pageSetUpPr /></sheetPr>'
         f'<dimension ref="A1:{last}" /><sheetViews><sheetView workbookViewId="0"><selection activeCell="A1" sqref="A1" /></sheetView></sheetViews>'
         f'<sheetFormatPr baseColWidth="8" defaultRowHeight="15" /><cols>{cols}</cols><sheetData>{rows}</sheetData>'
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
wb = wb.replace('</sheets>', f'<sheet name="출석" sheetId="{sheet_id}" state="visible" r:id="{rid}" /></sheets>')
files['xl/workbook.xml'] = wb.encode()

ct = files['[Content_Types].xml'].decode()
ct = ct.replace('</Types>', f'<Override PartName="/{part}" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" /></Types>')
files['[Content_Types].xml'] = ct.encode()

with zipfile.ZipFile(DST, 'w', zipfile.ZIP_DEFLATED) as out:
    for name in order:
        out.writestr(name, files[name])
print('ok', part, rid, 'sheetId', sheet_id)
