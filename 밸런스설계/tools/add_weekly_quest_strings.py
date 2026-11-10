# 2026-11-10 주간 퀘스트(세계수 의뢰 합침) 문구를 String.xlsx에 더한다. 사용: python add_weekly_quest_strings.py <String.xlsx>
# 엑셀 XML을 직접 고친다 (inlineStr). add_dex_strings.py와 같은 방식
import re, sys, zipfile
from xml.sax.saxutils import escape

PATH = sys.argv[1]
ROWS = [
    ('quest.desc_dungeon', '던전 {0:N0}판 하기', 'Play dungeons {0:N0} times', '의뢰 설명'),
    ('quest.desc_daily', '일일 의뢰 {0:N0}개 완료', 'Complete {0:N0} daily quests', '의뢰 설명 (주간)'),
    ('quest.title_dungeon', '던전 탐험', 'Dungeon Run', '의뢰 카드 제목'),
    ('quest.title_daily', '성실한 하루', 'Daily Routine', '의뢰 카드 제목 (주간: 일일 의뢰 완료 수)'),
    ('quest.tree_exp', '<color=#3E8E3E>세계수 경험치</color> +{0}', '<color=#3E8E3E>Tree EXP</color> +{0}', '의뢰 카드 보상 줄'),
    ('quest.reset_week', '초기화까지 {0}일 {1:00}:{2:00}:{3:00}', 'Resets in {0}d {1:00}:{2:00}:{3:00}', '주간 의뢰 창 {0}=일 {1}=시 {2}=분 {3}=초'),
]

z = zipfile.ZipFile(PATH)
order = z.namelist()
files = {n: z.read(n) for n in order}
z.close()
s = files['xl/worksheets/sheet1.xml'].decode('utf-8')
r = max(int(n) for n in re.findall(r'<row r="(\d+)"', s))
out = ''
for row in ROWS:
    assert f'<t>{row[0]}</t>' not in s, row[0]
    r += 1
    out += f'<row r="{r}">' + ''.join(f'<c r="{c}{r}" t="inlineStr"><is><t>{escape(v)}</t></is></c>' for c, v in zip('ABCD', row)) + '</row>'
s = s.replace('</sheetData>', out + '</sheetData>')
s = re.sub(r'<dimension ref="A1:D\d+" />', f'<dimension ref="A1:D{r}" />', s)
files['xl/worksheets/sheet1.xml'] = s.encode('utf-8')
with zipfile.ZipFile(PATH, 'w', zipfile.ZIP_DEFLATED) as o:
    for n in order:
        o.writestr(n, files[n])
print('ok', r)
