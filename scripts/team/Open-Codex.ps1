[CmdletBinding()]
param([string]$ProjectPath=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path)
$ErrorActionPreference='Stop'
if($env:CODEX_HOME) { throw 'Koristite vlastiti standardni Windows profil; ne naslijedeni CODEX_HOME.' }
Get-Command codex -ErrorAction Stop | Out-Null
$path=(Resolve-Path -LiteralPath $ProjectPath).Path
& codex app $path
if($LASTEXITCODE -ne 0) { throw 'Otvorite Codex rucno, prijavite svoj racun i odaberite mapu projekta.' }
