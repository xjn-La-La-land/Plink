# Builds Plink.exe with the .NET Framework C# compiler (no SDK required).
# Run make-icon.ps1 first so plink.ico exists.

$cscPath = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    Write-Host "ERROR: csc.exe not found at $cscPath"
    exit 1
}

$srcDir = $PSScriptRoot
$outExe = Join-Path $srcDir "Plink.exe"
$iconPath = Join-Path $srcDir "plink.ico"
if (-not (Test-Path $iconPath)) {
    Write-Host "ERROR: plink.ico not found. Run make-icon.ps1 first."
    exit 1
}

$sources = @(Get-ChildItem -Path $srcDir -Filter "*.cs" | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) {
    Write-Host "ERROR: no .cs source files found in $srcDir"
    exit 1
}

$cscArgs = @(
    "/nologo"
    "/target:winexe"
    "/optimize+"
    "/codepage:65001"
    "/win32icon:$iconPath"
    "/resource:$iconPath,plink.ico"
    "/win32manifest:$(Join-Path $srcDir 'app.manifest')"
    "/out:$outExe"
    "/reference:System.dll"
    "/reference:System.Drawing.dll"
    "/reference:System.Windows.Forms.dll"
) + $sources

& $cscPath $cscArgs
$code = $LASTEXITCODE

if ($code -eq 0) {
    Write-Host "BUILD OK -> $outExe"
} else {
    Write-Host "BUILD FAILED (exit $code)"
}
exit $code
