# Install on Windows

Assume your repository is `F:\Project\prime-cbs` (replace with your actual target repository if different).

## Option A — copy with Explorer

Copy these items to the Git repository root:
- `AGENTS.md`
- `.codex\`
- `.agents\`
- nested `AGENTS.md` files only where their service directories actually match your repository

Do not blindly overwrite an existing `AGENTS.md` without merging its rules.

## Option B — PowerShell example

From the extracted bundle directory:

```powershell
Copy-Item .\AGENTS.md F:\Project\prime-cbs\AGENTS.md
Copy-Item .\.codex F:\Project\prime-cbs\.codex -Recurse -Force
Copy-Item .\.agents F:\Project\prime-cbs\.agents -Recurse -Force
```

If `prime-cbs` already has its own service layout, manually place service-specific `AGENTS.md` files into the appropriate real directories instead of copying the sample `src` tree.

Then run:

```powershell
cd F:\Project\prime-cbs
powershell -ExecutionPolicy Bypass -File .codex\scripts\verify.ps1
```

Finally start a fresh Codex session in the repository and ask it to summarize loaded instructions plus current checkpoint state.
