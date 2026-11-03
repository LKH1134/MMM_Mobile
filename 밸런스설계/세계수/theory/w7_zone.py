# D2 (라운드 4): 단계·자리(zone)별로 "가졌는데 칸이 없어 못 놓은 장식"을 접속마다 센다 + 5단계 칸 1개 늘리기 안 비교.
#   python w7_zone.py > w7_zone.log
# 측정 시점: 접속 끝(공방 받기·꾸미기 뒤) — wt_model.summary의 "하루 끝"보다 촘촘하다 (검증 w7_r4와 같은 쪽).
import json, os, sys, copy
sys.dont_write_bytecode = True
import wt_model as M, wt_tables as WT

FEEDS = [('보통', 'feed_v3_90.json'), ('무광고', 'feed_v3_a0_90.json'), ('광고 많이', 'feed_v3_a1_90.json'),
         ('플레이 ×0.5', 'feed_v3_t0.5_90.json'), ('플레이 ×2', 'feed_v3_t2.json'), ('보통 · 던전 1만', 'feed_v3_d1.json'),
         ('무광고 · 던전 1만', 'feed_v3_a0_d1.json')]
BASE5 = [5, 4, 5, 1]
VARIANTS = [('지금 5/4/5/1', None), ('뿌리 +1 (6/4/5/1)', 0), ('줄기 +1 (5/5/5/1)', 1), ('가지 +1 (5/4/6/1)', 2), ('왕관 +1 (5/4/5/2)', 3)]


class ZTree(M.Tree):
    def session(self, s, *a):
        super().session(s, *a)
        slots = dict(zip(WT.ZONES, self.row()['slots']))
        z = {k: 0 for k in WT.ZONES}
        for key in self.decos_owned:
            if key not in self.placed:
                z[self.deco(key)['zone']] += 1
        tot = sum(z.values())
        rec = self.zlog.setdefault(self.stage, dict(max=0, at=None, zone_max={k: 0 for k in WT.ZONES}, sess=0, sess_over3=0))
        rec['sess'] += 1
        if tot > 3:
            rec['sess_over3'] += 1
        if tot > rec['max']:
            rec['max'], rec['at'] = tot, (self.day, dict(z), sorted(k for k in self.decos_owned if k not in self.placed))
        for k in WT.ZONES:
            rec['zone_max'][k] = max(rec['zone_max'][k], z[k])


def run(feed, zone=None):
    T = WT.load_current()
    if zone is not None:
        T['tree'][4]['slots'] = list(BASE5)
        T['tree'][4]['slots'][zone] += 1
    tr = ZTree.__new__(ZTree)
    tr.zlog = {}
    M.Tree.__init__(tr, T, json.load(open(os.path.join(M.HERE, feed), encoding='utf-8'))['feed'])
    tr.run()
    return tr


def bonus_on(tr, day):
    r = next((x for x in tr.log['daily'] if x['day'] == day), None)
    return r['bonus'] if r else None


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    for label, feed in FEEDS:
        print(f'## {label} ({feed})')
        base = None
        for vname, z in VARIANTS:
            tr = run(feed, z)
            L = tr.zlog
            sd = tr.log['stage_day']
            mx = {s: L[s]['max'] for s in sorted(L)}
            s5 = L.get(5)
            print(f'- {vname}: 단계일 ' + ' / '.join(f'{sd[s]:g}' for s in range(2, 9) if s in sd) +
                  ' | 못 놓은 최대(단계:개) ' + ', '.join(f'{s}:{v}' for s, v in mx.items()))
            if s5:
                d, zz, keys = s5['at'] or (None, {}, [])
                print(f'    5단계: 최대 {s5["max"]} (일 {d}, 자리 {zz}, 장식 {keys}), 자리별 최대 {s5["zone_max"]}, '
                      f'4개 이상 접속 {s5["sess_over3"]}/{s5["sess"]}')
            if z is None:
                base = tr
            else:
                # 5단계 동안의 효과 차이 (하루 끝 bonus, 5단계였던 날 평균)
                days5 = [r['day'] for r in tr.log['daily'] if r['stage'] == 5]
                b0 = {r['day']: r['bonus'] for r in base.log['daily']}
                diff = {}
                for d in days5:
                    for k, v in tr.log['daily'][d - 1]['bonus'].items():
                        dv = v - b0[d][k]
                        if abs(dv) > 1e-9:
                            diff[k] = max(diff.get(k, 0), dv) if dv > 0 else min(diff.get(k, 0), dv)
                print(f'    5단계 동안 효과 차이(최대): {({k: round(v, 3) for k, v in diff.items()})}')
        print()
