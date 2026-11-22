param(
    [switch]$FrameworkDependent,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist')
)
$ErrorActionPreference = 'Stop'
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetPath = if ($dotnet) { $dotnet.Source } else { Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
if (!(Test-Path -LiteralPath $dotnetPath)) { throw 'Instale o SDK do .NET 8 para compilar.' }
$project = Join-Path $PSScriptRoot 'MiniDeck\MiniDeck.csproj'
$publishDirectory = Join-Path $OutputDirectory 'win-x64'
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
& $dotnetPath publish $project -c Release -r win-x64 --self-contained $selfContained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $publishDirectory --nologo
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar MiniDeck.' }
foreach ($file in @('README.md','LICENSE','THIRD-PARTY-NOTICES.md','CHANGELOG.md','CONTRIBUTING.md','SECURITY.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $publishDirectory -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs') -Destination $publishDirectory -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'licenses') -Destination $publishDirectory -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'examples') -Destination $publishDirectory -Recurse -Force
[xml]$projectXml = Get-Content -LiteralPath $project
$version = [string]$projectXml.Project.PropertyGroup.Version
$packageName = 'MiniDeck-PTBR-v' + $version + '-win-x64' + $(if ($FrameworkDependent) { '-framework-dependent' } else { '' }) + '.zip'
$archive = Join-Path $OutputDirectory $packageName
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $archive -Force
$digest = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($archive + '.sha256', $digest + '  ' + $packageName + [Environment]::NewLine)
Write-Output ('Executável: ' + (Join-Path $publishDirectory 'MiniDeck.exe'))
Write-Output ('Pacote: ' + $archive)
