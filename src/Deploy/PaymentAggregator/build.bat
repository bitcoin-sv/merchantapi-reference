@ECHO OFF

SET /p VERSIONPREFIX=<version_aggregator.txt

git remote update
git pull
git status -uno

FOR /F %%i IN ('git rev-parse --short HEAD') DO SET COMMITID=%%i

SET APPVERSIONMAPI=%VERSIONPREFIX%-%COMMITID%

ECHO *******************************
ECHO *******************************
ECHO Building docker image for MerchantPaymentAggregator version %APPVERSIONMAPI%
ECHO Continue if you have latest version (commit %COMMITID%) or terminate job and get latest files.

PAUSE

if not exist "Build" mkdir "Build"

SETLOCAL ENABLEDELAYEDEXPANSION
(
  for /f "delims=" %%A in (template-docker-compose.yml) do (
    set "line=%%A"
	set "line=!line:{{VERSION}}=%VERSIONPREFIX%!"
    echo(!line!
  )
)>Build/docker-compose.yml

copy /y template.env Build\.env

docker build  --build-arg APPVERSION=%APPVERSIONMAPI% -t bitcoinsv/aggregator:%VERSIONPREFIX% --file ../../MerchantAPI/PaymentAggregator/PaymentAggregator.Rest/Dockerfile ../..

docker save bitcoinsv/aggregator -o Build/merchantaggregatorapp.tar