#!/usr/bin/env python3
"""Fetch the pinned Chinese OCR model and dictionary; verify before replacement."""
import hashlib
import json
import os
from pathlib import Path
import tempfile
import urllib.request

root = Path(__file__).resolve().parents[1] / 'models' / 'chinese'
root.mkdir(parents=True, exist_ok=True)
for model in json.loads((root / 'sources.json').read_text(encoding='utf-8')):
    name = model['file']
    if Path(name).name != name or not model['url'].startswith('https://'):
        raise ValueError('Invalid model source entry')
    target = root / name
    def valid(path):
        return path.is_file() and path.stat().st_size == model['bytes'] and hashlib.sha256(path.read_bytes()).hexdigest() == model['sha256']
    if valid(target):
        print('Verified:', name)
        continue
    descriptor, temporary = tempfile.mkstemp(prefix='.' + name + '.', suffix='.download', dir=root)
    temporary = Path(temporary)
    try:
        with os.fdopen(descriptor, 'wb') as output, urllib.request.urlopen(model['url'], timeout=120) as response:
            while chunk := response.read(1024 * 1024):
                output.write(chunk)
            output.flush()
            os.fsync(output.fileno())
        if not valid(temporary):
            raise ValueError('Model size or SHA-256 mismatch: ' + name)
        os.replace(temporary, target)
        print('Downloaded and verified:', name)
    finally:
        if temporary.exists():
            temporary.unlink()
