# D2 보조: 5단계 접속마다 자리별 못 놓은 수·놓인 장식, 자리 +1 안의 효과 합 차이(접속 단위). python w7_zone_time.py > w7_zone_time.log
import sys, json, os
sys.dont_write_bytecode = True
import wt_model as M, wt_tables as WT, w7_zone as Z
sys.stdout.reconfigure(encoding='utf-8')


class TT(M.Tree):
    def session(self, s, *a):
        super().session(s, *a)
        if self.stage == 5:
            z = {k: 0 for k in WT.ZONES}
            for key in self.decos_owned:
                if key not in self.placed:
                    z[self.deco(key)['zone']] += 1
            self.rec.append((self.day, self.sess_i, dict(z), {k: round(v, 3) for k, v in self.bonus().items() if v},
                             sorted(self.placed, key=lambda k: WT.ZONES.index(self.deco(k)['zone']))))


def run(feed, zone=None):
    T = WT.load_current()
    if zone is not None:
        T['tree'][4]['slots'][zone] += 1
    tr = TT.__new__(TT); tr.rec = []
    M.Tree.__init__(tr, T, json.load(open(os.path.join(M.HERE, feed), encoding='utf-8'))['feed'])
    tr.run()
    return tr


for label, feed in Z.FEEDS[:3] + Z.FEEDS[5:]:
    base = run(feed)
    print(f'## {label}')
    zs = {k: 0 for k in WT.ZONES}
    for d, i, z, b, p in base.rec:
        for k in WT.ZONES:
            zs[k] += z[k] > 0
    print(f'5단계 접속 {len(base.rec)}회 중 자리별 "못 놓은 것 있음" 접속 수: {zs}')
    for d, i, z, b, p in base.rec:
        print(f'  d{d}.{i} 못놓음 {z} 합 {sum(z.values())}')
    for zi, zn in enumerate(WT.ZONES):
        tr = run(feed, zi)
        dif = {}
        for (d, i, z, b, p), (_, _, z0, b0, _) in zip(tr.rec, base.rec):
            for k in set(b) | set(b0):
                dv = b.get(k, 0) - b0.get(k, 0)
                if abs(dv) > 1e-9:
                    dif[k] = max(dif.get(k, 0), dv)
        print(f'  {zn} +1: 5단계 접속 {len(tr.rec)}회, 못 놓은 최대 {max(sum(r[2].values()) for r in tr.rec)}, 효과 합 차이(접속 단위 최대) {dif}')
    print()
