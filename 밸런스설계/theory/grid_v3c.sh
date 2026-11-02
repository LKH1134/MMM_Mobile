cd /c/Project/MMM_Mobile/밸런스설계/theory
for q in 5 10 20; do for d in 1 2; do for a in 0.0 0.5; do
PYTHONIOENCODING=utf-8 python run_v3c.py g_q${q}_d${d} 1.4 $a 30 $q $d 1 | sed 's/DAY1.*//' &
done; done; wait; done
