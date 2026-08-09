$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
python "$ScriptDir\verify_bundle.py"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
