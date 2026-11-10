param([string]$Version = '0.1.0-preview')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^[A-Za-z0-9._-]+$') { throw 'Invalid version label.' }
$projectRoot = Split-Path $PSScriptRoot -Parent
$buildRoot = Join-Path $projectRoot 'Builds\Windows'
$importer = Join-Path $projectRoot 'Builds\Importer\CyanReferenceImporter.exe'
if (!(Test-Path -LiteralPath (Join-Path $buildRoot 'CyanLizor.exe'))) { throw 'Build the Windows player first.' }
if (!(Test-Path -LiteralPath $importer)) { throw 'Build the reference importer first.' }
$toolsRoot = Join-Path $buildRoot 'Tools'
New-Item -ItemType Directory -Force -Path $toolsRoot | Out-Null
Copy-Item -LiteralPath $importer -Destination $toolsRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $buildRoot 'README.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination (Join-Path $buildRoot 'LICENSE')
$notices = Join-Path $projectRoot 'Builds\ImporterNotices'
if (!(Test-Path -LiteralPath $notices)) { throw 'Generate importer notices with tools/collect_notices.py first.' }
$noticeDestination = Join-Path $buildRoot 'ThirdPartyNotices'
New-Item -ItemType Directory -Force -Path $noticeDestination | Out-Null
Get-ChildItem -LiteralPath $notices | Copy-Item -Destination $noticeDestination -Recurse -Force
$forbidden = Get-ChildItem -LiteralPath $buildRoot -Recurse | Where-Object {
    $_.Name -in @('Assembly-CSharp.dll', 'Assembly-CSharp-firstpass.dll', 'reference.json', 'local-data', 'decompiled') -or
    $_.Name -like 'Lizard*.png'
}
if ($forbidden) { throw ('Game reference files found in release: ' + ($forbidden.Name -join ', ')) }
$zip = Join-Path $projectRoot ('Builds\CyanLizor-' + $Version + '-win-x64.zip')
Compress-Archive -Path (Join-Path $buildRoot '*') -DestinationPath $zip -Force
Get-FileHash -LiteralPath $zip -Algorithm SHA256
