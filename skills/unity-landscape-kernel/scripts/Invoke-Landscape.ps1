[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [Parameter(Mandatory = $true)][string]$RequestPath,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath $ProjectPath).ProviderPath
$request = (Resolve-Path -LiteralPath $RequestPath).ProviderPath
if (-not (Test-Path -LiteralPath $project -PathType Container)) { throw 'ProjectPath must be a directory.' }
if (-not (Test-Path -LiteralPath $request -PathType Leaf)) { throw 'RequestPath must be a file.' }
$payload = Get-Content -LiteralPath $request -Raw -Encoding UTF8 | ConvertFrom-Json
if ($payload.schemaVersion -ne 1) { throw 'Only request schemaVersion 1 is supported.' }

$null = Get-Command unity -ErrorAction Stop
$output = [System.IO.Path]::GetFullPath($OutputDirectory)
$null = [System.IO.Directory]::CreateDirectory($output)
$runId = [Guid]::NewGuid().ToString('N')
$runDirectory = Join-Path $output $runId
$null = [System.IO.Directory]::CreateDirectory($runDirectory)
$helperPath = Join-Path $runDirectory 'LandscapeEntry.cs'
$stdoutPath = Join-Path $runDirectory 'cli.stdout.json'
$stderrPath = Join-Path $runDirectory 'cli.stderr.txt'

# Base64 contains no C# string delimiters; paths are decoded inside the entrypoint.
$encodedPath = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($request))
$className = 'LandscapeEntry_' + $runId
$source = @"
public static class $className
{
    public static object Run()
    {
        var path = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String("$encodedPath"));
        return RuinRelay.LandscapeKernel.Bridge.RunFile(path);
    }
}
"@
$utf8 = New-Object System.Text.UTF8Encoding($false)
$stream = [System.IO.File]::Open($helperPath, [System.IO.FileMode]::CreateNew)
try {
    $bytes = $utf8.GetBytes($source)
    $stream.Write($bytes, 0, $bytes.Length)
} finally { $stream.Dispose() }

$cliArguments = @('command', '--project-path', $project, 'script', 'run', '--file', $helperPath,
    '--entry', ($className + '.Run'), '--timeout', '180', '--format', 'json')
# Array arguments and the call operator avoid reparsing paths as shell code.
$global:LASTEXITCODE = 0
& unity @cliArguments 1> $stdoutPath 2> $stderrPath
$cliExitCode = $LASTEXITCODE
if ($cliExitCode -ne 0) {
    throw "Unity CLI failed with exit code $cliExitCode. Evidence: $runDirectory"
}
try { $result = Get-Content -LiteralPath $stdoutPath -Raw | ConvertFrom-Json }
catch { throw "Unity CLI did not return valid JSON. Evidence: $runDirectory. $($_.Exception.Message)" }
if (($result.PSObject.Properties.Name -notcontains 'success') -or $result.success -isnot [bool] -or -not $result.success) {
    throw "Unity CLI did not report success. Evidence: $runDirectory"
}
if (($result.PSObject.Properties.Name -notcontains 'result') -or $null -eq $result.result -or
    ($result.result.PSObject.Properties.Name -notcontains 'success') -or
    $result.result.success -isnot [bool] -or -not $result.result.success) {
    throw "Landscape Kernel did not report success. Evidence: $runDirectory"
}
[PSCustomObject]@{ success = $true; outputDirectory = $runDirectory; report = $result.result }
