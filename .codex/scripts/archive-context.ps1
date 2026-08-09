$ErrorActionPreference = "Stop"
$StateDir = Split-Path -Parent $PSScriptRoot
$HistoryDir = Join-Path $StateDir "history"
New-Item -ItemType Directory -Force -Path $HistoryDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$out = Join-Path $HistoryDir "$stamp-context.md"
"# Archived Context $stamp`n" | Set-Content -Encoding UTF8 $out
if (Test-Path (Join-Path $StateDir "CURRENT_TASK.md")) {
  "`n## Current Task`n" | Add-Content -Encoding UTF8 $out
  Get-Content (Join-Path $StateDir "CURRENT_TASK.md") | Add-Content -Encoding UTF8 $out
}
if (Test-Path (Join-Path $StateDir "VERIFICATION.md")) {
  "`n## Verification`n" | Add-Content -Encoding UTF8 $out
  Get-Content (Join-Path $StateDir "VERIFICATION.md") | Add-Content -Encoding UTF8 $out
}
Write-Host "Archived context to $out"
