$ErrorActionPreference = 'Stop'

$sourceDirectory = Join-Path $PSScriptRoot 'CalixtoOptimizer'
$sourceExecutable = Join-Path $sourceDirectory 'Optimizer.App.exe'
if (-not (Test-Path -LiteralPath $sourceExecutable -PathType Leaf)) {
    throw 'A pasta CalixtoOptimizer ao lado do instalador não contém Optimizer.App.exe.'
}

$localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
$programsDirectory = Join-Path $localAppData 'Programs'
$installDirectory = [IO.Path]::GetFullPath((Join-Path $programsDirectory 'CalixtoOptimizer'))
if (Test-Path -LiteralPath $installDirectory) {
    throw "A pasta de instalação já existe: $installDirectory. Remova a versão anterior antes de instalar."
}

$startMenuPrograms = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::StartMenu)) 'Programs'
$shortcutPath = Join-Path $startMenuPrograms 'Calixto Optimizer.lnk'
New-Item -ItemType Directory -Force -Path $programsDirectory, $startMenuPrograms | Out-Null
New-Item -ItemType Directory -Path $installDirectory | Out-Null

try {
    Copy-Item -Path (Join-Path $sourceDirectory '*') -Destination $installDirectory -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall-CalixtoOptimizer.ps1') -Destination (Join-Path $installDirectory 'Uninstall-CalixtoOptimizer.ps1') -Force

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = Join-Path $installDirectory 'Optimizer.App.exe'
    $shortcut.WorkingDirectory = $installDirectory
    $shortcut.IconLocation = Join-Path $installDirectory 'Optimizer.App.exe'
    $shortcut.Description = 'Calixto Optimizer — diagnóstico experimental'
    $shortcut.Save()
}
catch {
    if (Test-Path -LiteralPath $shortcutPath) {
        Remove-Item -LiteralPath $shortcutPath -Force
    }
    $expectedInstall = [IO.Path]::GetFullPath((Join-Path $programsDirectory 'CalixtoOptimizer'))
    if ([string]::Equals($installDirectory, $expectedInstall, [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $installDirectory)) {
        Remove-Item -LiteralPath $installDirectory -Recurse -Force
    }
    throw
}

Write-Host 'Instalação por usuário concluída.'
Write-Host "Aplicativo: $installDirectory"
Write-Host "Atalho: $shortcutPath"
Write-Host 'A instalação não iniciou o aplicativo; abra o atalho para entrar no diagnóstico.'
