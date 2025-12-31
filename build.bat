@echo off
echo Building OBS Live Now Indicator (Single File)...
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:EnableCompressionInSingleFile=true

echo.
echo Build complete! Single-file executable is located at:
echo ObsLiveNowIndicator\bin\Release\net8.0-windows\win-x64\publish\ObsLiveNowIndicator.exe
echo.
pause
