[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$CodexPackageDirectory,
 [Parameter(Mandatory)][string]$GitDirectory,
 [Parameter(Mandatory)][string]$ToolManifest,
 [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$out=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $out | Out-Null
$app=Join-Path $out 'app'
& dotnet publish (Join-Path $PSScriptRoot 'TermoDom.Studio.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o $app
if($LASTEXITCODE){throw 'Studio publish failed.'}
Copy-Item -LiteralPath $ToolManifest -Destination (Join-Path $app 'tool-manifest.json')
New-Item -ItemType Directory -Force (Join-Path $app 'Tools') | Out-Null
Copy-Item -LiteralPath $CodexPackageDirectory -Destination (Join-Path $app 'Tools/codex') -Recurse
Copy-Item -LiteralPath $GitDirectory -Destination (Join-Path $app 'Tools/git') -Recurse
& git -C $repo archive --format=zip "--output=$app/ProjectSeed.zip" HEAD
if($LASTEXITCODE){throw 'Project archive failed.'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::Open((Join-Path $app 'ProjectSeed.zip'),[IO.Compression.ZipArchiveMode]::Update)
try{@($zip.Entries | Where-Object {$_.FullName.StartsWith('.codex/')}) | ForEach-Object {$_.Delete()}}finally{$zip.Dispose()}
& git -C $repo bundle create (Join-Path $app 'Project.bundle') HEAD
if($LASTEXITCODE){throw 'Project Git bundle failed.'}
$payload=Join-Path $out 'StudioPayload.zip'
if(Test-Path -LiteralPath $payload){throw 'Use a fresh output directory; an existing payload is never overwritten automatically.'}
[IO.Compression.ZipFile]::CreateFromDirectory($app,$payload,[IO.Compression.CompressionLevel]::Optimal,$false)
& dotnet publish (Join-Path $PSScriptRoot 'installer/Studio.Setup.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true "-p:PayloadPath=$payload" -o (Join-Path $out 'installer')
if($LASTEXITCODE){throw 'Installer publish failed.'}
Write-Output (Join-Path $out 'installer/TermoDom-Studio-Setup-1.0.1-x64.exe')
