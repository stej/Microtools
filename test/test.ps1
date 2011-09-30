param(
  [switch]$runOnly
)

$compilator = $(
  if (test-path 'C:\prgs\dev\FSharp-2.0.0.0\v4.0\bin\fsc.exe') { 
    'C:\prgs\dev\FSharp-2.0.0.0\v4.0\bin\fsc.exe' 
  } else { 
    'd:\prgs\dev\FSharp-2.0.0.0\v4.0\bin\fsc.exe' 
  })
  
$res = "D:\backup\github-src\MicroTools\bin\MicroTest.dll"
if (!$runOnly) {
  & $compilator `
	"D:\backup\github-src\MicroTools\MicroCommon\Utils.fs" `
	"D:\backup\github-src\MicroTools\MicroCommon\Status.fs" `
	"D:\backup\github-src\MicroTools\MicroCommon\StatusFilter.fs" `
	"D:\backup\github-src\MicroTools\MicroCommon\StatusFunctions.fs" `
	"D:\backup\github-src\MicroTools\MicroCommon\OAuth.fs" `
	"D:\backup\github-src\MicroTools\MicroCommon\OAuthFunctions.fs" `
	"D:\backup\github-src\MicroTools\MicroCommon\DbFunctions.fs" `
	"D:\backup\github-src\MicroTools\MicroCommon\TwitterLimits.fs" `
	"D:\backup\github-src\MicroTools\MicroCommon\TwitterStatusesChecker.fs" `
	"D:\backup\github-src\MicroTools\MicroCommon\Twitter.fs" `
	"D:\backup\github-src\MicroTools\MicroCommon\StatusesReplies.fs" `
	"D:\backup\github-src\MicroTools\MicroCommon\PreviewsState.fs" `
	"D:\backup\github-src\MicroTools\MicroData\StatusDb.fs" `
	"D:\backup\github-src\MicroTools\MicroUI\ImagesSource.fs" `
	"D:\backup\github-src\MicroTools\Twipy\Cmdline.fs" `
	"d:\backup\github-src\MicroTools\test\FsUnit.fs" `
	"d:\backup\github-src\MicroTools\test\test.test.fs" `
	"d:\backup\github-src\MicroTools\test\test.xmlUtil.fs" `
	"d:\backup\github-src\MicroTools\test\test.statusParsing.fs" `
	"d:\backup\github-src\MicroTools\test\test.storeStatuses.fs" `
  --target:library --platform:x86 --out:$res `
  --reference:D:\backup\github-src\MicroTools\lib\DevDefined.OAuth.dll `
  --reference:D:\backup\github-src\MicroTools\lib\log4net.dll `
  --reference:D:\backup\github-src\MicroTools\lib\System.Data.SQLite.dll `
  --reference:D:\backup\github-src\MicroTools\bin\Monooptions.dll `
  --reference:d:\backup\github-src\MicroTools\lib\nunit\nunit.framework.dll `
  --reference:System.Runtime.Serialization `
  --reference:WindowsBase `
  --reference:System.Xml `
  --reference:System.Configuration `
  --debug
}

if ($? -or $runOnly) {
  copy-item d:\backup\github-src\MicroTools\test\testRetweet.xml d:\backup\github-src\MicroTools\bin\test
  copy-item d:\backup\github-src\MicroTools\test\testStatus.xml d:\backup\github-src\MicroTools\bin\test
  copy-item d:\backup\github-src\MicroTools\statuses.db d:\backup\github-src\MicroTools\bin\test
  
  d:\backup\github-src\MicroTools\lib\nunit\nunit-console-x86.exe d:\backup\github-src\MicroTools\bin\MicroTest.dll
}