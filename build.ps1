param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'build'))
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$taskSourceRoot = Join-Path $PSScriptRoot 'src/CodexUsageViewer'
$taskBuildDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $taskBuildDirectory -Force | Out-Null
$taskCompilerRoot = Join-Path $taskBuildDirectory 'compiler'
$taskCompiler = Join-Path $taskCompilerRoot 'tasks/net472/csc.exe'
if (-not (Test-Path -LiteralPath $taskCompiler)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $taskZip = Join-Path $taskBuildDirectory 'compiler.zip'
    Invoke-WebRequest -Uri 'https://api.nuget.org/v3-flatcontainer/microsoft.net.compilers.toolset/4.8.0/microsoft.net.compilers.toolset.4.8.0.nupkg' -OutFile $taskZip
    if ((Get-FileHash -LiteralPath $taskZip -Algorithm SHA256).Hash -ne '37333F4F1E2CE55E621355D6DA651DC23D4CB5F94A8F76B9478816E87F110AD9') { throw 'Compiler package SHA256 verification failed' }
    Expand-Archive -LiteralPath $taskZip -DestinationPath $taskCompilerRoot -Force
}
$taskFramework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$taskReferences = @('System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll','WPF/WindowsBase.dll','System.Xaml.dll') | ForEach-Object { '/r:' + (Join-Path $taskFramework $_) }
$taskSources = @('CodexUsageViewer','CodexUsageViewer.Usage','Properties') | ForEach-Object { Get-ChildItem -LiteralPath (Join-Path $taskSourceRoot $_) -Filter '*.cs' -Recurse } | ForEach-Object FullName
$taskArguments = @('/nologo','/target:winexe','/platform:x64','/langversion:12',('/out:' + (Join-Path $taskBuildDirectory 'CodexUsageViewer.exe')),('/win32icon:' + (Join-Path $taskSourceRoot 'app.ico')),('/resource:' + (Join-Path $taskSourceRoot 'CodexUsageViewer.Resources.Strings.zh-CN.txt') + ',CodexUsageViewer.Resources.Strings.zh-CN.txt'),('/resource:' + (Join-Path $taskSourceRoot 'CodexUsageViewer.Resources.Strings.en-US.txt') + ',CodexUsageViewer.Resources.Strings.en-US.txt'))
& $taskCompiler @taskArguments @taskReferences @taskSources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
Write-Output (Join-Path $taskBuildDirectory 'CodexUsageViewer.exe')
