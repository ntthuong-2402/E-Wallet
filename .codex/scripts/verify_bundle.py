from pathlib import Path
import sys, re
try:
    import tomllib
except ImportError:
    print("Python 3.11+ is required for tomllib verification.")
    sys.exit(2)

root = Path(__file__).resolve().parents[2]
errors = []
warnings = []

required = [
    'AGENTS.md', '.codex/config.toml', '.codex/state/CURRENT_TASK.md',
    '.codex/state/DECISIONS.md', '.codex/state/VERIFICATION.md',
]
for rel in required:
    if not (root / rel).exists():
        errors.append(f'Missing required file: {rel}')

for p in (root / '.codex/agents').glob('*.toml'):
    try:
        data = tomllib.loads(p.read_text(encoding='utf-8'))
    except Exception as exc:
        errors.append(f'Invalid TOML {p.relative_to(root)}: {exc}')
        continue
    for key in ('name', 'description', 'sandbox_mode', 'developer_instructions'):
        if key not in data:
            errors.append(f'{p.relative_to(root)} missing {key}')
    if data.get('sandbox_mode') not in ('read-only', 'workspace-write', 'danger-full-access'):
        errors.append(f'{p.relative_to(root)} invalid sandbox_mode')

try:
    tomllib.loads((root / '.codex/config.toml').read_text(encoding='utf-8'))
except Exception as exc:
    errors.append(f'Invalid .codex/config.toml: {exc}')

skills_root = root / '.agents/skills'
for skill in skills_root.glob('*/SKILL.md'):
    text = skill.read_text(encoding='utf-8')
    if not re.search(r'^---\s*\n.*?^name:\s*\S+.*?^description:\s*.+?^---\s*$', text, re.M | re.S):
        errors.append(f'Invalid/missing skill frontmatter: {skill.relative_to(root)}')

limits = {
    'AGENTS.md': 32768,
    '.codex/state/CURRENT_TASK.md': 12288,
    '.codex/state/VERIFICATION.md': 12288,
}
for rel, limit in limits.items():
    p = root / rel
    if p.exists() and p.stat().st_size > limit:
        warnings.append(f'{rel} is {p.stat().st_size} bytes; target <= {limit}')

# Simple secret-pattern check; intentionally conservative.
secret_patterns = [
    re.compile(r'(?i)(password|client_secret|api[_-]?key)\s*=\s*["\'][^"\']{8,}["\']'),
    re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'),
]
for p in root.rglob('*'):
    if p.is_file() and p.suffix.lower() in {'.md','.toml','.txt','.ps1','.sh','.py'}:
        text = p.read_text(encoding='utf-8', errors='ignore')
        for pat in secret_patterns:
            if pat.search(text):
                errors.append(f'Possible secret material in {p.relative_to(root)}')
                break

print('Codex banking context-safe bundle verification')
print(f'Root: {root}')
print(f'Errors: {len(errors)}')
for e in errors:
    print(f'ERROR: {e}')
print(f'Warnings: {len(warnings)}')
for w in warnings:
    print(f'WARN: {w}')
if errors:
    sys.exit(1)
print('RESULT: PASS')
