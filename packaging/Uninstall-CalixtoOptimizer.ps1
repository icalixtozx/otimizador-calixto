$ErrorActionPreference = 'Stop'

$localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$expectedDirectory = [IO.Path]::GetFullPath((Join-Path (Join-Path $localAppData 'Programs') 'CalixtoOptimizer'))
$actualDirectory = [IO.Path]::GetFullPath($PSScriptRoot)
if (-not [string]::Equals($actualDirectory, $expectedDirectory, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'O desinstalador só pode remover a pasta Calixto Optimizer em LocalAppData\Programs.'
}

if (Get-Process -Name 'Optimizer.App' -ErrorAction SilentlyContinue) {
    throw 'Feche o Calixto Optimizer antes de desinstalar.'
}

$startMenuPrograms = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::StartMenu)) 'Programs'
$shortcutPath = Join-Path $startMenuPrograms 'Calixto Optimizer.lnk'
if (Test-Path -LiteralPath $shortcutPath) {
    Remove-Item -LiteralPath $shortcutPath -Force
}

Remove-Item -LiteralPath $actualDirectory -Recurse -Force
Write-Host 'Calixto Optimizer removido. Nenhuma configuração do Windows precisa ser restaurada.'
