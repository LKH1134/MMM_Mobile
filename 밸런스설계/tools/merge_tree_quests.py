# 2026-11-10 세계수 '의뢰'를 메인 '퀘스트'로 합치고 주간 퀘스트를 더한다. 사용: python merge_tree_quests.py <원본.xlsx> <출력.xlsx>
#  - 퀘스트 시트: period(daily/weekly)·treeExp(받으면 세계수 경험치) 열을 더하고, 일일 5행에 세계수 경험치, 주간 4행(9211~)을 더한다
#    세계수 경험치 몫은 예전 의뢰와 같게 맞춘다: 일일 하루 3,000(예전 의뢰 3개 × 1,000) · 주간 한 주 10,000(예전 4,000+3,000+3,000)
#  - 의뢰 시트(9901~)·상수 treeQuestDaily(9072)를 지운다
#  - info: 퀘스트 설명을 고치고, 의뢰 두 줄은 개편 기록으로 바꾼다
# 엑셀 XML을 직접 고친다 (다른 시트의 서식을 건드리지 않게). add_dex_sets.py와 같은 방식
import re, sys, zipfile
from xml.sax.saxutils import escape

SRC, DST = sys.argv[1], sys.argv[2]

COLS = [
    ('index', 'int', '퀘스트 ID', 12),
    ('key', 'string', '퀘스트 식별자', 18),
    ('period', 'string', 'daily 일일(한국 00시 초기화) / weekly 주간(월요일 한국 00시 초기화)', 14),
    ('kind', 'string', '종류: kill 처치 · stage 보스 도전 · upgrade 강화 · gacha 뽑기 · ad 광고 · dungeon 던전 판 수 · daily 일일 퀘스트 받은 수(주간만)', 20),
    ('target', 'int', '목표 횟수', 12),
    ('goldKills', 'int', '보상 골드 = 받을 때 스테이지 적 처치 골드 × 이 횟수', 22),
    ('mycelite', 'int', '보상 균사석 (고정)', 12),
    ('treeExp', 'int', '보상 세계수 경험치 (고정, 0이면 없음)', 14),
    ('#memo', 'string', '메모', 50),
]
ROWS = [
    (9201, 'quest_kill', 'daily', 'kill', 300, 2000, 75, 600, '몬스터 처치'),
    (9202, 'quest_stage', 'daily', 'stage', 3, 3000, 75, 600, '보스 도전 (벽에서 막혀도 1)'),
    (9203, 'quest_upgrade', 'daily', 'upgrade', 20, 2000, 75, 600, '강화 횟수'),
    (9204, 'quest_gacha', 'daily', 'gacha', 11, 3000, 75, 600, '뽑기 횟수 (11연이면 11)'),
    (9205, 'quest_ad', 'daily', 'ad', 3, 6000, 50, 600, '광고 시청 (광고 상자·뽑기·던전 2배 모두 센다). 세계수 경험치 600 × 5 = 하루 3,000 (예전 일일 의뢰 3개 × 1,000)'),
    (9211, 'week_daily', 'weekly', 'daily', 25, 10000, 100, 3500, '일일 퀘스트 25개 받기 (한 주 최대 35). 예전 주간 의뢰 w_daily 4,000'),
    (9212, 'week_stage', 'weekly', 'stage', 350, 5000, 50, 2500, '보스 도전 350번. 예전 주간 의뢰 w_stage(350, 3,000)'),
    (9213, 'week_kill', 'weekly', 'kill', 40000, 5000, 50, 2500, '몬스터 4만 마리. 예전 주간 의뢰 w_kill(40,000, 3,000)'),
    (9214, 'week_dungeon', 'weekly', 'dungeon', 10, 5000, 50, 1500, '던전 10판 (예전 일일 의뢰 d_dungeon 하루 2판). 주간 세계수 경험치 합 10,000 = 예전 주간 의뢰 합. 골드·균사석은 임시값'),
]


