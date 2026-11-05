# Deliberately run only after the owner chooses the administrator setup button.
$ErrorActionPreference='Stop'
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {throw 'Run this script as administrator.'}
$result=Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -All -NoRestart
Add-Type -AssemblyName PresentationFramework
$message='Windows Sandbox je uključen.'
if($result.RestartNeeded){$message+=' Spremi rad i ponovno pokreni računalo, zatim otvori TermoDom Studio.'}
[Windows.MessageBox]::Show($message,'TermoDom Studio') | Out-Null
