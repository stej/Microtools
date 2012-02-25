param($file)
$rootDir = split-path $MyInvocation.MyCommand.Path -parent
ipmo sqlite
if (!$file) { $file = "$rootDir\statuses.db" }
new-psdrive -name tw -psp SQLite -root "Data Source=$file"

WRite-Host "Created drive tw:"
Write-Host "Example: dir tw:/Status -filter `"UserName = 'stejcz'`" | select username, Text"