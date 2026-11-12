# 사용자 결정 D11 (11-03): 광고 뽑기를 종류마다 하루 1회 · 11회 소환으로 되돌린다 (v3의 4회 취소).
# 뽑기상품 시트에서 requiresAd 행(key가 _ad로 끝남)의 pullCount와 이름의 "4회"를 바꾼다. 사용: python restore_ad_gacha11.py <출력.xlsx>
import re, sys, zipfile

SRC = r'C:\Project\MMM_Mobile\Assets\Data\Tables\MainTable.xlsx'
OLD, NEW = 4, 11
ROUNDS = '&#54924;'      # "회" (이 시트는 글자를 숫자 참조로 저장했다)

z = zipfile.ZipFile(SRC)
order = z.namelist()
files = {n: z.read(n) for n in order}
wb = files['xl/workbook.xml'].decode('utf-8')
rels = files['xl/_rels/workbook.xml.rels'].decode('utf-8')
rid = re.search(r'r:id="(rId\d+)"', re.search(r'<sheet [^>]*name="뽑기상품"[^>]*/>', wb).group(0)).group(1)
tgt = re.search(r'Target="([^"]+)"', re.search(r'<Relationship [^>]*Id="%s"[^>]*/>' % rid, rels).group(0)).group(1)
path = 'xl/' + tgt.lstrip('/').replace('xl/', '', 1)
s = files[path].decode('utf-8')

changed = 0


def fix(m):
    global changed
    row = m.group(0)
    if not re.search(r'<c r="B\d+"[^>]*><is><t>[a-z]+_ad</t></is></c>', row): return row
    r = re.match(r'<row r="(\d+)"', row).group(1)
    pull = f'<c r="E{r}" s="41" t="n"><v>{OLD}</v></c>'
    name = f' {OLD}{ROUNDS} ('
    assert row.count(pull) == 1 and row.count(name) == 1, r
    changed += 1
    return row.replace(pull, f'<c r="E{r}" s="41" t="n"><v>{NEW}</v></c>').replace(name, f' {NEW}{ROUNDS} (')


s = re.sub(r'<row r="\d+"[^>]*>.*?</row>', fix, s, flags=re.S)
assert changed == 4, changed
files[path] = s.encode('utf-8')
with zipfile.ZipFile(sys.argv[1], 'w', zipfile.ZIP_DEFLATED) as out:
    for n in order: out.writestr(n, files[n])
print('ad rows', changed, OLD, '->', NEW)
