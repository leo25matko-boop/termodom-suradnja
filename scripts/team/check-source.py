"""Bounded tracked-source scan. This is not a complete secrets detector."""
import re, subprocess, sys
from pathlib import Path
names=subprocess.check_output(['git','ls-files','-z']).decode().split('\0')
bad=[]
patterns=[re.compile(rb'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'),
          re.compile(rb'(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,})'),
          re.compile(rb'\bsk-(?:proj-|svcacct-)?[A-Za-z0-9_-]{40,}'),
          re.compile(rb'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b')]
for name in filter(None,names):
    path=Path(name)
    if (path.name in {'auth.json','history.jsonl','connections.json','local.properties'} or
        path.suffix.lower() in {'.jks','.keystore','.p12','.pem','.key','.termodom','.exe','.apk'} or
        name.startswith(('work/','node_modules/','desktop/','public/'))):
        bad.append((name,'unexpected file'))
        continue
    data=path.read_bytes()
    if any(p.search(data) for p in patterns):bad.append((name,'credential pattern'))
    for token in re.findall(rb'(?:[?&#]|&amp;)kk=([A-Za-z0-9_-]{15,})',data):
        if token not in {b'syntheticTestToken123',b'fixtureTokenABC'}:
            bad.append((name,'non-example solar share token'))
if bad:
    for name,reason in bad:print(name+': '+reason)
    sys.exit(1)
print('TRACKED_SOURCE_CHECK_OK: no credential files or recognized real-token patterns; this is a heuristic, not a guarantee.')
