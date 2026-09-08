$ErrorActionPreference = "Stop"
if (-not (Get-Command coord-guard -ErrorAction SilentlyContinue)) {
    throw "Install coord-guard before setting up hooks."
}
$hook = git rev-parse --git-path hooks/pre-commit
if ($LASTEXITCODE -ne 0) { throw "Run this script inside the repository." }
if ((Test-Path $hook) -and (Get-Item $hook).Length -gt 0) {
    if (-not (Select-String -Path $hook -Pattern 'coord-guard' -Quiet)) {
        throw "Existing unrelated pre-commit hook preserved; add coord-guard manually."
    }
    Write-Output "Coordination guard is already installed."
    exit 0
}
New-Item -ItemType Directory -Force -Path (Split-Path $hook) | Out-Null
$content = @'
#!/bin/sh
set -eu
coord-guard
'@
[IO.File]::WriteAllText($hook, $content.Replace([char]13, "") + [char]10, [Text.UTF8Encoding]::new($false))
Write-Output "Installed coordination guard."
