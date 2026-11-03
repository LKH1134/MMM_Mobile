# v6 경우들: python v6_run.py > v6_run.log
import json, os, sys, copy
sys.dont_write_bytecode = True
sys.stdout.reconfigure(encoding='utf-8')
import wt_model as M, wt_tables as WT, v6_model as V
from quest_model import QuestBook
import xlsx_dump

FEEDS = {'보통': 'feed_v3_90.json', '무광고': 'feed_v3_a0_90.json', '광고 많이': 'feed_v3_a1_90.json',
         '×0.5': 'feed_v3_t0.5_90.json', '×2': 'feed_v3_t2.json'}
ROOT = os.path.abspath(os.path.join(M.HERE, '..', '..', '..'))


def live_quests():
    d = xlsx_dump.load(os.path.join(ROOT, 'Assets/Data/Tables/GameBalance.xlsx'))['의뢰']
    rows = [dict(zip(d[0], r)) for r in d[3:] if r and r[0] not in ('', None)]
    D = [(r['kind'], int(r['target']), int(r['exp'])) for r in rows if r['period'] == 'daily']
    Wk = [(r['kind'], int(r['target']), int(r['exp'])) for r in rows if r['period'] == 'weekly']
    return lambda: QuestBook(D, Wk, stage_kind='boss')   # K3 적용됨 (IdleBattle.cs:347 보스 등장)


class Probe(V.V6Tree):
    """W7' 측정: 접속 끝 빈 칸 수·고를 거리(가졌는데 안 놓은 수)·세트 수"""
    def session(self, s, *a):
        super().session(s, *a)
        tot = sum(self.row()['slots'])
        empty = tot - len(self.placed)
        spare = len(self.decos_owned) - len(self.placed)
        sets = sum(1 for k, v in self.kids.items() if v['owned'] and self.set_complete(k))
        self.log.setdefault('w7', []).append((round(self.day - 1 + self.sess_i / 3, 2), self.stage, tot, empty, spare, sets))


def run(feed, policy, quests=True, set_rule=None, slots=None, T=None, **kw):
    T = T or WT.load_current()
    if slots:
        for r, sl in zip(T['tree'], slots):
            r['slots'] = list(sl)
    q = live_quests()() if quests else None
    tr = Probe(T, json.load(open(os.path.join(M.HERE, feed), encoding='utf-8'))['feed'], policy=policy, set_rule=set_rule, quests=q, **kw)
    return tr.run()


def w7(tr):
    """단계마다: 칸이 열린 뒤 빈 칸이 0이 되기까지(일), 그 단계 동안 빈 칸 최대, 고를 거리 최소~최대, 세트 최대"""
    out = {}
    L = tr.log['w7']
    sd = tr.log['stage_day']
    for st in sorted(set(x[1] for x in L)):
        R = [x for x in L if x[1] == st]
        t0 = sd.get(st, 0)
        fill = next((x[0] for x in R if x[3] == 0), None)
        out[st] = dict(fill=None if fill is None else round(fill - t0, 2), empty_max=max(x[3] for x in R),
                       empty_end=R[-1][3], spare=(min(x[4] for x in R), max(x[4] for x in R)), sets=max(x[5] for x in R), dur=round(R[-1][0] - t0, 2))
    return out


def line(label, tr):
    sd = tr.log['stage_day']
    D = tr.log['daily']
    r8 = next((r for r in D if r['stage'] >= 8), D[-1])
    b = r8['bonus']
    deco_tot = lambda e: sum(tr.deco(k)['value'] for k in tr.placed if tr.deco(k)['effect'] == e)
    w = w7(tr)
    fm = sum(r['m_first'] for r in D)
    print(f'- {label}: 2~8단계 ' + ' / '.join(f'{sd[s]:g}' for s in range(2, 9) if s in sd) +
          f' | 8단계 날 효과: 버섯 {b["Harvest"]:.2f} 보따리골드 {b["BagGold"]:.2f} 보따리균사석 {b["BagMycelite"]:.2f} 제작 {b["CraftSpeed"]:.2f} 보관 {b["BagHours"]:.1f}h '
          f'체력 {b["Hp"]:.2f} 치명피해 {b["CritDamage"]:.2f} 공격 {b["Attack"]:.2f} 보따리×{r8["bag_mult"]:.2f} | 첫배치 균사석 합 {fm:.0f} | 장식 {r8["decos"]}/{r8["placed"]}놓음')
    print('    W7\': ' + ' · '.join(f'{st}단계({v["dur"]}일) 채움 {v["fill"]}일·빈칸최대 {v["empty_max"]}·고를거리 {v["spare"][0]}~{v["spare"][1]}·세트 {v["sets"]}' for st, v in w.items()))
    return tr


if __name__ == '__main__':
    OLD = [s for s in WT.V4['tree']['slots']]
    print('## 기준선: v4 칸(1/3/6/10/16/23/24/24) + 라이브 의뢰 (지금 40.0일이라던 것)')
    for k in ('보통', '무광고'):
        line(f'{k} · v4 칸 · grade', run(FEEDS[k], 'grade', slots=OLD))
    print('\n## v6 칸 (기획서) + 라이브 의뢰')
    for k, f in FEEDS.items():
        for pol in ('grade', 'pace'):
            line(f'{k} · {pol}', run(f, pol))
    print('\n## v6 · 보통 · 정책·규칙 민감도')
    line('보통 · firstall (첫 배치 보상 다 받기)', run(FEEDS['보통'], 'firstall'))
    line('보통 · pace · 의뢰 없음', run(FEEDS['보통'], 'pace', quests=False))
    line('보통 · v4 칸 · 의뢰 없음 (v4 기준선)', run(FEEDS['보통'], 'grade', quests=False, slots=OLD))
    line('보통 · pace · 세트 = 2개 (K4안)', run(FEEDS['보통'], 'pace', set_rule=2))
    line('보통 · pace · 세트 가중치 0 (세트 무시)', run(FEEDS['보통'], 'pace', weights=dict()))
