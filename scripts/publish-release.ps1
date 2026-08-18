[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "== $Name =="
    & $Action
}

function Get-ProjectVersion {
    param([Parameter(Mandatory = $true)][string]$ProjectPath)

    [xml]$project = Get-Content -LiteralPath $ProjectPath
    $propertyGroup = $project.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1
    if ($null -eq $propertyGroup -or [string]::IsNullOrWhiteSpace($propertyGroup.Version)) {
        throw "Version was not found in $ProjectPath"
    }

    return [string]$propertyGroup.Version
}

function Add-Checksum {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [System.Collections.Generic.List[string]]$Lines
    )

    $hash = Get-FileHash -LiteralPath $Path -Algorithm SHA256
    $Lines.Add(("SHA256  {0}  {1}" -f $hash.Hash, (Split-Path -Leaf $Path)))
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$projectPath = Join-Path $repoRoot "src\PokeTokenBar\PokeTokenBar.csproj"
$version = Get-ProjectVersion -ProjectPath $projectPath

$artifactRoot = Join-Path $repoRoot "artifacts\v$version"
$publishDir = Join-Path $artifactRoot "publish"
$portableDir = Join-Path $artifactRoot "portable"
$installerDir = Join-Path $artifactRoot "installer"
$zipPath = Join-Path $artifactRoot ("PokeTokenBar-Windows-v{0}-{1}.zip" -f $version, $RuntimeIdentifier)
$checksumPath = Join-Path $artifactRoot "checksums.txt"
$installerPath = Join-Path $installerDir ("PokeTokenBar-Windows-Setup-v{0}.exe" -f $version)

Invoke-Step "Clean artifact directory" {
    if (Test-Path -LiteralPath $artifactRoot) {
        Remove-Item -LiteralPath $artifactRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $publishDir, $portableDir, $installerDir -Force | Out-Null
}

Invoke-Step "Restore" {
    dotnet restore (Join-Path $repoRoot "PokeTokenBar.Windows.sln")
}

Invoke-Step "Build" {
    dotnet build (Join-Path $repoRoot "PokeTokenBar.Windows.sln") -c $Configuration --no-restore
}

Invoke-Step "Test" {
    dotnet test (Join-Path $repoRoot "PokeTokenBar.Windows.sln") -c $Configuration --no-build
}

Invoke-Step "Publish self-contained portable app" {
    dotnet publish $projectPath `
        -c $Configuration `
        -r $RuntimeIdentifier `
        --self-contained true `
        -o $publishDir `
        /p:PublishSingleFile=false `
        /p:PublishTrimmed=false
}

Invoke-Step "Stage portable files" {
    Copy-Item -Path (Join-Path $publishDir "*") -Destination $portableDir -Recurse -Force

    foreach ($doc in @("README.md", "LICENSE", "docs\RELEASE_NOTES_0.1.0.md")) {
        $source = Join-Path $repoRoot $doc
        if (Test-Path -LiteralPath $source) {
            Copy-Item -LiteralPath $source -Destination $portableDir -Force
        }
    }

    Get-ChildItem -LiteralPath $portableDir -Filter "*.pdb" -Recurse | Remove-Item -Force
}

Invoke-Step "Create portable ZIP" {
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $portableDir "*") -DestinationPath $zipPath -Force
}

$checksumLines = [System.Collections.Generic.List[string]]::new()
Add-Checksum -Path $zipPath -Lines $checksumLines

Invoke-Step "Build installer if Inno Setup is available" {
    $candidatePaths = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles} "Inno Setup 6\ISCC.exe")
    )
    $candidates = @($candidatePaths | Where-Object { $_ -and (Test-Path -LiteralPath $_) })

    if ($candidates.Count -eq 0) {
        Write-Host "Inno Setup compiler not found. Installer script is ready, but installer was not built."
        return
    }

    $iscc = $candidates[0]
    & $iscc (Join-Path $repoRoot "installer\PokeTokenBar.iss")
    if (Test-Path -LiteralPath $installerPath) {
        Add-Checksum -Path $installerPath -Lines $checksumLines
    }
}

Invoke-Step "Write checksums" {
    $checksumLines | Set-Content -LiteralPath $checksumPath -Encoding UTF8
    Get-Content -LiteralPath $checksumPath
}

Invoke-Step "Smoke test published executable" {
    $exePath = Join-Path $portableDir "PokeTokenBar.exe"
    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Published executable was not found at $exePath"
    }

    $smokeCodexHome = Join-Path $artifactRoot "smoke-codex-home"
    try {
        New-Item -ItemType Directory -Path $smokeCodexHome -Force | Out-Null

        $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = $exePath
        $startInfo.WorkingDirectory = $portableDir
        $startInfo.UseShellExecute = $false
        $startInfo.Environment["CODEX_HOME"] = $smokeCodexHome

        $process = [System.Diagnostics.Process]::Start($startInfo)
        if ($null -eq $process) {
            throw "Failed to start published executable."
        }

        Start-Sleep -Seconds 5
        if ($process.HasExited) {
            throw "Published executable exited during smoke test with code $($process.ExitCode)."
        }

        $null = $process.CloseMainWindow()
        Start-Sleep -Seconds 2
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }

        Write-Host "Published executable started and remained alive for smoke test."
    }
    finally {
        if (Test-Path -LiteralPath $smokeCodexHome) {
            Remove-Item -LiteralPath $smokeCodexHome -Recurse -Force
        }
    }
}

Write-Host ""
Write-Host "Release artifacts:"
Write-Host "Portable folder: $portableDir"
Write-Host "Portable ZIP:    $zipPath"
if (Test-Path -LiteralPath $installerPath) {
    Write-Host "Installer:       $installerPath"
}
else {
    Write-Host "Installer:       not built"
}
Write-Host "Checksums:       $checksumPath"