def letter(n):
    out = ''
    while n:
        n, r = divmod(n - 1, 26)
        out = chr(65 + r) + out
    return out


def cstr(ref, text, style):
    return f'<c r="{ref}" s="{style}" t="inlineStr"><is><t xml:space="preserve">{escape(str(text))}</t></is></c>'


def cell(ref, v, num_style=6, str_style=2):
    if isinstance(v, (int, float)) and not isinstance(v, bool):
        return f'<c r="{ref}" s="{num_style}" t="n"><v>{v}</v></c>'
    return cstr(ref, v, str_style)


z = zipfile.ZipFile(SRC)
order = z.namelist()
files = {n: z.read(n) for n in order}
wb = files['xl/workbook.xml'].decode()
rels = files['xl/_rels/workbook.xml.rels'].decode()


def sheet_entry(name):
    return re.search(r'<sheet name="%s" sheetId="\d+"[^>]*r:id="(rId\d+)"\s*/>' % name, wb)


def part_of(rid):
    rel = re.search(r'<Relationship [^>]*Id="%s"[^>]*/>' % rid, rels).group(0)
    return re.search(r'Target="([^"]+)"', rel).group(1).lstrip('/'), rel


# ── 퀘스트 시트 다시 쓰기 ──
qpart, _ = part_of(sheet_entry('퀘스트').group(1))
s = files[qpart].decode()
assert '<t>period</t>' not in s, 'already merged'
rows = ''
for line, style, ht in ((1, 42, '29.1'), (2, 43, '21.95'), (3, 44, '32.1')):
    rows += f'<row r="{line}" ht="{ht}" customHeight="1" s="30">' + ''.join(
        cstr(f'{letter(i + 1)}{line}', col[line - 1], style) for i, col in enumerate(COLS)) + '</row>'
for k, values in enumerate(ROWS):
    line = 4 + k
    rows += f'<row r="{line}" ht="21.95" customHeight="1" s="30">' + ''.join(cell(f'{letter(i + 1)}{line}', v) for i, v in enumerate(values)) + '</row>'
cols = ''.join(f'<col width="{c[3]}" customWidth="1" style="30" min="{i + 1}" max="{i + 1}" />' for i, c in enumerate(COLS))
s = re.sub(r'<cols>.*?</cols>', '<cols>' + cols + '</cols>', s, flags=re.S)
s = re.sub(r'<sheetData>.*?</sheetData>', '<sheetData>' + rows + '</sheetData>', s, flags=re.S)
s = re.sub(r'<dimension ref="[^"]+"\s*/>', f'<dimension ref="A1:{letter(len(COLS))}{3 + len(ROWS)}" />', s)
files[qpart] = s.encode()

# ── 의뢰 시트 지우기 ──
m = sheet_entry('의뢰')
rid = m.group(1)
part, rel = part_of(rid)
assert f'xl/worksheets/_rels/{part.split("/")[-1]}.rels' not in files, 'sheet has rels'
wb = wb.replace(m.group(0), '')
rels = rels.replace(rel, '')
files['xl/workbook.xml'] = wb.encode()
files['xl/_rels/workbook.xml.rels'] = rels.encode()
ct = files['[Content_Types].xml'].decode()
ov = re.search(r'<Override PartName="/%s"[^>]*/>' % re.escape(part), ct).group(0)
files['[Content_Types].xml'] = ct.replace(ov, '').encode()
del files[part]
order.remove(part)
assert '<definedName ' not in wb  # 빈 <definedNames />는 괜찮다

