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
  # VSMB files keep the host owner's SID. Trust only this reviewed mapped repo
  # for this process tree; do not disable Git ownership checks for other paths.
  $env:GIT_CONFIG_COUNT='1'
  $env:GIT_CONFIG_KEY_0='safe.directory'
  $env:GIT_CONFIG_VALUE_0='C:/TermoDom/Project'
  $env:PATH="C:\TermoDom\App\Tools\git\cmd;C:\TermoDom\Data\Tools\dotnet;$env:PATH"
  foreach($name in 'OPENAI_API_KEY','OPENAI_BASE_URL','CHATGPT_API_KEY','GH_TOKEN','GITHUB_TOKEN','GIT_ASKPASS','SSH_AUTH_SOCK') {Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue}
  New-Item -ItemType Directory -Force $env:CODEX_HOME | Out-Null
  # Initialize only this app's isolated config. CLI arguments enforce the safety
  # settings on every start; user-added MCP endpoints can persist here.
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
  if (-not (Test-Path -LiteralPath "$env:CODEX_HOME\config.toml")) {
    [IO.File]::WriteAllText("$env:CODEX_HOME\config.toml",$studioConfig,[Text.UTF8Encoding]::new($false))
  }
  if (-not (Test-Path -LiteralPath C:\TermoDom\Project\.git)) {
    & C:\TermoDom\App\Tools\git\cmd\git.exe -C C:\TermoDom\Project init -b studio
    if($LASTEXITCODE){throw 'Cannot initialize isolated Git project.'}
  }
  & C:\TermoDom\App\Tools\git\cmd\git.exe -C C:\TermoDom\Project rev-parse --verify --quiet HEAD | Out-Null
  if ($LASTEXITCODE -ne 0) {
    # Bundle contains only the reviewed history of this single public project.
    # Mixed reset sets the initial index, leaving all existing source files intact.
    & C:\TermoDom\App\Tools\git\cmd\git.exe -C C:\TermoDom\Project fetch --no-tags C:\TermoDom\App\Project.bundle HEAD
    if($LASTEXITCODE){throw 'Cannot import reviewed project history.'}
    & C:\TermoDom\App\Tools\git\cmd\git.exe -C C:\TermoDom\Project reset --mixed FETCH_HEAD
    if($LASTEXITCODE){throw 'Cannot attach reviewed initial source history.'}
    # The archive deliberately omits this generic host/team policy from runtime.
    # Keep it in Git history without presenting its intentional absence as a deletion.
    & C:\TermoDom\App\Tools\git\cmd\git.exe -C C:\TermoDom\Project update-index --skip-worktree -- .codex/config.toml
    if($LASTEXITCODE){throw 'Cannot preserve the reviewed project policy in the initial index.'}
    $remotes=@(& C:\TermoDom\App\Tools\git\cmd\git.exe -C C:\TermoDom\Project remote)
    foreach($name in @('upstream','origin')) {
      if ($remotes -notcontains $name) {& C:\TermoDom\App\Tools\git\cmd\git.exe -C C:\TermoDom\Project remote add $name https://github.com/leo25matko-boop/termodom-suradnja.git}
    }
  }
  # A fresh Windows Sandbox may not contain any HTTPS handler. Prepare a signed
  # verified official browser in the guest before the app attempts OAuth.
  & C:\TermoDom\App\Prepare-Browser.ps1
  @{kind='TermoDom Windows Sandbox';started=(Get-Date).ToUniversalTime().ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath C:\TermoDom\worker-ready.json
  Start-Process -FilePath C:\TermoDom\App\TermoDomStudio.exe -ArgumentList '--inside' -WorkingDirectory C:\TermoDom\Project
} catch {
 Add-Type -AssemblyName PresentationFramework
 [Windows.MessageBox]::Show($_.Exception.Message,'TermoDom Studio — priprema nije uspjela') | Out-Null
 exit 1
}
