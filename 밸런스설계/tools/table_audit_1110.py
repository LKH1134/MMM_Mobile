# 2026-11-10 데이터 전수 조사 2차: 코드에 남은 콘텐츠 수치·미등록 리소스를 표로, 시트 사이 index 겹침 정리.
# 사용: python table_audit_1110.py <원본.xlsx> <출력.xlsx>
#  - 상수 번호: 9001~9099가 차서 9099~9105(9101~는 던전과 겹침)를 새 대역 19001~19007로 옮긴다
#  - 출석: 17001~17007(가이드 17001~과 겹침) → 19101~19107
#  - 새 시트 '공격속도단계'(19201~): 코드 IdleProgress.SpeedSteps였던 공격력 → 공격 속도 단계표
#  - 상수 19008~19013: 가이드 대체 목표(guideFallback*), 증분 강화 구간(upgInc*) — 코드 const였던 값
#  - 리소스 7426~7437: Resources에 있는데 등록 안 된 그림 12개. 업그레이드.resourceId를 실제 강화 줄 아이콘으로
#  - info: 새 시트 설명·개편 기록, 이전 기록의 번호 고침
# 엑셀 XML을 직접 고친다 (다른 시트의 서식·표를 건드리지 않게)
import re, sys, zipfile
from xml.sax.saxutils import escape

SRC, DST = sys.argv[1], sys.argv[2]

