# 스테이지 표 수치안: 70 이후 적 체력·공격 배율 + 50배수 보스 벽
import openpyxl,sys
src,dst,ramp,full,wall,atkmul=sys.argv[1],sys.argv[2],float(sys.argv[3]),int(sys.argv[4]),float(sys.argv[5]),float(sys.argv[6])
wall2=float(sys.argv[7]) if len(sys.argv)>7 else wall   # 200 뒤 벽 배율
START=70
wb=openpyxl.load_workbook(src); ws=wb['스테이지']
hdr=[c.value for c in ws[1]]; col={h:i+1 for i,h in enumerate(hdr)}
for r in range(4,ws.max_row+1):
    s=ws.cell(r,col['stage']).value
    if not isinstance(s,(int,float)) or s<=START: continue
    m=1+ramp*min(1,(s-START)/(full-START))
    am=1+(m-1)*atkmul
    for k,mul in (('enemyHp',m),('bossHp',m*((wall if s<=200 else wall2) if s%50==0 else 1)),('enemyAttack',am),('bossAttack',am)):
        c=ws.cell(r,col[k]); c.value=float(f'{c.value*mul:.6g}')
wb.save(dst)
