# out_v3d_<이름>_<ads>.json → 첫 7일 하루별 표 (이론.md 6.7).  python day_md.py v3 0.5
import json, sys
KO = {'battle': '전투', 'offline': '앱밖', 'offline_ad': '앱밖2배', 'adbox': '상자', 'quest': '퀘스트', 'dungeon': '던전', 'boss': '보스'}
d = json.load(open(f'out_v3d_{sys.argv[1]}_{sys.argv[2]}.json', encoding='utf-8'))
print('| 날 | 끝 스테이지 (목표) | 가장 긴 벽: 스테이지 · 플레이 분 · 달력 일 | 처음 열리는 것 | 접속 이유: 퀘스트 · 던전 입장 · 상자 | 골드 출처 % | 균사석 출처 |')
print('|---|---|---|---|---|---|---|')
for r in d['table']:
    g = ' · '.join(f"{KO[k]} {v:.0f}" for k, v in sorted(r['gold_pct'].items(), key=lambda x: -x[1]) if v >= 3)
    m = ' · '.join(f"{KO[k]} {v}" for k, v in r['myc'].items() if v)
    print(f"| D{r['day']} | {r['stage']} ({r['target']}) | s{r['wall_stage']} · {r['wall_play_min']:.0f}분 · {r['wall_cal_days']}일 | {', '.join(r['new']) or '–'} | {r['quests']}/5 · {r['dungeon_entries']}회 · {r['boxes']}개 | {g} | {m} |")
