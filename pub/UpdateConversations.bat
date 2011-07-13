pushd %cd%
cd /d %~dp0
Twipy -file UpdateConversations.py -nogui
popd