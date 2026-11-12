# 2026-11-12 모델 현행화 — 지금 표(MainTable.xlsx) 그대로, 수치 변경 없음.
#   python v6_run.py > v6_run_1112.log
# 입력 feed: 11-03 feed_v3_*_90.json (idle_feed.py가 지금 표로 돌지 않아 가장 최근 것을 씀 — 이론.md "2026-11-12 모델 현행화" 1절)
import json, os, sys
sys.dont_write_bytecode = True
sys.stdout.reconfigure(encoding='utf-8')
import wt_model as M, wt_tables as WT, v6_model as V

FEEDS = {'보통': 'feed_v3_90.json', '무광고': 'feed_v3_a0_90.json', '광고 많이': 'feed_v3_a1_90.json',
         '×0.5': 'feed_v3_t0.5_90.json', '×2': 'feed_v3_t2.json'}


class Probe(V.V6Tree):
    """W7′ 측정: 접속 끝 빈 칸 수·고를 거리(가졌는데 안 놓은 수)·세트 수"""
    def session(self, s, *a):
        super().session(s, *a)
        tot = sum(self.row()['slots'])
        empty = tot - len(self.placed)
        spare = len(self.decos_owned) - len(self.placed)
        sets = sum(1 for k, v in self.kids.items() if v['owned'] and self.set_complete(k))
        self.log.setdefault('w7', []).append((round(self.day - 1 + self.sess_i / 3, 2), self.stage, tot, empty, spare, sets))


def run(case, policy='pace', T=None, **kw):
    fd, meta = M.load_feed(FEEDS[case])
    T = T or WT.load_current()
    kw.setdefault('ads', meta.get('ads', 0.5))
    return Probe(T, fd, meta, policy=policy, **kw).run()


def w7(tr):
    out = {}
    L = tr.log['w7']
    sd = tr.log['stage_day']
    for st in sorted(set(x[1] for x in L)):
        R = [x for x in L if x[1] == st]
        t0 = sd.get(st, 0)
        fill = next((x[0] for x in R if x[3] == 0), None)
        out[st] = dict(fill=None if fill is None else round(fill - t0, 2), empty_max=max(x[3] for x in R),
                       spare=(min(x[4] for x in R), max(x[4] for x in R)), sets=max(x[5] for x in R), dur=round(R[-1][0] - t0, 2))
    return out


def day8(tr):
    D = tr.log['daily']
    return next((r for r in D if r['stage'] >= 8), D[-1])


