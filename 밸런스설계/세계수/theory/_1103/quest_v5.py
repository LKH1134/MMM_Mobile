# 의뢰 v5 수치안 (x = 1000) 전체 요약: python quest_v5.py > quest_v5.log
import sys
sys.dont_write_bytecode = True
sys.stdout.reconfigure(encoding='utf-8')
import wt_model as M
from quest_model import QuestBook
from quest_run import run, FEEDS

V5_D = [('stage', 30, 1000), ('kill', 4000, 1000), ('species', 350, 1000), ('combo', 5, 1000)]
V5_W = [('daily', 14, 4000), ('stage', 350, 3000), ('kill', 40000, 3000)]

if __name__ == '__main__':
    for k in ('보통', '무광고', '광고 많이', '×0.5', '×2'):
        for cp in ((0.8, 0.0) if k == '보통' else (0.8,)):
            tr = run(FEEDS[k], QuestBook(V5_D, V5_W, combo_p=cp, stage_kind='boss'))
            print(M.summary(tr, f'v5 · {k} · combo p={cp:g}'))
            print()
