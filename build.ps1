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
