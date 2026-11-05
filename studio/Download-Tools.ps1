[CmdletBinding()]
param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
$out=[IO.Path]::GetFullPath($Destination)
if(Test-Path -LiteralPath $out){throw 'Use a fresh tool directory.'}
New-Item -ItemType Directory -Force $out | Out-Null
$manifest=Get-Content (Join-Path $PSScriptRoot 'tool-manifest.json') -Raw | ConvertFrom-Json
$codex=Join-Path $out 'codex.tgz'
Invoke-WebRequest -Uri $manifest.codex.url -OutFile $codex
$hash=[Security.Cryptography.SHA512]::Create()
try{$digest='sha512-'+[Convert]::ToBase64String($hash.ComputeHash([IO.File]::ReadAllBytes($codex)))}finally{$hash.Dispose()}
if($digest -cne $manifest.codex.integrity){throw 'Codex package integrity mismatch.'}
& tar -xzf $codex -C $out
if($LASTEXITCODE){throw 'Codex archive extraction failed.'}
Move-Item -LiteralPath (Join-Path $out 'package') -Destination (Join-Path $out 'codex')
foreach($name in 'LICENSE','NOTICE') {Invoke-WebRequest -Uri "https://raw.githubusercontent.com/openai/codex/rust-v0.166.0/$name" -OutFile (Join-Path $out "codex/$name")}
$git=Join-Path $out 'git.zip'
Invoke-WebRequest -Uri $manifest.git.url -OutFile $git
if((Get-FileHash -LiteralPath $git -Algorithm SHA256).Hash.ToLowerInvariant() -ne $manifest.git.sha256){throw 'Git package checksum mismatch.'}
Expand-Archive -LiteralPath $git -DestinationPath (Join-Path $out 'git')
Write-Output 'Official Codex and MinGit packages downloaded and checked. No account credentials were used.'
