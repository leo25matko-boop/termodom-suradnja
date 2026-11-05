# TermoDom development

Use this repository only. Do not open the owner's other projects, Windows profiles,
Codex sessions, auth stores, SSH keys or production connections.
Read docs/KONTEKST-ZA-CODEX.md for the shared application context and current limits.

Windows Debug and Android Debug are development builds with local fixture data.
Start Windows with `./scripts/team/Dev.ps1 -Action Run` and test with `-Action Test`.
Never send heating mutations, camera commands or personal logins while developing.
Never copy an owner's `.codex` directory or chat history to another Windows account.

Create `zaposlenici/<github-login>/<task>` in the contributor's fork. Commit only
reviewed source files, push to the contributor's fork, and open a pull request to
`leo25matko-boop/termodom-suradnja:mail`. Do not merge or approve your own PR.
The owner reviews and merges. Do not force push shared branches.

Keep tokens, signing keys, connection files, build products and personal paths out
of commits. Check `git diff --cached` before every commit. A .gitignore or these
instructions do not replace Windows permissions or GitHub branch protection.

The repository has native Android/Windows source. The older web/Electron app,
LDesign branches and private setup backups are intentionally not included.
