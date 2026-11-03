# 라운드 4: 검증 래퍼(feed.py v3 = 라이브 표에서 직접 만든 입력)가 KHD v3 진행(out_v3d_v3_<ads>.json, run_v3d.py 결과)을 재현하는지,
# 700→701 꼬리가 이어지는지 (wt-verify, 읽기만)
import sys, os, json
sys.stdout.reconfigure(encoding='utf-8')
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
TH = os.path.abspath(os.path.join(HERE, '../../../theory'))
for ads in ('0.0', '0.5', '1.0'):
    k = json.load(open(os.path.join(TH, f'out_v3d_v3_{ads}.json'), encoding='utf-8'))['stage_by_day'][:60]
    m = [d['stage'] for d in json.load(open(os.path.join(HERE, f'feed_v3_a{ads}.json'), encoding='utf-8'))['days']]
    diff = [(i + 1, a, b) for i, (a, b) in enumerate(zip(k, m)) if a != b]
    print(f'광고 {ads}: 60일 중 다른 날 {len(diff)}일, 최대 차 {max([abs(a - b) for _, a, b in diff], default=0)} 스테이지', diff[:12])
sys.path.insert(0, TH); sys.path.insert(0, os.path.dirname(HERE))
import feed, formulas as F
feed.setup_live()
for s in (698, 699, 700, 701, 702, 709, 710, 711, 1000, 2000, 5000, 9000):
    print(f'  s{s}: 처치 골드 {F.enemy_gold(s):.4g} 보스 골드 {F.boss_gold(s):.4g} 보스 체력 {F.boss_hp(s):.4g} 적 공격 {F.enemy_attack(s):.4g} 보스 균사석 {F.boss_myc(s)}')
