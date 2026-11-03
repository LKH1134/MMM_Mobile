# 라운드 4 (KHD 방치 v3 머지 뒤): 세계수 진행 = wt_port(코드 포팅, 라운드 1~3과 같음) × 방치 입력 feed_v3_*.json (wt-verify)
# python r4/run_cases_r4.py > r4/cases_r4.log   (verify 폴더에서)
import sys, json, os
sys.stdout.reconfigure(encoding='utf-8')
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import tables, wt_port as W
from run_cases import line
T = tables.load()


def feed(name): return json.load(open(os.path.join(HERE, name), encoding='utf-8'))


CASES = [
    ('v3 보통 (광고 0.5)', 'feed_v3_a0.5.json', {}),
    ('v3 무광고', 'feed_v3_a0.0.json', {}),
    ('v3 광고 많이', 'feed_v3_a1.0.json', {}),
    ('v3 플레이 ×0.5', 'feed_v3_a0.5_t0.5.json', {}),
    ('v3 플레이 ×2', 'feed_v3_a0.5_t2.0.json', {}),
    ('v3 보통 · 던전 1만 (지금 게임)', 'feed_v3_a0.5_d1.json', {}),
    ('v3 무광고 · 던전 1만', 'feed_v3_a0.0_d1.json', {}),
    ('v3 골드 예산 5%', 'feed_v3_a0.5.json', dict(gold_share=0.05)),
    ('v3 균사석 예산 0%', 'feed_v3_a0.5.json', dict(myc_share=0.0)),
    ('v3 무광고 균사석 예산 0%', 'feed_v3_a0.0.json', dict(myc_share=0.0)),
    ('v3 꼬마 뽑기 10%', 'feed_v3_a0.5.json', dict(kid_share=0.10)),
    ('v3 K1 없음', 'feed_v3_a0.5.json', dict(k1=False)),
    ('v3 가속 씀', 'feed_v3_a0.5.json', dict(rush=True)),
]


def detail(label, f):
    w = W.WT(T, feed(f)).run()
    for p in W.period_table(w):
        print(f"  {label} {p['r']}: 골드 쓰기 {p['g_sp']:.1f}% (나이테 {p['g_ring']:.1f} 진화 {p['g_evo']:.1f} 공방 {p['g_craft']:.1f}) · 보따리 골드 {p['g_bag']:.1f}% 부탁 {p['g_req']:.1f}%"
              f" · 균사석 쓰기/세계수 수입/방치 {p['m_sp']:.0f}/{p['m_inc']:.0f}/{p['m_idle']:.0f} (순 {p['m_net']:.1f}%) · 보따리 균사석/일 {p['m_bag']:.1f}"
              f" · 창고/일 {p['harvest']:.0f} 공물 경험치/일 {p['exp']:.0f} · 선물 몫 {p['gift_share']:.1f}% · 보따리 넘친 시간/일 {p['bag_full']:.1f}h")
    return w


if __name__ == '__main__':
    for label, f, o in CASES:
        line(label, W.WT(T, feed(f), **o).run())
    print()
    for label, f in (('보통', 'feed_v3_a0.5.json'), ('무광고', 'feed_v3_a0.0.json'), ('많이', 'feed_v3_a1.0.json'), ('보통 d1', 'feed_v3_a0.5_d1.json'), ('무광고 d1', 'feed_v3_a0.0_d1.json')):
        w = detail(label, f)
        if label == '보통':
            for s, b in sorted(w.log['eff_at'].items()):
                print(f"    {s}단계 진화 순간 효과:", {k: round(v, 3) for k, v in b.items() if v})
            print('    보유/칸 (진화 순간):', w.log['decos_at'])
    # 되먹임용 타임라인
    for label, f in (('v3', 'feed_v3_a0.5.json'), ('v3a0', 'feed_v3_a0.0.json'), ('v3a1', 'feed_v3_a1.0.json')):
        w = W.WT(T, feed(f)).run()
        tl = [dict(day=d['day'], **{k: d['bonus'][k] for k in ('Attack', 'Hp', 'CritChance', 'CritDamage', 'KillGold', 'BossMycelite')})
              for d in w.log['day']]
        json.dump(tl, open(os.path.join(HERE, f'timeline_{label}.json'), 'w', encoding='utf-8'))
    print('효과 최대 합:', {k: round(v, 3) for k, v in W.max_totals(T).items() if v})
