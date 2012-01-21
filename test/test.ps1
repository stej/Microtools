param(
  [switch]$runOnly
)

$compilator = $(
  if (test-path 'C:\prgs\dev\FSharp-2.0.0.0\v4.0\bin\fsc.exe') { 
    'C:\prgs\dev\FSharp-2.0.0.0\v4.0\bin\fsc.exe' 
  } else { 
    'd:\prgs\dev\FSharp-2.0.0.0\v4.0\bin\fsc.exe' 
  })
  
$rootDir = Join-Path (split-path $MyInvocation.MyCommand.Path -parent) '..' | Resolve-Path
Write-Host "Root directory is $rootDir"
$res = "$rootDir\bin\MicroTest.dll"

mkdir $rootDir\bin\test -EA SilentlyContinue
if (!$runOnly) {
  & $compilator `
	"$rootDir\MicroCommon\Utils.fs" `
	"$rootDir\MicroCommon\Status.fs" `
	"$rootDir\MicroCommon\StatusFilter.fs" `
	"$rootDir\MicroCommon\StatusFunctions.fs" `
	"$rootDir\MicroCommon\OAuth.fs" `
	"$rootDir\MicroCommon\OAuthInterface.fs" `
	"$rootDir\MicroCommon\OAuthFunctions.fs" `
	"$rootDir\MicroCommon\StatusDbInterface.fs" `
	"$rootDir\MicroCommon\ShortenerDbInterface.fs" `
	"$rootDir\MicroCommon\TwitterLimits.fs" `
	"$rootDir\MicroCommon\TwitterStatusesChecker.fs" `
	"$rootDir\MicroCommon\Twitter.fs" `
	"$rootDir\MicroCommon\StatusesReplies.fs" `
	"$rootDir\MicroCommon\PreviewsState.fs" `
	"$rootDir\MicroCommon\UrlShortenerFunctions.fs" `
	"$rootDir\MicroData\DbCommon.fs" `
	"$rootDir\MicroData\StatusDb.fs" `
	"$rootDir\MicroData\UrlDb.fs" `
	"$rootDir\MicroUI\ImagesSource.fs" `
	"$rootDir\Twipy\Cmdline.fs" `
	"$rootDir\TwitterClient\UIState.fs" `
	"$rootDir\TwitterClient\SubscriptionsConfig.fs" `
	"$rootDir\test\FsUnit.fs" `
	"$rootDir\test\test.test.fs" `
	"$rootDir\test\test.xmlUtil.fs" `
	"$rootDir\test\test.testdbUtils.fs" `
	"$rootDir\test\test.statusParsing.fs" `
	"$rootDir\test\test.storeStatuses.fs" `
	"$rootDir\test\test.previewState.fs" `
	"$rootDir\test\test.previewStateAccessingDb.fs" `
	"$rootDir\test\test.statusesChecker.fs" `
	"$rootDir\test\test.UIState.fs" `
	"$rootDir\test\test.urlShortening.fs" `
	"$rootDir\test\test.fparsec.fs" `
	"$rootDir\test\test.testShortenedUrlToDomain.fs" `
	"$rootDir\test\test.statusFilter.fs" `
	"$rootDir\test\test.subscriptionsConfig.fs" `
  --target:library --platform:x86 --out:$res `
  --reference:$rootDir\lib\DevDefined.OAuth.dll `
  --reference:$rootDir\lib\log4net.dll `
  --reference:$rootDir\lib\System.Data.SQLite.dll `
  --reference:$rootDir\bin\Monooptions.dll `
  --reference:$rootDir\lib\nunit\nunit.framework.dll `
  --reference:$rootDir\packages\FParsec.0.9.1\lib\net40\FParsec.dll `
  --reference:$rootDir\packages\FParsec.0.9.1\lib\net40\FParsecCS.dll `
  --reference:System.Runtime.Serialization `
  --reference:WindowsBase `
  --reference:System.Xml `
  --reference:System.Configuration `
  --debug
}

if ($? -or $runOnly) {
  copy-item $rootDir\test\testRetweet.xml $rootDir\bin
  copy-item $rootDir\test\testStatus.xml $rootDir\bin
  copy-item $rootDir\statuses.db $rootDir\bin\test.statuses.db
  copy-item $rootDir\test\app.config $rootDir\bin\MicroTest.dll.config
  copy-item $rootDir\packages\FParsec.0.9.1\lib\net40\FParsec.dll $rootDir\bin
  copy-item $rootDir\packages\FParsec.0.9.1\lib\net40\FParsecCS.dll $rootDir\bin
  
  & $rootDir\lib\nunit\nunit-console-x86.exe $rootDir\bin\MicroTest.dll
  if (!$?) { Write-Error "Testing failed" }
  else     { Write-Host "Tests successfull" -fore Green }
}