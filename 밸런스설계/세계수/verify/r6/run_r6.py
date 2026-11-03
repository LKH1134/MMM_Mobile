# 라운드 6 (의뢰 v5.1): W1~W10 재판정 (wt-verify). python r6/run_r6.py > r6/cases_r6.log
import sys, os, json
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE); sys.path.insert(0, os.path.dirname(HERE))
import tables, wt_port as W
from wt_quest import WTQ
T = tables.load()
R = lambda x: None if x is None else round(x, 2)


def feed(f): return json.load(open(os.path.join(HERE, f), encoding='utf-8'))


def run(f, end=True, **o):
    w = WTQ(T, feed(f), **o)
    rec_end = {}
    ra0 = w.rush_all
    def ra():
        ra0(); n = len(w.owned) - len(w.placed); rec_end[w.stage] = max(rec_end.get(w.stage, 0), n)
    w.rush_all = ra
    w.run(); w.w7 = rec_end
    return w


def reach(w): return ' / '.join(f"{R(w.log['reach'].get(s))}" for s in range(2, 9))


def share(w, a, b):
    ds = [d for d in w.log['day'] if a <= d['day'] <= b and d['stage'] < 8]
    q = sum(d.get('exp_q', 0) for d in ds); t = sum(d.get('exp', 0) for d in ds)
    return None if q + t == 0 else q / (q + t) * 100


FEEDS = [('보통', 'feed_v3_a0.5.json'), ('무광고', 'feed_v3_a0.0.json'), ('광고 많이', 'feed_v3_a1.0.json'),
         ('플레이 ×0.5', 'feed_v3_a0.5_t0.5.json'), ('무광고 ×0.5', 'feed_v3_a0.0_t0.5.json'), ('플레이 ×2', 'feed_v3_a0.5_t2.0.json')]
if __name__ == '__main__':
    print('의뢰 시트:', [(r['key'], r['kind'], r['target'], r['exp']) for r in WTQ(T, feed('feed_v3_a0.5.json')).qrows])
    for label, f in FEEDS:
        w0 = run(f, quests=False)
        w = run(f)
        L = w.log
        st3 = {k: R(v[2]) if len(v) > 2 else None for k, v in L['star'].items()}
        print(f'\n[{label}] 의뢰 있음 2~8단계 {reach(w)} | 의뢰 없음 {reach(w0)}')
        st = [w.log['reach'].get(s) for s in range(2, 9)]
        if None not in st:
            g = [st[0]] + [b - a for a, b in zip(st, st[1:])]
            print('    간격', [R(x) for x in g], '비', [R(b / a) for a, b in zip(g, g[1:])])
        print(f"    ★3 {st3} 호감 최대 {R(min(L['affmax'].values()))}~{R(max(L['affmax'].values()))} 장식24 {R(L['all24'])} 다놓음 {R(L['placed24'])} · W7 접속 끝 {w.w7}")
        ql = w.qlog
        print('    일일 (나온 날 / 깬 날):', {k: f"{ql['daily_done'].get(k, 0)}/{ql['daily_shown'][k]}" for k in ql['daily_shown']})
        nweeks = len(w.feed['days']) // 7
        print(f'    주간 ({nweeks}주 중 깬 주 · 깬 날(주 시작부터 일)):', {k: (ql['weekly_done'].get(k, 0), (min(v), max(v)) if (v := ql['weekly_days'].get(k)) else None) for k in [r['key'] for r in w.weekly_rows]})
        print('    의뢰 몫 (의뢰/(의뢰+공물), 8단계 전): 1-7 %s · 8-14 %s · 15-30 %s · 31-45 %s · 46-60 %s' % tuple(
            (f'{x:.1f}%' if (x := share(w, a, b)) is not None else '-') for a, b in ((1, 7), (8, 14), (15, 30), (31, 45), (46, 60))))
        P = W.period_table(w)
        print('    순균사석%', '/'.join(f"{p['m_net']:.1f}" for p in P), '| 골드 쓰기%', '/'.join(f"{p['g_sp']:.1f}" for p in P),
              '| 보따리%', '/'.join(f"{p['g_bag']:.1f}" for p in P), '| 선물 몫', '/'.join(f"{p['gift_share']:.0f}" for p in P))
        if label in ('보통', '무광고', '광고 많이'):
            tl = [dict(day=d['day'], **{k: d['bonus'][k] for k in ('Attack', 'Hp', 'CritChance', 'CritDamage', 'KillGold', 'BossMycelite')}) for d in L['day']]
            json.dump(tl, open(os.path.join(HERE, f'timeline_q{ {"보통": "", "무광고": "a0", "광고 많이": "a1"}[label]}.json'), 'w', encoding='utf-8'))
            tl0 = [dict(day=d['day'], **{k: d['bonus'][k] for k in ('Attack', 'Hp', 'CritDamage')}) for d in w0.log['day']]
            first = lambda tlx, k, v: next((d['day'] for d in tlx if d[k] >= v - 1e-9), None)
            print('    전투 효과 처음 닿는 날 (의뢰 있음 / 없음):', {f'{k}≥{v}': (first(tl, k, v), first(tl0, k, v)) for k, v in (('Attack', 0.01), ('Attack', 0.02), ('Hp', 0.01), ('Hp', 0.02), ('CritDamage', 0.01), ('CritDamage', 0.03))})
    # 민감도: 의뢰 일부만
    for label, o in (('보통 · 일일만 (주간 안 함)', dict(skip={'w_daily', 'w_stage', 'w_kill'})), ('보통 · 주간만', dict(skip={'d_stage', 'd_kill', 'd_species', 'd_dungeon'})),
                     ('보통 · 던전 의뢰 안 함', dict(skip={'d_dungeon'}))):
        print(f'[{label}] 2~8단계 {reach(run("feed_v3_a0.5.json", **o))}')
