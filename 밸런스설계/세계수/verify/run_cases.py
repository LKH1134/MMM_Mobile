# 모든 경우 (wt-verify): 기준선·광고·플레이 시간·예산·꼬마 뽑기 몫·K1·D4·가속. python run_cases.py > cases.log
import sys, json, os
sys.stdout.reconfigure(encoding='utf-8')
import tables, wt_port as W
HERE = os.path.dirname(os.path.abspath(__file__))
T = tables.load()


def feed(name): return json.load(open(os.path.join(HERE, name), encoding='utf-8'))


def r1(x): return None if x is None else round(x, 1)


def line(label, w):
    L = w.log
    reach = ' '.join(f"{s}:{r1(d)}" for s, d in sorted(L['reach'].items()) if s > 1)
    st3 = {k: r1(v[2]) if len(v) > 2 else None for k, v in L['star'].items()}
    for k in w.kids:
        st3.setdefault(k, None)
    aff = [r1(v) for v in L['affmax'].values()]
    P = W.period_table(w)
    gs = '/'.join(f"{p['g_sp']:.1f}" for p in P)
    gi = '/'.join(f"{p['g_bag'] + p['g_req']:.1f}" for p in P)
    mn = '/'.join(f"{p['m_net']:.1f}" for p in P)
    print(f"[{label}] 단계 {reach}")
    print(f"    ★3 {st3}  호감 최대 {min(aff) if aff else None}~{max(aff) if aff else None}일({len(aff)}/9)  장식24 {r1(L['all24'])}일 다놓음 {r1(L['placed24'])}일  부탁 {L['req_done']}/{L['req_total']} 주간 {L['week_boxes']}")
    print(f"    골드 쓰기% {gs} | 수입% {gi} | 순균사석% {mn}  (구간 1-7/8-14/15-30/31-45/46-60)")
    return w


CASES = [
    ('라이브 보통', 'feed_live_a0.5.json', {}),
    ('라이브 무광고', 'feed_live_a0.0.json', {}),
    ('라이브 광고 많이', 'feed_live_a1.0.json', {}),
    ('v2 보통', 'feed_v2_a0.5.json', {}),
    ('v2 무광고', 'feed_v2_a0.0.json', {}),
    ('v2 광고 많이', 'feed_v2_a1.0.json', {}),
    ('v2 플레이 ×0.5 (45분/일)', 'feed_v2_a0.5_t0.5.json', {}),
    ('v2 플레이 ×2 (3시간/일)', 'feed_v2_a0.5_t2.0.json', {}),
    ('라이브 플레이 ×0.5', 'feed_live_a0.5_t0.5.json', {}),
    ('라이브 플레이 ×2', 'feed_live_a0.5_t2.0.json', {}),
    ('v2 골드 예산 5%', 'feed_v2_a0.5.json', dict(gold_share=0.05)),
    ('v2 골드 예산 20%', 'feed_v2_a0.5.json', dict(gold_share=0.20)),
    ('v2 균사석 예산 0% (세계수 수입만)', 'feed_v2_a0.5.json', dict(myc_share=0.0)),
    ('v2 무광고 균사석 예산 0%', 'feed_v2_a0.0.json', dict(myc_share=0.0)),
    ('v2 꼬마 뽑기 10%', 'feed_v2_a0.5.json', dict(kid_share=0.10)),
    ('v2 꼬마 뽑기 0% (기본 5마리만)', 'feed_v2_a0.5.json', dict(kid_share=0.0)),
    ('v2 K1 없음', 'feed_v2_a0.5.json', dict(k1=False)),
    ('v2 선물 0', 'feed_v2_a0.5.json', dict(gifts=0)),
    ('v2 부탁 안 함', 'feed_v2_a0.5.json', dict(requests=False)),
    ('v2 가속 씀', 'feed_v2_a0.5.json', dict(rush=True)),
    ('v2 D4 (등급 해금 뽑기 Lv 1/1/2/4/5 가정)', 'feed_v2_a0.5.json', dict(grade_unlock=[1, 1, 2, 4, 5])),
]
if __name__ == '__main__':
    res = {}
    for label, f, o in CASES:
        res[label] = line(label, W.WT(T, feed(f), **o).run())
    # 효과 타임라인 (되먹임 feed용)
    for label, f in (('v2', 'feed_v2_a0.5.json'), ('live', 'feed_live_a0.5.json'), ('v2a0', 'feed_v2_a0.0.json')):
        w = W.WT(T, feed(f)).run()
        tl = [dict(day=d['day'], **{k: d['bonus'][k] for k in ('Attack', 'Hp', 'CritChance', 'CritDamage', 'KillGold', 'BossMycelite')})
              for d in w.log['day']]
        json.dump(tl, open(os.path.join(HERE, f'timeline_{label}.json'), 'w', encoding='utf-8'))
    # 효과 최대 합
    print('효과 최대 합 (가호+꼬마 ★3·세트+장식 24):', {k: round(v, 3) for k, v in W.max_totals(T).items() if v})
    # 단계별 효과 (v2 보통, 진화 순간)
    w = W.WT(T, feed('feed_v2_a0.5.json')).run()
    for s, b in sorted(w.log['eff_at'].items()):
        print(f"  v2 {s}단계 진화 순간:", {k: round(v, 3) for k, v in b.items() if v})
    print('  v2 보유/칸 (진화 순간):', w.log['decos_at'])
    # 하루 보따리 (v2·라이브 보통): 보따리 골드 / 하루 골드, 보따리 꽉 찬 시간
    for label, f in (('v2', 'feed_v2_a0.5.json'), ('live', 'feed_live_a0.5.json')):
        w = W.WT(T, feed(f)).run()
        for p in W.period_table(w):
            print(f"  {label} {p['r']}: 보따리 골드 {p['g_bag']:.1f}% 부탁 {p['g_req']:.1f}% · 보따리 균사석/일 {p['m_bag']:.1f} · 세계수 균사석 쓰기/수입/방치 {p['m_sp']:.0f}/{p['m_inc']:.0f}/{p['m_idle']:.0f} · 창고/일 {p['harvest']:.0f} 공물 경험치/일 {p['exp']:.0f} · 선물 몫 {p['gift_share']:.1f}% · 보따리 넘친 시간/일 {p['bag_full']:.1f}h")
