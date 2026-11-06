# verify 독립 표 사본 생성 (manager#20, 11-04). theory/pet_table.py를 보지 않고 이론.md 13.5 문구만으로 작성.
#   python pet_table_v.py <원본.xlsx> <출력.xlsx> <weaponEffectMul|-> <k점 json|-> [--check]
#   - 상수 시트 끝에 9081 weaponEffectMul 행을 더한다 (값 '-'이면 안 더함)
#   - 스테이지 시트 enemyHp·bossHp 열 × k(s): k 점 사이는 ln k 를 s에 대해 선형 보간, 첫 점 앞 1, 끝 점 뒤 끝 값
#   - 출력은 "stage,k,enemyHp_old,enemyHp_new,bossHp_old,bossHp_new" csv(같은 이름 .csv)도 남긴다
#   --boss=170:1.3,180:1.3 : 그 스테이지 bossHp만 추가로 × (쟁점 (a) s170~180 측정용, 11-05)
#   --check: 1~700 enemyHp·bossHp 단조 증가(감소 0곳) 확인
import sys, re, json, math, zipfile

NS_SHEET = r'<sheet [^>]*?name="([^"]*)"[^>]*?r:id="([^"]*)"'


def sheet_paths(z):
    wb = z.read('xl/workbook.xml').decode('utf-8')
    rels = z.read('xl/_rels/workbook.xml.rels').decode('utf-8')
    rmap = {m.group(2): m.group(1) for m in re.finditer(r'Target="([^"]*)" Id="([^"]*)"', rels)}
    rmap.update({m.group(1): m.group(2) for m in re.finditer(r'Id="([^"]*)"[^>]*Target="([^"]*)"', rels)})
    out = {}
    for m in re.finditer(NS_SHEET, wb):
        t = rmap[m.group(2)].lstrip('/')
        out[m.group(1)] = t if t.startswith('xl/') else 'xl/' + t
    return out


def cell_text(c):
    m = re.search(r'<t[^>]*>(.*?)</t>', c, re.S) or re.search(r'<v>(.*?)</v>', c)
    return m.group(1) if m else ''


def header_cols(xml):
    head = re.search(r'<row [^>]*?r="1"[^>]*>(.*?)</row>', xml, re.S).group(1)
    return {cell_text(m.group(2)): m.group(1) for m in re.finditer(r'<c r="([A-Z]+)1"[^>]*>(.*?)</c>', head, re.S)}


def kfun(pts):
    def f(s):
        if s <= pts[0][0]:
            return 1.0 if pts[0][1] == 1.0 or s < pts[0][0] else pts[0][1]
        for (a, ka), (b, kb) in zip(pts, pts[1:]):
            if s <= b:
                return math.exp(math.log(ka) + (math.log(kb) - math.log(ka)) * (s - a) / (b - a))
        return pts[-1][1]
    return f


BOSS = {}


def main():
    for a in sys.argv:
        if a.startswith('--boss='):
            BOSS.update({int(x.split(':')[0]): float(x.split(':')[1]) for x in a[7:].split(',')})
    src, dst, wm, pts = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
    z = zipfile.ZipFile(src)
    paths = sheet_paths(z)
    repl = {}
    # 상수
    if wm != '-':
        p = paths['상수']
        x = z.read(p).decode('utf-8')
        last = max(int(m.group(1)) for m in re.finditer(r'<row [^>]*?r="(\d+)"', x))
        r = last + 1
        row = (f'<row r="{r}"><c r="A{r}" t="n"><v>9081</v></c><c r="B{r}" t="inlineStr"><is><t>weaponEffectMul</t></is></c>'
               f'<c r="C{r}" t="n"><v>{float(wm)!r}</v></c><c r="D{r}" t="inlineStr"><is><t>verify copy</t></is></c></row>')
        x = x.replace('</sheetData>', row + '</sheetData>', 1)
        x = re.sub(r'<dimension ref="A1:([A-Z]+)\d+"/>', lambda m: f'<dimension ref="A1:{m.group(1)}{r}"/>', x)
        repl[p] = x
    # 스테이지
    p = paths['스테이지']
    x = z.read(p).decode('utf-8')
    cols = header_cols(x)
    cs, ce, cb = cols['stage'], cols['enemyHp'], cols['bossHp']
    f = kfun(json.loads(pts)) if pts != '-' else (lambda s: 1.0)
    rows_out = []
    series = {}

    def num(body, col, rn):
        m = re.search(r'<c r="%s%d"[^>]*><v>([^<]*)</v></c>' % (col, rn), body)
        return float(m.group(1)) if m else None

    def row_fn(m):
        body, rn = m.group(0), int(m.group(1))
        if rn < 4:
            return body
        s = num(body, cs, rn)
        if s is None:
            return body
        s = int(s)
        k = f(s)
        e0, b0 = num(body, ce, rn), num(body, cb, rn)
        e1, b1 = e0, b0
        bm = BOSS.get(s, 1.0)
        if abs(k - 1) > 1e-12 or bm != 1.0:
            e1 = float('%.6g' % (e0 * k)); b1 = float('%.6g' % (b0 * k * bm))
            for col, v in ((ce, e1), (cb, b1)):
                body = re.sub(r'(<c r="%s%d"[^>]*><v>)[^<]*(</v></c>)' % (col, rn), lambda mm: mm.group(1) + repr(v) + mm.group(2), body, count=1)
        series[s] = (e1, b1)
        rows_out.append((s, k, e0, e1, b0, b1))
        return body

    x = re.sub(r'<row [^>]*?r="(\d+)"[^>]*>.*?</row>', row_fn, x, flags=re.S)
    repl[p] = x
    with zipfile.ZipFile(dst, 'w', zipfile.ZIP_DEFLATED) as out:
        for it in z.infolist():
            out.writestr(it, repl[it.filename].encode('utf-8') if it.filename in repl else z.read(it.filename))
    with open(dst.rsplit('.', 1)[0] + '.csv', 'w', encoding='utf-8') as fo:
        fo.write('stage,k,enemyHp_old,enemyHp_new,bossHp_old,bossHp_new\n')
        for r in rows_out:
            fo.write(','.join('%.6g' % v if isinstance(v, float) else str(v) for v in r) + '\n')
    nchg = sum(1 for r in rows_out if abs(r[1] - 1) > 1e-12)
    print('rows', len(rows_out), 'changed', nchg, 'const', wm)
    if '--check' in sys.argv:
        ss = sorted(series)
        dec_e = [s for a, s in zip(ss, ss[1:]) if series[s][0] <= series[a][0]]
        dec_b = [s for a, s in zip(ss, ss[1:]) if series[s][1] <= series[a][1]]
        print('enemyHp 비증가 스테이지', dec_e[:20], len(dec_e))
        print('bossHp  비증가 스테이지', dec_b[:20], len(dec_b))
        for s in (419, 420, 421, 500, 600, 650, 699, 700):
            if s in series:
                print(s, 'k=%.4f' % f(s), 'enemyHp %.6g bossHp %.6g' % series[s])
        rr = [series[s][1] / series[a][1] for a, s in zip(ss, ss[1:]) if a >= 400]
        print('bossHp 이웃 비(s400~700) min %.4f max %.4f' % (min(rr), max(rr)))


main()
