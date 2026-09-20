[CmdletBinding()]
param(
    [string]$Destination,
    [switch]$CreateTarball
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$packageRoot = Join-Path $projectRoot 'Packages/LxyGame/LxyGame.UIPrefabGenerator'
$manifest = Get-Content -LiteralPath (Join-Path $packageRoot 'package.json') -Raw | ConvertFrom-Json
if ($manifest.name -ne 'com.lxy.ui-effect-generator') {
    throw 'Unexpected package manifest.'
}
if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $projectRoot "Build/UPMPackages/UIPrefabGenerator-$($manifest.version)"
}
$destinationPath = [IO.Path]::GetFullPath($Destination)
$archivePath = $destinationPath.TrimEnd([IO.Path]::DirectorySeparatorChar) + '.tgz'
if ($CreateTarball) {
    $tarCommand = Get-Command tar.exe -ErrorAction Stop
    if (Test-Path -LiteralPath $archivePath) { throw "Archive already exists: $archivePath" }
}
$sourcePath = [IO.Path]::GetFullPath($packageRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
if ($destinationPath.Equals($sourcePath, [StringComparison]::OrdinalIgnoreCase) -or
    $destinationPath.StartsWith($sourcePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Export destination must be outside the source package.'
}
if ((Test-Path -LiteralPath $destinationPath) -and
    (-not (Test-Path -LiteralPath $destinationPath -PathType Container) -or
     @(Get-ChildItem -LiteralPath $destinationPath -Force).Count -gt 0)) {
    throw 'Export destination must be a new or empty directory; existing files will not be overwritten.'
}

$dependencies = @($manifest.dependencies.PSObject.Properties.Name)
if (@($dependencies | Where-Object { $_ -notin @('com.unity.ugui', 'com.unity.textmeshpro',
        'com.unity.modules.imageconversion', 'com.unity.modules.jsonserialize') }).Count -gt 0) {
    throw 'The portable package has an unexpected dependency. Review it before exporting.'
}
$assembly = Get-Content -LiteralPath (Join-Path $packageRoot 'Editor/Lxy.UIEffectGenerator.Editor.asmdef') -Raw | ConvertFrom-Json
if (@($assembly.references | Where-Object { $_ -notin @('UnityEngine.UI', 'Unity.TextMeshPro') }).Count -gt 0 -or
    @($assembly.includePlatforms).Count -ne 1 -or $assembly.includePlatforms[0] -ne 'Editor') {
    throw 'The portable assembly must be Editor-only and reference only UGUI and TextMeshPro.'
}

# Explicit package contents only: no host adapter, gameplay, Assets, credentials or project settings.
$contents = @('Editor', 'Editor.meta', 'Documentation~', 'Samples~',
    'package.json', 'package.json.meta', 'README.md', 'README.md.meta',
    'CHANGELOG.md', 'CHANGELOG.md.meta')
foreach ($entry in $contents) {
    if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $entry))) {
        throw "Missing package entry: $entry"
    }
}
New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
foreach ($entry in $contents) {
    Copy-Item -LiteralPath (Join-Path $packageRoot $entry) -Destination $destinationPath -Recurse
}
[IO.File]::WriteAllText((Join-Path $destinationPath '.gitignore'), ".DS_Store`nThumbs.db`n.vs/`n.idea/`n")
if ($CreateTarball) {
    $archiveParent = [IO.Path]::GetFullPath((Split-Path -Parent $archivePath))
    $stagingPath = [IO.Path]::GetFullPath((Join-Path $archiveParent ('.upm-pack-' + [Guid]::NewGuid().ToString('N'))))
    if (-not $stagingPath.StartsWith($archiveParent.TrimEnd([IO.Path]::DirectorySeparatorChar) +
            [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Tarball staging must remain inside the export directory.'
    }
    New-Item -ItemType Directory -Path $stagingPath -ErrorAction Stop | Out-Null
    try {
        Copy-Item -LiteralPath $destinationPath -Destination (Join-Path $stagingPath 'package') -Recurse
        $temporaryArchive = Join-Path $stagingPath 'package.tgz'
        & $tarCommand.Source -czf $temporaryArchive -C $stagingPath package
        if ($LASTEXITCODE -ne 0) { throw "tar failed with exit code $LASTEXITCODE" }
        Move-Item -LiteralPath $temporaryArchive -Destination $archivePath -ErrorAction Stop
    }
    finally {
        # Only the unique staging directory validated above is owned by this export.
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }
    Write-Output "Created offline UPM package: $archivePath"
}
Write-Output "Exported $($manifest.displayName) $($manifest.version) to: $destinationPath"
Write-Output 'Place these contents at the root of a Git repository, commit and push, then install its .git URL in Package Manager.'
