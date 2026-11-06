# Trusted installer helper. Only invoked after the user chooses Studio setup.
# ExecutionPolicy Bypass applies to this process, never to the Windows policy.
param([Parameter(Mandatory=$true)][string]$ResultPath)
$ErrorActionPreference='Stop'
$result=[ordered]@{Success=$false;RestartNeeded=$false;Message='';CompletedUtc='';FeatureState=$null}
$code=1
try {
 if (-not [IO.Path]::IsPathRooted($ResultPath) -or $ResultPath.StartsWith('\\')) {throw 'Setup requires a local absolute result path.'}
 $identity=[Security.Principal.WindowsIdentity]::GetCurrent()
 if (-not ([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {throw 'Windows administrator approval is required.'}
 $feature=Get-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM
 $result.FeatureState=$feature.State.ToString()
 if ($result.FeatureState -eq 'EnablePending') {
  $result.RestartNeeded=$true
 } elseif ($result.FeatureState -ne 'Enabled') {
  $enabled=Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -All -NoRestart
  $result.RestartNeeded=[bool]$enabled.RestartNeeded
  $result.FeatureState=(Get-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM).State.ToString()
  if ($result.FeatureState -eq 'EnablePending') {$result.RestartNeeded=$true}
 }
 $sandbox=Join-Path $env:WINDIR 'System32\WindowsSandbox.exe'
 if (-not $result.RestartNeeded -and -not (Test-Path -LiteralPath $sandbox)) {throw 'Windows reports Sandbox enabled, but WindowsSandbox.exe is missing. Restart Windows, then run Studio setup again. Windows servicing may need repair.'}
 $result.Success=$true
 if ($result.RestartNeeded) {
  $result.Message='Windows Sandbox is enabled. Save your work and restart Windows before opening Studio. This setup will not restart Windows automatically.'
  $code=3010
 } else {
  $result.Message='Windows Sandbox is ready. Studio can now open the isolated project.'
  $code=0
 }
} catch {
 $result.Message='Windows Sandbox setup failed: '+$_.Exception.Message+' Supported Windows Pro/Enterprise/Education and hardware virtualization are required. No project or sign-in data was changed.'
} finally {
 $result.CompletedUtc=(Get-Date).ToUniversalTime().ToString('o')
 try {
  $parent=Split-Path -Parent $ResultPath
  if (-not (Test-Path -LiteralPath $parent -PathType Container)) {throw 'Missing Studio setup folder.'}
  [IO.File]::WriteAllText($ResultPath,($result | ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
  # Keep restart diagnostics even if the owner closes the launcher during servicing.
  [IO.File]::WriteAllText((Join-Path $parent 'last-result.json'),($result | ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
 } catch {
  Add-Type -AssemblyName PresentationFramework
  [Windows.MessageBox]::Show($result.Message+"`nCannot write Studio setup status: "+$_.Exception.Message,'TermoDom Studio - Windows Sandbox') | Out-Null
  $code=1
 }
}
exit $code
