@ECHO OFF

SET /p VERSIONPREFIX=<version_mapi.txt
SET copy_env=/y template.env Build\.env
SET /A release_build=1

FOR %%A IN (%*) DO (
  IF "%%A"=="-d" ( 
    SET copy_env=/-y template.env Build\.env
    SET /A release_build=0
    goto BUILD
  )
)

git remote update
git pull
git status -uno

:BUILD
FOR /F %%i IN ('git rev-parse --short HEAD') DO SET COMMITID=%%i

SET APPVERSIONMAPI=%VERSIONPREFIX%-%COMMITID%

ECHO *******************************
ECHO *******************************
ECHO Building docker image for MerchantAPI version %APPVERSIONMAPI%

IF "%release_build%"=="1" (
  ECHO Continue if you have latest version (commit %COMMITID%^) or terminate job and get latest files.

  PAUSE  
)
IF NOT EXIST "Build" MKDIR "Build"

SETLOCAL ENABLEDELAYEDEXPANSION
(
  FOR /f "delims=" %%A IN (template-docker-compose.yml) DO (
    SET "line=%%A"
	SET "line=!line:{{VERSION}}=%VERSIONPREFIX%!"
    ECHO(!line!
  )
)>Build/docker-compose.yml

IF "%release_build%"=="0" (
  COPY template-docker-compose-dev.yml "Build/docker-compose-dev.yml"
)

COPY %copy_env%

docker build  --build-arg APPVERSION=%APPVERSIONMAPI% -t bitcoinsv/mapi:%VERSIONPREFIX% --file ..\MerchantAPI\APIGateway\APIGateway.Rest\Dockerfile ..

IF "%release_build%"=="1" docker save bitcoinsv/mapi -o Build/merchantapiapp.tar