# ── 상수 treeQuestDaily 지우기 (뒤 행 번호를 당긴다) ──
cpart, _ = part_of(sheet_entry('상수').group(1))
s = files[cpart].decode()
head, rest = s.split('<sheetData>', 1)
data, tail = rest.split('</sheetData>', 1)
row_list = re.findall(r'<row r="\d+"[^>]*>.*?</row>', data, flags=re.S)
kept = [r for r in row_list if '<t>treeQuestDaily</t>' not in r]
assert len(kept) == len(row_list) - 1
out = []
for i, row in enumerate(kept, start=1):
    old = int(re.match(r'<row r="(\d+)"', row).group(1))
    if old != i:
        row = re.sub(r'<row r="%d"' % old, f'<row r="{i}"', row, count=1)
        row = re.sub(r'(<c r="[A-Z]+)%d"' % old, r'\g<1>%d"' % i, row)
    out.append(row)
s = head + '<sheetData>' + ''.join(out) + '</sheetData>' + tail
s = re.sub(r'<dimension ref="A1:D\d+"\s*/>', f'<dimension ref="A1:D{len(out)}" />', s)
files[cpart] = s.encode()

# ── info ──
ipart, _ = part_of(sheet_entry('info').group(1))
s = files[ipart].decode()


def set_row(s, n, values):
    m = re.search(r'<row r="%d"[^>]*>.*?</row>' % n, s, flags=re.S)
    style = re.search(r' s="(\d+)"', m.group(0).split('<c ', 1)[1]).group(1)
    new = f'<row r="{n}">' + ''.join(cstr(f'{letter(i + 1)}{n}', v, style) for i, v in enumerate(values)) + '</row>'
    return s.replace(m.group(0), new)


def check(s, n, a, b):
    row = re.search(r'<row r="%d"[^>]*>.*?</row>' % n, s, flags=re.S).group(0)
    assert f'<t>{a}</t>' in row and f'<t>{b}</t>' in row, (n, a, b)


check(s, 107, '시트', '퀘스트'); check(s, 108, '퀘스트', 'kind·target·보상'); check(s, 169, '시트', '의뢰'); check(s, 170, '의뢰', 'kind')
s = set_row(s, 107, ('시트', '퀘스트', '일일·주간 퀘스트 목록 (DailyQuests). period daily는 한국 시각 00시, weekly는 월요일 한국 00시에 진행도·수령 상태가 초기화된다. 퀘스트 창(전투 화면 왼쪽 버튼)의 일일/주간 탭에 행 순서대로 나온다.', '9행 · 일일 9201~ · 주간 9211~'))
s = set_row(s, 108, ('퀘스트', 'kind·target·보상', 'kind = 무엇을 셀지(kill·stage·upgrade·gacha·ad·dungeon·daily — daily는 일일 퀘스트 받은 수, 주간만), target = 목표 횟수, goldKills = 보상 골드(받을 때 스테이지 적 처치 골드 × 횟수), mycelite = 보상 균사석, treeExp = 보상 세계수 경험치. 앱 밖 정산분은 세지 않는다.', '골드·균사석 임시값 — 기획 확정 필요'))
s = set_row(s, 169, ('개편', '2026-11-10 의뢰 → 퀘스트', '세계수 일일·주간 의뢰(의뢰 시트 9901~, 상수 treeQuestDaily)를 지우고 메인 퀘스트로 합쳤다. 세계수 경험치는 퀘스트 treeExp로 받는다 (일일 하루 3,000 · 주간 10,000 — 예전 의뢰와 같은 합). 지정 버섯(species) 의뢰는 없앴다.', '세계수 화면 의뢰 버튼·창 삭제'))
s = set_row(s, 170, ('개편', '2026-11-10 주간 퀘스트', '퀘스트 시트에 period·treeExp 열과 주간 4행(9211~9214)을 더했다. 저장 키: 일일 Quest.<일일 순번>.P/.C·Quest.Day(예전과 같음), 주간 QuestW.<index>.P/.C·QuestW.Week(이번 주 월요일).', '옛 세계수 의뢰 저장값은 읽지 않는다'))
files[ipart] = s.encode()

with zipfile.ZipFile(DST, 'w', zipfile.ZIP_DEFLATED) as o:
    for name in order:
        o.writestr(name, files[name])
print('ok')
