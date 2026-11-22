param(
    [string]$ExecutablePath,
    [switch]$Interface,
    [switch]$Audio
)
$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot '.artifacts'
New-Item -ItemType Directory -Path $output -Force | Out-Null
if (!$ExecutablePath) {
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    $dotnetPath = if ($dotnet) { $dotnet.Source } else { Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
    & $dotnetPath build (Join-Path $PSScriptRoot 'MiniDeck\MiniDeck.csproj') -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar para os testes.' }
    $ExecutablePath = Join-Path $PSScriptRoot 'MiniDeck\bin\Release\net8.0-windows\MiniDeck.exe'
}
$ExecutablePath = (Resolve-Path -LiteralPath $ExecutablePath).Path
function Invoke-MiniDeckCheck([string]$Flag, [string]$Destination, [string]$ResultFile) {
    # Quote the path for Start-Process: the repository may contain spaces.
    $quotedDestination = '"' + $Destination + '"'
    $process = Start-Process -FilePath $ExecutablePath -ArgumentList @($Flag,$quotedDestination) -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -Wait -PassThru
    if (!(Test-Path -LiteralPath $ResultFile)) { throw ('O teste não gerou resultado: ' + $Flag) }
    $result = Get-Content -LiteralPath $ResultFile -Raw | ConvertFrom-Json
    Write-Output ('{0}: {1} passou/passaram; {2} falhou/falharam.' -f $Flag,$result.Passed,$result.Failed)
    if ($process.ExitCode -ne 0 -or $result.Failed -ne 0) { throw ($result | ConvertTo-Json -Depth 10) }
}
$logic = Join-Path $output 'testes.json'
Invoke-MiniDeckCheck '--self-test' $logic $logic
if ($Interface) {
    $uiFolder = Join-Path $output 'interface'
    Invoke-MiniDeckCheck '--ui-smoke' $uiFolder (Join-Path $uiFolder 'resultado.json')
}
if ($Audio) {
    $audioFolder = Join-Path $output 'audio'
    Invoke-MiniDeckCheck '--sound-smoke' $audioFolder (Join-Path $audioFolder 'resultado.json')
}
