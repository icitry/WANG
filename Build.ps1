[CmdletBinding()]
param(
    [switch]$NoPackage
)

$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT)
{
    throw 'Needs Windows with .NET 10 SKD installed to build WANG.'
}

$root = $PSScriptRoot
$project = Join-Path $root 'src\WANG\WANG.csproj'
$artifacts = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
Push-Location $root
try
{
    dotnet build $project -c Release --nologo
    if ($LASTEXITCODE -ne 0)
    {
        throw 'Build failed.'
    }

    if ($NoPackage)
    {
        return
    }

    [xml]$metadata = Get-Content -LiteralPath $project -Raw
    $version = [string]$metadata.Project.PropertyGroup.Version
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $bundle = Join-Path $artifacts "release-$stamp\WANG"
    $app = Join-Path $bundle 'app'
    New-Item -ItemType Directory -Path $app -Force | Out-Null
    dotnet publish $project -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o $app --nologo
    if ($LASTEXITCODE -ne 0)
    {
        throw 'Self-contained publish failed.'
    }

    Copy-Item -LiteralPath (Join-Path $root 'examples') -Destination (Join-Path $app 'examples') -Recurse
    Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination (Join-Path $app 'docs') -Recurse
    Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $app
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $app
    $installer = @'
[CmdletBinding()]
param(
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'Programs\WANG\__VERSION__'),
    [switch]$NoShortcuts
)

$ErrorActionPreference = 'Stop'
$payload = Join-Path $PSScriptRoot 'app'
if (-not (Test-Path -LiteralPath (Join-Path $payload 'WANG.exe')))
{
    throw 'Keep Install.ps1 beside the app folder from the release ZIP.'
}

$target = [IO.Path]::GetFullPath($InstallDirectory)
if (Test-Path -LiteralPath $target)
{
    throw "Destination already exists: $target. Choose a fresh folder or use the portable app."
}

New-Item -ItemType Directory -Path $target | Out-Null
Get-ChildItem -LiteralPath $payload -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $target -Recurse
}

if (-not $NoShortcuts)
{
    $menu = Join-Path ([Environment]::GetFolderPath('Programs')) 'WANG.lnk'
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($menu)
    $shortcut.TargetPath = Join-Path $target 'WANG.exe'
    $shortcut.WorkingDirectory = $target
    $shortcut.Save()
}

Write-Output "Installed WANG to $target"
Write-Output 'No administrator access or separate .NET installation is required.'
'@
    $installer.Replace('__VERSION__', $version) | Set-Content -LiteralPath (Join-Path $bundle 'Install.ps1') -Encoding UTF8
    $archive = Join-Path $artifacts "WANG-$version-win-x64.zip"
    Compress-Archive -LiteralPath $bundle -DestinationPath $archive -CompressionLevel Optimal -Force
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
    "$hash  $([IO.Path]::GetFileName($archive))" | Set-Content -LiteralPath ($archive + '.sha256') -Encoding ascii
    Write-Output "Release folder: $bundle"
    Write-Output "Release ZIP: $archive"
}
finally
{
    Pop-Location
}
