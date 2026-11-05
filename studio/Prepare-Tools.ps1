$ErrorActionPreference='Stop'
if ($env:USERNAME -ne 'WDAGUtilityAccount' -or -not (Test-Path C:\TermoDom\worker-ready.json)) {throw 'Run only inside TermoDom Sandbox.'}
$target='C:\TermoDom\Data\Tools\dotnet'
if(Test-Path "$target\dotnet.exe"){Write-Output '.NET SDK je već pripremljen.';exit 0}
$cache='C:\TermoDom\Data\Downloads';New-Item -ItemType Directory -Force $cache | Out-Null
$archive=Join-Path $cache 'dotnet-sdk-8.0.426-win-x64.zip'
$manifest=Get-Content C:\TermoDom\App\tool-manifest.json -Raw | ConvertFrom-Json
Write-Output 'Preuzimanje službenog Microsoft .NET 8 SDK-a (oko 285 MB).'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -UseBasicParsing -Uri $manifest.dotnet.url -OutFile $archive
if((Get-FileHash -Algorithm SHA512 -LiteralPath $archive).Hash.ToLowerInvariant() -ne $manifest.dotnet.hash.ToLowerInvariant()){throw 'SDK checksum mismatch; archive was not executed.'}
$stage='C:\TermoDom\Data\Tools\dotnet-stage-'+[guid]::NewGuid().ToString('N')
New-Item -ItemType Directory -Force $stage | Out-Null
Expand-Archive -LiteralPath $archive -DestinationPath $stage
Move-Item -LiteralPath $stage -Destination $target
Write-Output '.NET SDK je pripremljen. Windows Build/Test/Run sada može koristiti dotnet.'
