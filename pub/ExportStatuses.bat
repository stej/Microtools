pushd %cd%
cd /d %~dp0
Twipy -file ExportStatuses.py -nogui
popd