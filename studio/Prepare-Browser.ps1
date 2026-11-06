# Windows Sandbox images can ship without an HTTPS browser. Install Mozilla's
# signed full Firefox installer inside the disposable guest, without MSI and
# without importing any host browser profile, account or credentials.
# https://support.mozilla.org/en-US/kb/silently-install-uninstall-firefox-enterprise
# https://wiki.mozilla.org/Release_Management/Product_details
$ErrorActionPreference='Stop'
if ($env:USERNAME -ne 'WDAGUtilityAccount' -or
    -not (Test-Path -LiteralPath C:\TermoDom\App -PathType Container) -or
    -not (Test-Path -LiteralPath C:\TermoDom\Project -PathType Container) -or
    -not (Test-Path -LiteralPath C:\TermoDom\Data -PathType Container)) {
  throw 'Browser preparation is allowed only inside the TermoDom Windows Sandbox.'
}
function Test-PublisherSignature([string]$FilePath,[string]$Publisher) {
  $signature=Get-AuthenticodeSignature -LiteralPath $FilePath
  $organization='(^|,\s*)O='+[regex]::Escape($Publisher)+'(,|$)'
  return ($signature.Status -eq 'Valid' -and $null -ne $signature.SignerCertificate -and
    $signature.SignerCertificate.GetNameInfo([Security.Cryptography.X509Certificates.X509NameType]::SimpleName,$false) -eq $Publisher -and
    $signature.SignerCertificate.Subject -match $organization)
}
function Find-VerifiedStudioBrowser([switch]$FirefoxOnly) {
  $incompleteFirefox=$false
  foreach ($browser in @(
    @{RelativePath='Mozilla Firefox\firefox.exe';Publisher='Mozilla Corporation'},
    @{RelativePath='Microsoft\Edge\Application\msedge.exe';Publisher='Microsoft Corporation'})) {
    if ($browser.Publisher -eq 'Microsoft Corporation' -and ($FirefoxOnly -or $incompleteFirefox)) {continue}
    foreach ($programRoot in @($env:ProgramFiles,${env:ProgramFiles(x86)})) {
      if (-not $programRoot) {continue}
      $executable=Join-Path $programRoot $browser.RelativePath
      if (Test-Path -LiteralPath $executable -PathType Leaf) {
        try {
          if (Test-PublisherSignature $executable $browser.Publisher) {
            $application=Split-Path -Parent $executable
            if ($browser.Publisher -eq 'Mozilla Corporation') {
              if ((Test-Path -LiteralPath (Join-Path $application 'xul.dll') -PathType Leaf) -and (Test-Path -LiteralPath (Join-Path $application 'mozglue.dll') -PathType Leaf)) {return $executable}
            } else {
              $edgeVersion=(Get-Item -LiteralPath $executable).VersionInfo.FileVersion
              if ($edgeVersion -match '^\d+\.\d+\.\d+\.\d+$') {
                $edgeRuntime=Join-Path $application ($edgeVersion+'\msedge.dll')
                if ((Test-Path -LiteralPath $edgeRuntime -PathType Leaf) -and (Test-PublisherSignature $edgeRuntime 'Microsoft Corporation')) {return $executable}
              }
            }
          }
        } catch { }
        if ($browser.Publisher -eq 'Mozilla Corporation') {$incompleteFirefox=$true}
      }
    }
  }
  return $null
}
if (Find-VerifiedStudioBrowser) {Write-Output 'The official browser is ready in Windows Sandbox.';return}
$principal=[Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {throw 'The Windows Sandbox user must have its standard guest administrator rights to prepare the browser.'}
[Net.ServicePointManager]::SecurityProtocol=[Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$ProgressPreference='SilentlyContinue'
$versions=Invoke-RestMethod -Uri 'https://product-details.mozilla.org/1.0/firefox_versions.json' -TimeoutSec 60
$version=[string]$versions.LATEST_FIREFOX_VERSION
if ($version -notmatch '^\d+\.\d+(\.\d+){0,2}$') {throw 'Mozilla did not return a valid stable Firefox release version.'}
$releaseBase='https://archive.mozilla.org/pub/firefox/releases/'+$version+'/'
$relative='win64/en-US/Firefox Setup '+$version+'.exe'
$checksumText=(Invoke-WebRequest -UseBasicParsing -Uri ($releaseBase+'SHA512SUMS') -TimeoutSec 60).Content
$checksumPattern='^(?<digest>[a-fA-F0-9]{128})\s+\*?'+[regex]::Escape($relative)+'\s*$'
$checksumEntries=@($checksumText -split '\r?\n'|ForEach-Object {[regex]::Match($_,$checksumPattern)}|Where-Object {$_.Success})
if ($checksumEntries.Count -ne 1) {throw 'Mozilla SHA512SUMS did not contain exactly one checksum for the Windows x64 full installer. Installation was refused.'}
$expectedHash=$checksumEntries[0].Groups['digest'].Value
$downloadUri=$releaseBase+'win64/en-US/Firefox%20Setup%20'+$version+'.exe'
$cache='C:\TermoDom\Data\Tools\Browser'
foreach ($folder in @('C:\TermoDom\Data','C:\TermoDom\Data\Tools',$cache)) {
  if (Test-Path -LiteralPath $folder) {
    if ((Get-Item -LiteralPath $folder -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {throw 'The browser cache must not contain directory links.'}
  } else {New-Item -ItemType Directory -Path $folder | Out-Null}
}
$cachedInstaller=Join-Path $cache ('FirefoxSetup-'+$version+'-x64.exe')
function Test-FirefoxPackage([string]$FilePath) {
  if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) {return $false}
  if ((Get-Item -LiteralPath $FilePath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {return $false}
  return ((Get-FileHash -LiteralPath $FilePath -Algorithm SHA512).Hash -eq $expectedHash -and (Test-PublisherSignature $FilePath 'Mozilla Corporation'))
}
if (-not (Test-FirefoxPackage $cachedInstaller)) {
  $temporary=Join-Path $cache ('firefox-'+[guid]::NewGuid().ToString('N')+'.download.exe')
  try {
    Write-Output ('Preparing official Firefox '+$version+' for secure sign-in...')
    Invoke-WebRequest -UseBasicParsing -Uri $downloadUri -OutFile $temporary -TimeoutSec 900
    if (-not (Test-FirefoxPackage $temporary)) {throw 'The Firefox download failed SHA512 or Mozilla Authenticode verification. Installation was refused.'}
    Move-Item -LiteralPath $temporary -Destination $cachedInstaller -Force
  } finally {
    if (Test-Path -LiteralPath $temporary) {Remove-Item -LiteralPath $temporary -Force}
  }
}
# Stage locally so the full installer and its child processes do not depend on
# access to a mapped host cache. No Windows Installer/MSI process is started.
$localSetup=Join-Path ([IO.Path]::GetTempPath()) ('TermoDomStudio-Browser-'+[guid]::NewGuid().ToString('N'))
if (-not [IO.Path]::IsPathRooted($localSetup) -or $localSetup.StartsWith('C:\TermoDom\',[StringComparison]::OrdinalIgnoreCase) -or $localSetup.StartsWith('\\')) {throw 'The browser installation staging folder must be local to the disposable guest.'}
New-Item -ItemType Directory -Path $localSetup | Out-Null
$localInstaller=Join-Path $localSetup 'FirefoxSetup.exe'
$installerFinished=$false
try {
  Copy-Item -LiteralPath $cachedInstaller -Destination $localInstaller
  if ((Get-FileHash -LiteralPath $localInstaller -Algorithm SHA512).Hash -ne $expectedHash) {throw 'The guest-local Firefox package failed SHA512 verification. Installation was refused.'}
  $process=Start-Process -FilePath $localInstaller -ArgumentList '/S' -WindowStyle Hidden -PassThru
  if (-not $process.WaitForExit(300000)) {throw 'The Firefox full installer is still running in this disposable guest. Allow it to finish before reopening Studio.'}
  $installerFinished=$true
  if ($process.ExitCode -ne 0) {throw ('The Firefox full installer failed with exit code '+$process.ExitCode+'. Reopen Studio to retry. No unverified browser will be launched.')}
} finally {
  if ($installerFinished -and (Test-Path -LiteralPath $localInstaller)) {
    try {Remove-Item -LiteralPath $localInstaller -Force} catch {Write-Warning 'The temporary Firefox installer remains in this disposable guest until Windows Sandbox closes.'}
  }
}
$browserDeadline=[DateTime]::UtcNow.AddSeconds(30)
$verifiedFirefox=$null
do {
  $verifiedFirefox=Find-VerifiedStudioBrowser -FirefoxOnly
  if (-not $verifiedFirefox) {Start-Sleep -Milliseconds 500}
} while (-not $verifiedFirefox -and [DateTime]::UtcNow -lt $browserDeadline)
if (-not $verifiedFirefox) {throw 'Firefox setup did not produce a verified browser within 30 seconds after installer exit. Studio sign-in cannot start yet.'}
Write-Output 'The official browser is ready in Windows Sandbox.'
