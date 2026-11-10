import csv,sys,statistics as st,glob
pref=sys.argv[1] if len(sys.argv)>1 else 'k'
cps=[5,10,20,30,50,75,100,150,200,250,300]
def load(tag):
    rows=list(csv.DictReader(open(f'stage_{tag}.csv')))
    return {int(r['stage']):(float(r['wallSec']),float(r['playSec'])) for r in rows}
for cur in sys.argv[2].split(','):
  for nm in ['B','Bhalf','Bads']:
    ds=[load(f'{pref}_{cur}_{nm}_s{s}') for s in (1,2,3)]
    def med(stg,i): 
        v=[d[stg][i] for d in ds if stg in d]; return st.median(v) if v else None
    line=[]
    for c in cps:
        p=med(c,1); w=med(c,0)
        line.append('-' if p is None else f'{p/3600:.2f}h/{w/86400:.1f}d')
    # longest single-stage play gaps
    gaps=[]
    d=ds[0]; ks=sorted(d)
    for a,b in zip(ks,ks[1:]):
        gaps.append((d[b][1]-d[a][1],b))
    gaps.sort(reverse=True)
    print(f'{cur:5s} {nm:6s}',' '.join(line))
    print('      top stage play gaps(min):',', '.join(f'{b}:{g/60:.0f}' for g,b in gaps[:6]))
print('cps',cps)
