import openpyxl,sys
src,dst,m2,m4,m5=sys.argv[1],sys.argv[2],float(sys.argv[3]),float(sys.argv[4]),float(sys.argv[5])
wb=openpyxl.load_workbook(src); ws=wb['던전']; hdr=[c.value for c in ws[1]]; col={h:i+1 for i,h in enumerate(hdr)}
for r in range(4,ws.max_row+1):
    d=ws.cell(r,col['dungeon']).value; m={2:m2,4:m4,5:m5}.get(d,1)
    c=ws.cell(r,col['rewardKills']); c.value=int(round(c.value*m))
wb.save(dst)
