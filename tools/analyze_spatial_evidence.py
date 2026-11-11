"""Independently audit saved candidate scores and summarize desktop timings."""
from pathlib import Path
import collections
import json
import statistics

root = Path(__file__).resolve().parents[1]
rows = [json.loads(line) for line in (root / 'evidence/spatial-sweep.jsonl').read_text().splitlines()]
selections = {}
for row in rows:
    eligible = [candidate for candidate in row['candidates'] if candidate['eligible']]
    assert len(eligible) == row['eligible']
    if eligible:
        chosen = next(candidate for candidate in eligible if candidate['id'] == row['selected'])
        assert abs(chosen['total'] - min(candidate['total'] for candidate in eligible)) < 1e-7
    for candidate in row['candidates']:
        assert all(0 <= candidate[key] <= 1.000001 for key in ('interruption', 'occlusion', 'social', 'total'))
    selections.setdefault(row['context'] + '/' + row['priority'], collections.Counter())[row['selected']] += 1
milliseconds = sorted(row['evaluationMilliseconds'] for row in rows)
summary = {
    'scenarioCount': len(rows),
    'minimumSelectionVerified': True,
    'evaluationMilliseconds': {
        'median': statistics.median(milliseconds),
        'p95': milliseconds[int((len(milliseconds) - 1) * .95)],
        'max': max(milliseconds),
    },
    'selections': selections,
    'timingScope': 'Mac Unity EditMode synchronous spatial evaluation, not headset frame timing',
}
(root / 'evidence/spatial-analysis.json').write_text(json.dumps(summary, indent=2) + '\n')
print('Verified', len(rows), 'scenarios and saved evidence/spatial-analysis.json')
