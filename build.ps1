param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'
$game = [IO.Path]::GetFullPath($GameDir)
$managed = Join-Path $game 'Night in the Woods_Data\Managed'
$core = Join-Path $game 'BepInEx\core'
$required = @(
    (Join-Path $managed 'mscorlib.dll'),
    (Join-Path $managed 'Assembly-CSharp.dll'),
    (Join-Path $managed 'UnityEngine.dll'),
    (Join-Path $managed 'Rewired_Core.dll'),
    (Join-Path $core 'BepInEx.dll'),
    (Join-Path $core '0Harmony.dll')
)
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required reference not found: $path" }
}

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$system = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v2.0.50727\System.dll'
if (-not (Test-Path -LiteralPath $csc)) { throw 'The 64-bit .NET Framework C# compiler was not found.' }
if (-not (Test-Path -LiteralPath $system)) { throw 'The .NET 2.0 System.dll reference was not found.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$outputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$output = Join-Path $outputDirectory 'NightInTheWoodsCoop.dll'

$compilerArgs = @(
    '/nologo', '/noconfig', '/nostdlib+', '/nowin32manifest', '/target:library', '/platform:x64',
    "/out:$output",
    "/reference:$(Join-Path $managed 'mscorlib.dll')",
    "/reference:$system",
    "/reference:$(Join-Path $core 'BepInEx.dll')",
    "/reference:$(Join-Path $core '0Harmony.dll')",
    "/reference:$(Join-Path $managed 'Assembly-CSharp.dll')",
    "/reference:$(Join-Path $managed 'UnityEngine.dll')",
    "/reference:$(Join-Path $managed 'Rewired_Core.dll')",
    (Join-Path $PSScriptRoot 'src\NightInTheWoodsCoop.cs')
)
& $csc @compilerArgs
if ($LASTEXITCODE -ne 0) { throw "C# compilation failed with exit code $LASTEXITCODE" }
Write-Output "Built $output"

$patcherOutput = Join-Path $outputDirectory 'NightInTheWoodsSavePathPatcher.dll'
$patcherArgs = @(
    '/nologo', '/noconfig', '/nostdlib+', '/nowin32manifest', '/target:library', '/platform:x64',
    "/out:$patcherOutput",
    "/reference:$(Join-Path $managed 'mscorlib.dll')",
    "/reference:$(Join-Path $core 'Mono.Cecil.dll')",
    (Join-Path $PSScriptRoot 'src\SavePathPatcher.cs')
)
& $csc @patcherArgs
if ($LASTEXITCODE -ne 0) { throw "Save-path patcher compilation failed with exit code $LASTEXITCODE" }
Write-Output "Built $patcherOutput"