CONST_MOVE = {9099: 19001, 9100: 19002, 9101: 19003, 9102: 19004, 9103: 19005, 9104: 19006, 9105: 19007}
ATTEND_MOVE = {17001 + i: 19101 + i for i in range(7)}
NEW_CONSTS = [
    (19008, 'guideFallbackKills', 50, '가이드: 할 수 있는 반복 목표(가이드반복)가 하나도 없을 때 대신 내는 몬스터 처치 목표 수'),
    (19009, 'guideFallbackMycelite', 50, '가이드: 위 대체 목표의 보상 균사석'),
    (19010, 'upgIncLinearLevel', 100, '증분(Incremental) 강화: 이 레벨까지 값은 레벨마다 perLevel씩, 비용은 아래 두 구간으로 일정하게 오른다. 그 뒤로 증가량이 커진다'),
    (19011, 'upgIncCostStep1Level', 50, '증분 강화 비용: 이 레벨까지 레벨마다 upgIncCostStep1골드씩, 그 뒤 upgIncLinearLevel까지 upgIncCostStep2골드씩 오른다'),
    (19012, 'upgIncCostStep1', 1, '증분 강화 비용 첫 구간의 레벨당 증가 골드'),
    (19013, 'upgIncCostStep2', 2, '증분 강화 비용 둘째 구간의 레벨당 증가 골드'),
]
SPEED_COLS = [
    ('index', 'int', '단계 ID'),
    ('key', 'string', '단계 식별자'),
    ('gateLevel', 'int', '공격력 강화가 이 레벨 이상이면 (첫 행 = 공격 속도 해금 레벨)'),
    ('capLevel', 'int', '공격 속도를 이 레벨까지 올릴 수 있다 (마지막 행 ≤ 공격 속도 maxLevel)'),
    ('#memo', 'string', '메모 (게임이 읽지 않는다)'),
]
SPEED_ROWS = [
    (19201, 'speed_step_1', 20, 30, '해금. 속도 1.5(Lv 87.5)까지 30레벨씩'),
    (19202, 'speed_step_2', 375, 60, ''),
    (19203, 'speed_step_3', 730, 90, ''),
    (19204, 'speed_step_4', 1085, 130, '2.0(Lv 150)까지 40레벨씩'),
    (19205, 'speed_step_5', 1440, 170, ''),
    (19206, 'speed_step_6', 1795, 220, '3.0(Lv 275)까지 50레벨씩'),
    (19207, 'speed_step_7', 2150, 270, ''),
    (19208, 'speed_step_8', 2500, 275, '3.0/s 마스터'),
]
NEW_RESOURCES = [
    (7426, 'asset_sprite_bg_image2', 'Sprite', 'Sprite/BG/image2'),
    (7427, 'asset_ui_icon_upgrade_icon_atk_icon', 'Sprite', 'UI/Icon/Upgrade_Icon/atk_icon'),
    (7428, 'asset_ui_icon_upgrade_icon_atk_speed_icon', 'Sprite', 'UI/Icon/Upgrade_Icon/atk_speed_icon'),
    (7429, 'asset_ui_icon_upgrade_icon_big_damage', 'Sprite', 'UI/Icon/Upgrade_Icon/big_damage'),
    (7430, 'asset_ui_icon_upgrade_icon_crit_damage', 'Sprite', 'UI/Icon/Upgrade_Icon/crit_damage'),
    (7431, 'asset_ui_icon_upgrade_icon_crit_percent_icon', 'Sprite', 'UI/Icon/Upgrade_Icon/crit_percent_icon'),
    (7432, 'asset_ui_icon_upgrade_icon_hp', 'Sprite', 'UI/Icon/Upgrade_Icon/Hp'),
    (7433, 'asset_ui_icon_upgrade_icon_normal_enemy_damage', 'Sprite', 'UI/Icon/Upgrade_Icon/normal_enemy_damage'),
    (7434, 'asset_ui_icon_upgrade_icon_regen', 'Sprite', 'UI/Icon/Upgrade_Icon/regen'),
    (7435, 'asset_ui_icon_warning', 'Sprite', 'UI/Icon/warning'),
    (7436, 'asset_ui_roulette_pin', 'Sprite', 'UI/roulette_pin'),
    (7437, 'asset_ui_roulette_wheel', 'Sprite', 'UI/roulette_wheel'),
]
# 업그레이드.resourceId: 옛 Art/Dream/Icons 그림(7021~7028) → UpgradeNotebook 프리팹 줄에 실제로 놓인 그림 (공격력 = UI/Icon/sword 7366)
UPGRADE_ICON = {7021: 7366, 7022: 7432, 7023: 7434, 7024: 7428, 7025: 7431, 7026: 7430, 7027: 7429, 7028: 7433}
INFO_ROWS = [
    ('시트', '공격속도단계', '공격력 강화 레벨 → 올릴 수 있는 공격 속도 최대 레벨 단계표 (IdleProgress.SpeedCap). gateLevel·capLevel은 오름차순, 첫 gateLevel = 공격 속도 해금 레벨, 마지막 capLevel ≤ 공격 속도 maxLevel.', '8행'),
    ('개편', '2026-11-10 데이터 전수 조사 2차', '코드에 남은 콘텐츠 값을 표로: 공격 속도 단계표 → 공격속도단계 시트(19201~), 증분 강화 구간·가이드 대체 목표 → 상수 19008~19013. 리소스 7426~7437(미등록 그림 12개), 업그레이드 아이콘을 실제 강화 줄 그림으로. 시트끼리 겹치던 index 정리: 상수 9099~9105 → 19001~19007(9101~은 던전 번호, 상수는 9001~9099·19001~19099 두 대역), 출석 17001~ → 19101~(가이드 번호와 같았다). 검증기가 시트끼리 겹치는 index를 막는다.', '값은 옮기기 전과 같다'),
]
# 예전 info 기록의 번호를 고친다 (옮긴 번호)
INFO_FIX = [
    ('출석 시트(17001~17007,', '출석 시트(17001~17007 → 11-10 19101~19107,'),
    ('상수 9099~9103 adBoxGoldChance', '상수 9099~9103(→ 11-10 19001~19005) adBoxGoldChance'),
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


def get(files, name):
    return files[sheet_path(files, name)].decode()


def put(files, name, s):
    files[sheet_path(files, name)] = s.encode()


def append_rows(files, name, rows, style=None):
    s = get(files, name)
    r = max(int(n) for n in re.findall(r'<row r="(\d+)"', s))
    out = ''
    for values in rows:
        r += 1
        out += f'<row r="{r}">' + ''.join(cell(f'{letter(i + 1)}{r}', v, style) for i, v in enumerate(values) if v != '') + '</row>'
    s = s.replace('</sheetData>', out + '</sheetData>')
    dim = re.search(r'<dimension ref="A1:([A-Z]+)(\d+)"\s*/>', s)
    if dim:
        s = s.replace(dim.group(0), f'<dimension ref="A1:{dim.group(1)}{r}" />')
    put(files, name, s)


def renumber(files, name, column, moves):
    s = get(files, name)
    for old, new in moves.items():
        pat = re.compile(r'(<c r="%s\d+"[^>]*>)<v>%d</v>' % (column, old))
        s, n = pat.subn(lambda m: m.group(1) + f'<v>{new}</v>', s)
        assert n == 1, (name, column, old, n)
    put(files, name, s)


def ent(text):
    return ''.join(c if ord(c) < 128 else '&#%d;' % ord(c) for c in text)


def replace_text(files, name, a, b):
    s = get(files, name)
    for x, y in ((escape(a), escape(b)), (ent(escape(a)), ent(escape(b)))):
        if x in s:
            assert s.count(x) == 1, (name, a)
            put(files, name, s.replace(x, y))
            return
    raise AssertionError(('text not found', name, a))


def has_text(files, name, text):
    s = get(files, name)
    return escape(text) in s or ent(escape(text)) in s


def add_sheet(files, order, title, cols, rows):
    assert f'<sheet name="{title}"' not in files['xl/workbook.xml'].decode()
    colxml = ''.join(f'<col width="{14 if i < len(cols) - 1 else 36}" customWidth="1" style="30" min="{i + 1}" max="{i + 1}" />' for i in range(len(cols)))
    body = ''
    for line, style in ((1, 15), (2, 16), (3, 17)):
        body += f'<row r="{line}">' + ''.join(cstr(f'{letter(i + 1)}{line}', col[line - 1], style) for i, col in enumerate(cols)) + '</row>'
    for k, values in enumerate(rows):
        line = 4 + k
        body += f'<row r="{line}">' + ''.join(cell(f'{letter(i + 1)}{line}', v) for i, v in enumerate(values) if v != '') + '</row>'
    last = f'{letter(len(cols))}{3 + len(rows)}'
    sheet = ('<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetPr><outlinePr summaryBelow="1" summaryRight="1" /><pageSetUpPr /></sheetPr>'
             f'<dimension ref="A1:{last}" /><sheetViews><sheetView workbookViewId="0"><selection activeCell="A1" sqref="A1" /></sheetView></sheetViews>'
             f'<sheetFormatPr baseColWidth="8" defaultRowHeight="15" /><cols>{colxml}</cols><sheetData>{body}</sheetData>'
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
    files['xl/workbook.xml'] = wb.replace('</sheets>', f'<sheet name="{title}" sheetId="{sheet_id}" state="visible" r:id="{rid}" /></sheets>').encode()
    ct = files['[Content_Types].xml'].decode()
    files['[Content_Types].xml'] = ct.replace('</Types>', f'<Override PartName="/{part}" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" /></Types>').encode()
    return part


z = zipfile.ZipFile(SRC)
order = z.namelist()
files = {n: z.read(n) for n in order}

renumber(files, '상수', 'A', CONST_MOVE)
renumber(files, '출석', 'A', ATTEND_MOVE)
for c in NEW_CONSTS:
    assert not has_text(files, '상수', c[1]), c[1]
append_rows(files, '상수', NEW_CONSTS)
for r in NEW_RESOURCES:
    assert not has_text(files, '리소스', r[3]), r[3]
append_rows(files, '리소스', NEW_RESOURCES, style=53)
renumber(files, '업그레이드', 'O', UPGRADE_ICON)
part = add_sheet(files, order, '공격속도단계', SPEED_COLS, SPEED_ROWS)
for a, b in INFO_FIX:
    replace_text(files, 'info', a, b)
append_rows(files, 'info', INFO_ROWS, style=39)

with zipfile.ZipFile(DST, 'w', zipfile.ZIP_DEFLATED) as out:
    for name in order:
        out.writestr(name, files[name])
print('ok', part)
