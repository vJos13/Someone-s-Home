#!/bin/bash
unity command script.eval --code "$(cat "$1")" --timeout ${2:-120} 2>&1 | python3 -c "
import json,sys
lines=sys.stdin.read().splitlines()
row=[l for l in lines if l.startswith('script.eval')]
if not row: print('\n'.join(lines[-6:])); sys.exit(1)
cols=row[0].split('\t'); r=json.loads(cols[2])
if r.get('diagnostics'): print('DIAG',r['diagnostics'])
if r.get('output'): print('OUT',r['output'])
res=r.get('result'); print(res if isinstance(res,str) else json.dumps(res,indent=1))
"
