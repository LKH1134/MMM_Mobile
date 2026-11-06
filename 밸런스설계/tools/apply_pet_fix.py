# 펫 삭제 보정 (11-05, verify#20 조건부 통과 · 사용자 수용): theory#19 ①+② 적용.
#   ① 상수 9081 weaponEffectMul = 1.75 (Equipment.BaseOwnPercent 무기만 곱함)
#   ② 스테이지 421~700 enemyHp·bossHp = theory/pet_hp_late2.csv 새 값 (k = 1@420 → 0.55@600 → 0.35@700 로그 보간)
#   사용: python apply_pet_fix.py <출력.xlsx>   (원본은 Assets/Data/Tables/GameBalance.xlsx)
import csv, os, re, sys, zipfile
from xml.sax.saxutils import escape

SRC = r'C:\Project\MMM_Mobile\Assets\Data\Tables\GameBalance.xlsx'
CSV = os.path.join(os.path.dirname(__file__), '..', 'theory', 'pet_hp_late2.csv')
CONST = (9081, 'weaponEffectMul', 1.75, '무기 보유·장착 효과 배율(무기만, 방어구는 1). 펫 삭제로 빠진 공격% 보정 (11-05, 검증.md P절)')

z = zipfile.ZipFile(SRC); order = z.namelist(); files = {n: z.read(n) for n in order}
wb = files['xl/workbook.xml'].decode(); rels = files['xl/_rels/workbook.xml.rels'].decode()


def path(name):
    rid = re.search(r'r:id="(rId\d+)"', re.search(r'<sheet [^>]*name="%s"[^>]*/>' % name, wb).group(0)).group(1)
    t = re.search(r'Target="([^"]+)"', re.search(r'<Relationship [^>]*Id="%s"[^>]*/>' % rid, rels).group(0)).group(1)
    return 'xl/' + t.lstrip('/').replace('xl/', '', 1)


def fmt(x):
    x = float(x)
    return str(int(x)) if x == int(x) and abs(x) < 1e15 else repr(x)


# ② 스테이지 시트 (스테이지 s = ID 5000+s)
new = {int(r['stage']): r for r in csv.DictReader(open(CSV, encoding='utf-8'))}
assert sorted(new) == list(range(421, 701))
p = path('스테이지'); s = files[p].decode(); done = 0
for st, r in new.items():
    m = re.search(r'<row r="\d+"[^>]*>(?:(?!</row>).)*?<c r="A(\d+)"[^>]*><v>%d</v></c>.*?</row>' % (5000 + st), s, re.S)
    row = m.group(0); rn = m.group(1); out = row
    for col, old, nv in (('T', 'enemyHp_now', 'enemyHp_new'), ('U', 'bossHp_now', 'bossHp_new')):
        c = re.search(r'<c r="%s%s"([^>]*)><v>([^<]*)</v></c>' % (col, rn), out)
        assert abs(float(c.group(2)) / float(r[old]) - 1) < 1e-9, (st, col, c.group(2), r[old])
        out = out.replace(c.group(0), f'<c r="{col}{rn}"{c.group(1)}><v>{fmt(r[nv])}</v></c>')
    s = s.replace(row, out); done += 1
files[p] = s.encode()

# ① 상수 시트: 마지막 행을 본떠 한 행
p = path('상수'); s = files[p].decode()
assert f'<t>{CONST[1]}</t>' not in s
dim = re.search(r'<dimension ref="A1:D(\d+)"/>', s); lastrow = int(dim.group(1))
assert max(int(v) for v in re.findall(r'<c r="A\d+"[^>]*><v>(9\d{3})</v></c>', s)) + 1 == CONST[0]
tmpl = re.findall(r'<row r="\d+"[^>]*>.*?</row>', s, flags=re.S)[-1]
ln = lastrow + 1
def sty(c):     # 마지막 행 칸 스타일 (없으면 생략)
    m = re.search(r'<c r="%s%d" s="(\d+)"' % (c, lastrow), tmpl)
    return f' s="{m.group(1)}"' if m else ''
sa, sb, sc, sd = (sty(c) for c in 'ABCD')
row = (f'<row r="{ln}"><c r="A{ln}"{sa} t="n"><v>{CONST[0]}</v></c><c r="B{ln}"{sb} t="inlineStr"><is><t>{CONST[1]}</t></is></c>'
       f'<c r="C{ln}"{sc} t="n"><v>{CONST[2]}</v></c><c r="D{ln}"{sd} t="inlineStr"><is><t>{escape(CONST[3])}</t></is></c></row>')
s = s.replace('</sheetData>', row + '</sheetData>').replace(dim.group(0), f'<dimension ref="A1:D{ln}"/>')
files[p] = s.encode()

# info 시트 상수 행 수
ROW = '&#54665;'     # "행"
p = path('info'); s = files[p].decode()
key = '<t>' + ''.join(f'&#{ord(ch)};' for ch in '상수') + '</t>'     # B열이 '상수'인 시트 설명 행 (11-05 전에 68행으로 남아 있었다)
info_row = [r for r in re.findall(r'<row r="\d+"[^>]*>.*?</row>', s, re.S) if key in r and re.search(r'>\d+%s<' % ROW, r)]
assert len(info_row) == 1, ('info 상수 행', len(info_row))
fixed = re.sub(r'>\d+%s<' % ROW, f'>{lastrow - 2}{ROW}<', info_row[0])
assert fixed != info_row[0]
files[p] = s.replace(info_row[0], fixed).encode()

with zipfile.ZipFile(sys.argv[1], 'w', zipfile.ZIP_DEFLATED) as out:
    for n in order: out.writestr(n, files[n])
print('stages', done, 'const', CONST[0], CONST[1], CONST[2], 'const rows', lastrow - 3, '->', lastrow - 2)