def line(label, tr, detail=True):
    sd = tr.log['stage_day']
    r8 = day8(tr)
    b = r8['bonus']
    st = ' / '.join(f'{sd[s]:g}' for s in range(2, 9) if s in sd)
    gaps = [sd[s + 1] - sd[s] for s in range(1, 8) if s + 1 in sd]
    ratio = ' / '.join(f'{gaps[i + 1] / gaps[i]:.2f}' for i in range(len(gaps) - 1) if gaps[i] > 0)
    fm = sum(r['m_first'] for r in tr.log['daily'])
    print(f'- {label}: 2~8단계 {st} | 간격 비 {ratio}')
    if not detail:
        return tr
    print(f'    8단계 날(또는 끝) 효과: 버섯 {b["Harvest"]:.2f} 처치골드 {b["KillGold"]:.2f} 보스균사석 {b["BossMycelite"]:.2f} 제작 {b["CraftSpeed"]:.2f} '
          f'공격 {b["Attack"]:.2f} 체력 {b["Hp"]:.2f} 치명피해 {b["CritDamage"]:.2f} 공속 {b["Speed"]:.2f} 치명확률 {b["CritChance"]:.2f} '
          f'| 장식 {r8["decos"]}/{r8["placed"]}놓음 | 첫배치 균사석 합 {fm:.0f}')
    stars = tr.log['star_day']
    s3 = sorted((v.get(3), k) for k, v in stars.items() if v.get(3) is not None)
    s2 = sorted((v.get(2), k) for k, v in stars.items() if v.get(2) is not None)
    s1 = sorted((v.get(1), k) for k, v in stars.items() if v.get(1) is not None)
    fmt = lambda xs: (f'{xs[0][0]:g}~{xs[-1][0]:g}일 ({len(xs)}/8)' if xs else '없음')
    print(f'    ★1 {fmt(s1)} · ★2 {fmt(s2)} · ★3 {fmt(s3)} | 꼬마 성장 관문(Lv5·10) {list(tr.log["grow_day"].get("leaf", {}).values())}일')
    full = next((r['day'] for r in tr.log['daily'] if r['decos'] >= len(tr.T['decos'])), None)
    print(f'    장식 24개 다 만듦 {full}일 · 진화 순간 장식(보유+제작중/칸) ' + ', '.join(f'{k}:{v[0]}/{v[1]}' for k, v in tr.log['deco_at'].items()))
    for lo, hi in M.WINDOWS:
        x = M.flows(tr, lo, hi)
        if not x:
            continue
        print(f'    d{lo}-{hi}: 골드 쓰기 {x["g_spend"]:.1f}% (나이테 {x["g_ring"]:.1f} 진화 {x["g_evo"]:.1f} 공방 {x["g_craft"]:.1f}) · 부탁 +{x["g_req"]:.1f}% · 처치골드 효과 +{x["g_kill"]:.1f}% '
              f'| 균사석 쓰기 {x["m_spend"]:.0f}/일 (진화 {x["m_evo"]:.0f} ★ {x["m_star"]:.0f} 공방 {x["m_craft"]:.0f} 가속 {x["m_rush"]:.0f}) '
              f'세계수 수입 {x["m_in"]:.0f}/일 (주간상자 {x["m_week"]:.0f} 첫배치 {x["m_first"]:.0f} 보스효과 {x["m_boss"]:.1f}) 방치 {x["m_idle"]:.0f}/일 → 순 {x["m_net"]:.1f}% '
              f'| 경험치 공물 {x["exp"]:.0f} + 퀘스트 {x["q_exp"]:.0f}/일 (퀘스트 {x["q_share"]:.0f}%) · 부탁 {x["req"]:.1f}/일 · 꼬마 뽑기 {x["kid_pulls"]:.1f}/일')
    w = w7(tr)
    print('    W7′: ' + ' · '.join(f'{st}단계({v["dur"]}일) 채움 {v["fill"]}일·빈칸최대 {v["empty_max"]}·고를거리 {v["spare"][0]}~{v["spare"][1]}·세트 {v["sets"]}'
                          for st, v in w.items()))
    return tr


def table_checks(T):
    """W9: 단계·등급이 오를수록 비용·보상·칸이 줄지 않는다, int 안"""
    out = []
    tr = T['tree']
    for col in ('evoExp', 'evoGoldKills', 'evoMycelite'):
        v = [r[col] for r in tr[:-1]]
        out.append(f'{col} {v} 단조 {all(a <= b for a, b in zip(v, v[1:]))}')
    v = [r['ringKills'] for r in tr]
    out.append(f'ringKills {v} 단조 {all(a <= b for a, b in zip(v, v[1:]))}')
    v = [r['craftSlots'] for r in tr]
    out.append(f'craftSlots {v} 단조 {all(a <= b for a, b in zip(v, v[1:]))}')
    for i, z in enumerate(WT.ZONES):
        v = [r['slots'][i] for r in tr]
        out.append(f'{z} {v} 단조 {all(a <= b for a, b in zip(v, v[1:]))}')
    out.append('칸 합 ' + str([sum(r['slots']) for r in tr]))
    s = T['stars']
    out.append('꼬마진화 mycelite ' + str([x['mycelite'] for x in s]) + ' needLevel ' + str([x['needLevel'] for x in s]))
    g = T['dgrades']
    out.append('장식등급 ' + str([(x['goldKills'], x['mycelite'], x['hours']) for x in g]))
    ring_max = max(r['ringKills'] * T['const']['treeRingGrowth'] ** (T['const']['treeRingMax'] - 1) for r in tr)
    out.append(f'나이테 최대 비용 {ring_max:.0f} KE (× 적 처치 골드 ≤ 9e18이면 long 안: 적 처치 골드 ≤ {9e18 / ring_max:.2e})')
    out.append(f'evoExp 합 {sum(r["evoExp"] for r in tr)} (int 안)')
    return out


