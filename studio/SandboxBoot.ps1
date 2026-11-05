$ErrorActionPreference='Stop'
if ($env:USERNAME -ne 'WDAGUtilityAccount') { throw 'This bootstrap runs only inside Windows Sandbox.' }
if (-not (Test-Path -LiteralPath C:\TermoDom\Project) -or -not (Test-Path -LiteralPath C:\TermoDom\Data)) {throw 'Missing isolated folders.'}
try {
  # No owner environment, vault, browser profile, Git credentials or Codex home is mounted.
  $env:CODEX_HOME='C:\TermoDom\Data\Codex'
  $env:DOTNET_CLI_HOME='C:\TermoDom\Data\DotnetHome'
  $env:NUGET_PACKAGES='C:\TermoDom\Data\Nuget'
  $env:DOTNET_ROOT='C:\TermoDom\Data\Tools\dotnet'
  $env:GIT_CONFIG_GLOBAL='C:\TermoDom\Data\gitconfig'
  $env:GIT_CONFIG_NOSYSTEM='1'
  $env:PATH="C:\TermoDom\App\Tools\git\cmd;C:\TermoDom\Data\Tools\dotnet;$env:PATH"
  foreach($name in 'OPENAI_API_KEY','OPENAI_BASE_URL','CHATGPT_API_KEY','GH_TOKEN','GITHUB_TOKEN','GIT_ASKPASS','SSH_AUTH_SOCK') {Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue}
  New-Item -ItemType Directory -Force $env:CODEX_HOME | Out-Null
  # Ignore repository-local Codex config; this personal app owns its isolated configuration.
  $studioConfig=@'
cli_auth_credentials_store = "file"
forced_login_method = "chatgpt"
sandbox_mode = "danger-full-access"
approval_policy = "on-request"
[analytics]
enabled = false
[features]
apps = false
plugins = false
'@
  [IO.File]::WriteAllText("$env:CODEX_HOME\config.toml",$studioConfig,[Text.UTF8Encoding]::new($false))
  if (-not (Test-Path -LiteralPath C:\TermoDom\Project\.git)) {
    & C:\TermoDom\App\Tools\git\cmd\git.exe -C C:\TermoDom\Project init -b studio
    if($LASTEXITCODE){throw 'Cannot initialize isolated Git project.'}
    # Bundle contains only the reviewed history of this single public project.
    # Mixed reset sets the initial index, leaving all existing source files intact.
    & C:\TermoDom\App\Tools\git\cmd\git.exe -C C:\TermoDom\Project fetch --no-tags C:\TermoDom\App\Project.bundle HEAD
    if($LASTEXITCODE){throw 'Cannot import reviewed project history.'}
    & C:\TermoDom\App\Tools\git\cmd\git.exe -C C:\TermoDom\Project reset --mixed FETCH_HEAD
    if($LASTEXITCODE){throw 'Cannot attach reviewed initial source history.'}
    & C:\TermoDom\App\Tools\git\cmd\git.exe -C C:\TermoDom\Project remote add upstream https://github.com/leo25matko-boop/termodom-suradnja.git
    & C:\TermoDom\App\Tools\git\cmd\git.exe -C C:\TermoDom\Project remote add origin https://github.com/leo25matko-boop/termodom-suradnja.git
  }
  @{kind='TermoDom Windows Sandbox';started=(Get-Date).ToUniversalTime().ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath C:\TermoDom\worker-ready.json
  Start-Process -FilePath C:\TermoDom\App\TermoDomStudio.exe -ArgumentList '--inside' -WorkingDirectory C:\TermoDom\Project
} catch {
 Add-Type -AssemblyName PresentationFramework
 [Windows.MessageBox]::Show($_.Exception.Message,'TermoDom Studio — priprema nije uspjela') | Out-Null
 exit 1
}
