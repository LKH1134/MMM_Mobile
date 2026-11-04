# 펫 삭제 보정 실측 비교 (theory). test 분석(analyze_pet.py)과 같은 정의:
#   D n = 벽시계 n일 경과 시점 스테이지, 첫 1시간 = 플레이 0~3600초, 허들 = 한 스테이지 클리어에 든 플레이 시간이 처음 문턱(5·30·60분)을 넘는 스테이지
# 사용: python analyze.py 이름1 이름2 ...   (이름 'base' = test 기준선 7b17e68 결과 test/out/stage_pet_base_*, 'tcur' = test 삭제 후 결과)
# 나머지 이름은 scratchpad/theory_pet/<이름>/stage_<시나리오>_s<시드>.csv (run.sh 결과)
import csv, os, sys, statistics as st, bisect, glob, json

SP = 'C:/Users/user/AppData/Local/Temp/claude/82d5963c662fda11/2c54f97f-d275-490e-a84e-6bcf329252d2/scratchpad'
TEST = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..', 'test', 'out'))
SC = [('B', '무광고'), ('Bhalf', '보통'), ('Bads', '많이')]


def files(name, nm):
    if name == 'base':
        return sorted(glob.glob(f'{TEST}/stage_pet_base_{nm}_s*.csv'))
    if name == 'tcur':
        return sorted(glob.glob(f'{TEST}/stage_pet_cur_{nm}_s*.csv'))
    return sorted(glob.glob(f'{SP}/theory_pet/{name}/stage_{nm}_s*.csv'))


def load(f):
    return [(int(r['stage']), float(r['wallSec']), float(r['playSec'])) for r in csv.DictReader(open(f))]


def stage_at(rows, key, t):
    ts = [r[key] for r in rows]
    i = bisect.bisect_right(ts, t) - 1
    return rows[i][0] if i >= 0 else 1


def waits(rows):
    return [(rows[i][0], rows[i + 1][2] - rows[i][2], rows[i][1]) for i in range(len(rows) - 1)]


def first_hour(rows):
    res = []
    for i, r in enumerate(rows):
        if r[2] >= 3600:
            break
        nxt = rows[i + 1][2] if i + 1 < len(rows) else 1e18
        w = min(nxt, 3600) - r[2]
        if w >= 300:
            res.append((r[0], w))
    return res


def med(v):
    v = [x for x in v if x is not None]
    return st.median(v) if v else None


def summarize(name):
    out = {}
    for nm, nl in SC:
        R = [load(f) for f in files(name, nm)]
        if not R:
            continue
        d = {'n': len(R), '1h': med([stage_at(r, 2, 3600) for r in R])}
        for day in (1, 3, 7, 14, 30, 60):
            d[f'D{day}'] = med([stage_at(r, 1, day * 86400) if r[-1][1] >= day * 86400 or True else None for r in R])
        fh = [first_hour(r) for r in R]
        d['fh_n'] = med([len(x) for x in fh]); d['fh_min'] = round(med([sum(w for _, w in x) / 60 for x in fh]), 1)
        d['fh_stages'] = sorted({s for x in fh for s, _ in x})
        for th in (5, 30, 60):
            ss = []
            for r in R:
                f = next((s for s, w, _ in waits(r) if w >= th * 60), None)
                ss.append(f)
            d[f'h{th}'] = med(ss)
        d['end'] = med([r[-1][0] for r in R]); d['end_day'] = round(med([r[-1][1] / 86400 for r in R]), 1)
        out[nl] = d
    return out


if __name__ == '__main__':
    names = sys.argv[1:] or ['base', 'tcur']
    res = {n: summarize(n) for n in names}
    base = res.get('base')
    keys = ['1h', 'fh_n', 'fh_min', 'D1', 'D3', 'D7', 'D14', 'D30', 'D60', 'h5', 'h30', 'h60', 'end']
    for nl in ('무광고', '보통', '많이'):
        print(f'== {nl}  (괄호 = 기준선 대비 %)')
        print('이름'.ljust(14) + ' '.join(k.rjust(13) for k in keys))
        for n in names:
            d = res[n].get(nl)
            if not d:
                continue
            cells = []
            for k in keys:
                v = d[k]
                b = base.get(nl, {}).get(k) if base else None
                pct = '' if (b in (None, 0) or v is None or k.startswith('fh')) else f'({(v / b - 1) * 100:+.0f})'
                cells.append((f'{v:g}' if v is not None else '-') + pct)
            print(n.ljust(14) + ' '.join(c.rjust(13) for c in cells) + f"  5분↑ s{d['fh_stages']}")
    json.dump(res, open(os.path.join(os.path.dirname(__file__), 'last_analyze.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
