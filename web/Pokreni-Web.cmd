@echo off
setlocal
where py 1>nul 2>nul
if not errorlevel 1 (
    py -3 -c "import sys; sys.exit(0 if sys.version_info >= (3, 10) else 1)" 1>nul 2>nul
    if not errorlevel 1 goto use_py
)
where python 1>nul 2>nul
if not errorlevel 1 (
    python -c "import sys; sys.exit(0 if sys.version_info >= (3, 10) else 1)" 1>nul 2>nul
    if not errorlevel 1 goto use_python
)
echo TermoDom Web trazi Python 3.10 ili noviji: https://www.python.org/downloads/
pause
exit /b 1

:use_py
py -3 -X utf8 "%~dp0run-local.py" %*
goto check_result

:use_python
python -X utf8 "%~dp0run-local.py" %*

:check_result
set "termodom_exit=%ERRORLEVEL%"
if not "%termodom_exit%"=="0" pause
exit /b %termodom_exit%
