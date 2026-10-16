param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'build'))
$ErrorActionPreference = 'Stop'
$taskBuildDirectory = [IO.Path]::GetFullPath($OutputDirectory)
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $taskBuildDirectory
$taskSourceRoot = Join-Path $PSScriptRoot 'src/CodexUsageViewer'
$taskFramework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$taskReferences = @('System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll','WPF/WindowsBase.dll','System.Xaml.dll') | ForEach-Object { '/r:' + (Join-Path $taskFramework $_) }
$taskSources = @('CodexUsageViewer','CodexUsageViewer.Usage','Properties') | ForEach-Object { Get-ChildItem -LiteralPath (Join-Path $taskSourceRoot $_) -Filter '*.cs' -Recurse } | ForEach-Object FullName
$taskTestExe = Join-Path $taskBuildDirectory 'UsageTests.exe'
$taskArguments = @('/nologo','/target:exe','/platform:x64','/langversion:12','/main:UsageTests',('/out:' + $taskTestExe))
& (Join-Path $taskBuildDirectory 'compiler/tasks/net472/csc.exe') @taskArguments @taskReferences @taskSources (Join-Path $PSScriptRoot 'tests/UsageTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
$taskTestData = Join-Path $taskBuildDirectory ('test-data/' + [Guid]::NewGuid().ToString('N'))
& $taskTestExe $taskTestData
if ($LASTEXITCODE -ne 0) { throw 'Accounting tests failed' }
