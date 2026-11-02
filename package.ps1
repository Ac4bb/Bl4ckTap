$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$distDir = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $distDir -Force | Out-Null
$archivePath = Join-Path $distDir 'Bl4ckTap-source.zip'
# Explicit source list: never include local settings, build output or other archives.
$files = @(
    Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -File
    Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' -File
    foreach ($relative in @('assets/bl4cktap.ico','assets/icon-source.jpg','assets/make_icon.py',
        '.github/workflows/build.yml','.gitignore','app.manifest','build.ps1','package.ps1',
        'README.md','CONTRIBUTING.md','LICENSE')) {
        Get-Item -LiteralPath (Join-Path $PSScriptRoot $relative)
    }
)
$stream = [IO.File]::Open($archivePath, [IO.FileMode]::Create)
$archive = New-Object IO.Compression.ZipArchive($stream, ([IO.Compression.ZipArchiveMode]::Create))
try {
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($PSScriptRoot.Length + 1).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, ('Bl4ckTap/' + $relative)) | Out-Null
    }
} finally { $archive.Dispose(); $stream.Dispose() }
Write-Output ('Sources uniquement : ' + $archivePath)
