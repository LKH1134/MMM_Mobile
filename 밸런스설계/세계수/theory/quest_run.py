# 의뢰 v5 경우들: python quest_run.py > quest_run.log
import json, os, sys
sys.dont_write_bytecode = True
sys.stdout.reconfigure(encoding='utf-8')
import wt_model as M, wt_tables as WT
from quest_model import QuestBook

FEEDS = {'보통': 'feed_v3_90.json', '무광고': 'feed_v3_a0_90.json', '광고 많이': 'feed_v3_a1_90.json',
         '×0.5': 'feed_v3_t0.5_90.json', '×2': 'feed_v3_t2.json'}


def run(feed, book, which='v4'):
    T = WT.apply(WT.load_current(), WT.V4) if which == 'v4' else WT.load_current()
    tr = M.Tree(T, json.load(open(os.path.join(M.HERE, feed), encoding='utf-8'))['feed'], quests=book).run()
    return tr


def report(label, feed, mk):
    tr = run(feed, mk() if mk else None)
    D = tr.log['daily']
    sd = tr.log['stage_day']
    parts = []
    for lo, hi in [(1, 7), (8, 14), (15, 30), (31, 45)]:
        R = [r for r in D if lo <= r['day'] <= hi]
        if R:
            q = sum(r['q_exp'] for r in R); t = sum(r['exp'] for r in R)
            parts.append(f'd{lo}-{hi} {q / len(R):.0f}/일 ({q / (q + t) * 100:.0f}%)' if q + t else '')
    un = tr.log.get('unplaced_sess', {}).get(5)
    print(f'- {label}: 2~8단계 ' + ' / '.join(f'{sd[s]:g}' for s in range(2, 9) if s in sd) +
          f' | 의뢰 경험치(몫 = 의뢰 / (의뢰 + 공물)) ' + ' · '.join(parts) + f' | 5단계 못 놓음 {un}')
    return tr


def daily_rate(feed, daily, stage_kind, combo_p):
    """일일 의뢰 행마다 '그날 나온 날 중 깬 날' 비율 (1~60일, 61~90일)"""
    D = json.load(open(os.path.join(M.HERE, feed), encoding='utf-8'))['feed']
    out = []
    for kind, target, _ in daily:
        if kind == 'combo':
            out.append(f'{kind} {target}: p={combo_p:g}'); continue
        ok = [0, 0]; n = [0, 0]
        for d in D:
            S = d['sessions']
            v = sum(s['kills'] for s in S) if kind == 'kill' else sum(s['kills'] for s in S) / 10 if kind == 'species' else \
                sum(s['boss_tries'] if stage_kind == 'boss' else s['stages'] for s in S)
            b = 0 if d['day'] <= 60 else 1
            n[b] += 1; ok[b] += v >= target
        out.append(f'{kind} {target}: {ok[0]}/{n[0]} · {ok[1]}/{n[1]}')
    return ', '.join(out)


if __name__ == '__main__':
    NOW_D = [('stage', 3, 1000), ('kill', 1500, 1000), ('species', 150, 1000), ('combo', 30, 1000)]
    NOW_W = [('daily', 7, 4000), ('stage', 20, 3000), ('kill', 10000, 3000)]
    print('## 기준선 (의뢰 없음, v4)')
    for k, f in FEEDS.items():
        report(k, f, None)
    print('\n## 지금 임시값 (stage = 돌파, combo 30)')
    for k, f in FEEDS.items():
        print('  일일 깬 날 (1~60 · 61~90):', k, daily_rate(f, NOW_D, 'stage', 0))
    for cp in (1.0, 0.0):
        for k in ('보통', '무광고', '×0.5'):
            report(f'{k} · combo p={cp:g}', FEEDS[k], lambda cp=cp: QuestBook(NOW_D, NOW_W, combo_p=cp))
    # 제안 목표 (stage → 보스 도전, K3)
    def P_D(x): return [('stage', 30, x), ('kill', 4000, x), ('species', 350, x), ('combo', 5, x)]
    def P_W(x): return [('daily', 14, 4 * x), ('stage', 350, 3 * x), ('kill', 40000, 3 * x)]
    print('\n## 제안 목표 (일일 보스 30 · 처치 4000 · 지정 350 · 콤보 5, 주간 일일 14 · 보스 350 · 처치 40000), exp 크기')
    for k, f in FEEDS.items():
        print('  일일 깬 날 (1~60 · 61~90):', k, daily_rate(f, P_D(0), 'boss', 0.8))
    for x in (500, 1000, 1500, 2000, 3000):
        for k in ('보통', '무광고', '광고 많이', '×0.5', '×2'):
            report(f'x={x} {k} · combo p=0.8', FEEDS[k], lambda x=x: QuestBook(P_D(x), P_W(x), combo_p=0.8, stage_kind='boss'))
        print()
    x = 1500
    print('## x=1500 민감도')
    for cp in (1.0, 0.0):
        report(f'보통 · combo p={cp:g}', FEEDS['보통'], lambda cp=cp: QuestBook(P_D(x), P_W(x), combo_p=cp, stage_kind='boss'))
    report('보통 · 첫 주 목요일 시작', FEEDS['보통'], lambda: QuestBook(P_D(x), P_W(x), combo_p=0.8, stage_kind='boss', start_dow=3))
    report('보통 · K3 없음 (stage = 돌파, 일일 3 · 주간 20)', FEEDS['보통'],
           lambda: QuestBook([('stage', 3, x)] + P_D(x)[1:], [P_W(x)[0], ('stage', 20, 3 * x), P_W(x)[2]], combo_p=0.8))
