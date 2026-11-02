param([switch]$Test)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Le compilateur .NET Framework est introuvable.' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$common = @('/nologo', '/optimize+', '/warn:4', '/warnaserror+', '/codepage:65001', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Xml.dll')
$outputDir = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
& $compiler @common '/target:winexe' '/platform:anycpu' ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')) ('/win32icon:' + (Join-Path $PSScriptRoot 'assets\bl4cktap.ico')) ('/out:' + (Join-Path $outputDir 'Bl4ckTap.exe')) @sources
if ($LASTEXITCODE -ne 0) { throw 'La compilation a echoue.' }
Write-Output 'bin\Bl4ckTap.exe compile.'
if ($Test) {
    $testDir = Join-Path $PSScriptRoot 'tests'
    $testOutputDir = Join-Path $outputDir 'tests'
    New-Item -ItemType Directory -Path $testOutputDir -Force | Out-Null
    & $compiler @common '/target:exe' '/platform:anycpu' '/main:Bl4ckTapApp.Tests' ('/out:' + (Join-Path $testOutputDir 'Bl4ckTap.Tests.exe')) @sources (Join-Path $testDir 'Tests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'La compilation des tests a echoue.' }
    & (Join-Path $testOutputDir 'Bl4ckTap.Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Les tests ont echoue.' }
    & $compiler @common '/target:winexe' ('/out:' + (Join-Path $testOutputDir 'MouseTestPad.exe')) (Join-Path $testDir 'MouseTestPad.cs')
    if ($LASTEXITCODE -ne 0) { throw 'La compilation de la zone de test a echoue.' }
    & $compiler @common '/target:winexe' '/main:Bl4ckTapApp.NativeBench' ('/out:' + (Join-Path $testOutputDir 'NativeBench.exe')) @sources (Join-Path $testDir 'NativeBench.cs')
    if ($LASTEXITCODE -ne 0) { throw 'La compilation du test natif a echoue.' }
}