if __name__ == '__main__':
    T = WT.load_current()
    print('# 2026-11-12 모델 현행화 — 지금 표 그대로')
    print('창고 확률 (적.index: 1마리당):', {k: round(v, 4) for k, v in sorted(T['store_w'].items())}, '조각', T['store_slices'])
    print('퀘스트 세계수 경험치: 일일', sum(q['treeExp'] for q in T['quests'] if q['period'] == 'daily'),
          '/일 · 주간', sum(q['treeExp'] for q in T['quests'] if q['period'] == 'weekly'), '/주')
    print('\n## W9 표 점검')
    for x in table_checks(T):
        print('-', x)

    print('\n## 경우별 (pace = 고르고 바꿔 끼우기 · grade = 높은 등급부터, 교체 없음) — 메인 퀘스트 함')
    res = {}
    for case in FEEDS:
        for pol in ('pace', 'grade'):
            res[(case, pol)] = line(f'{case} · {pol}', run(case, pol), detail=(pol == 'pace' or case == '보통'))

    print('\n## 민감도 (보통)')
    line('보통 · firstall (첫 배치 보상 다 받기)', run('보통', 'firstall'), detail=False)
    for case in ('보통', '무광고', '광고 많이'):
        line(f'{case} · pace · 퀘스트 세계수 경험치 없음', run(case, 'pace', quests=False), detail=False)
        line(f'{case} · grade · 퀘스트 세계수 경험치 없음', run(case, 'grade', quests=False), detail=False)
    line('보통 · pace · 일일만 (주간 안 함)', run('보통', 'pace', quest_only={q['key'] for q in T['quests'] if q['period'] == 'daily'}), detail=False)
    line('보통 · pace · 꼬마 성장 안 함 (Lv5 관문에서 멈춤)', run('보통', 'pace', kid_grow=False))
    line('보통 · pace · 꼬마 뽑기 ×0.5', run('보통', 'pace', kid_pull_mul=0.5), detail=False)
    line('보통 · pace · 꼬마 뽑기 ×2', run('보통', 'pace', kid_pull_mul=2.0), detail=False)
    line('보통 · pace · 하루 처치 ×0.8 (방치 feed 오차)', run('보통', 'pace', kill_mul=0.8), detail=False)
    line('보통 · pace · 하루 처치 ×1.2', run('보통', 'pace', kill_mul=1.2), detail=False)
    T2 = WT.load_current()
    T2['store_w'] = {k: v for k, v in T2['store_w'].items() if k <= 1005}      # 상수 9079 설명대로 독성·약용이 창고에 안 들어간다면
    line('보통 · pace · 창고 = 식용만 (독성·약용 제외)', run('보통', 'pace', T=T2), detail=False)
    line('보통 · pace · 가속 씀', run('보통', 'pace', rush=True), detail=False)
    line('보통 · pace · 예산 골드·균사석 5%', run('보통', 'pace', gold_frac=0.05, myc_frac=0.05), detail=False)
    line('보통 · pace · 예산 골드·균사석 20%', run('보통', 'pace', gold_frac=0.20, myc_frac=0.20), detail=False)

    print('\n## W7′(c)(d) 목표별 배치 (보통 · 퀘스트 함)')
    placed8 = {}
    for g, w in V.GOALS.items():
        tr = run('보통', 'pace', weights=w)
        line(f'목표 {g}', tr, detail=False)
        placed8[g] = set(tr.placed)
        print('    8단계 배치:', sorted(tr.placed))
    tr = res[('보통', 'grade')]
    placed8['grade'] = set(tr.placed)
    alld = {d['key'] for d in T['decos']}
    union = set().union(*placed8.values())
    print('- 어디에도 안 드는 장식:', sorted(alld - union) or '없음')
