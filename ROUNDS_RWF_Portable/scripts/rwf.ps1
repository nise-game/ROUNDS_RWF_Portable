param([Parameter(Mandatory)][ValidateSet('install','play','uninstall')][string]$Action)
$ErrorActionPreference = 'Stop'
trap { Write-Host "エラー: $($_.Exception.Message)" -ForegroundColor Red; exit 1 }
$AppId = '1557740'
$Root = Split-Path -Parent $PSScriptRoot
$GameFiles = Join-Path $Root 'game_files'
$Preloader = Join-Path $Root 'BepInEx\core\BepInEx.Preloader.dll'

function Get-SteamPath {
    $p = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    if (-not $p) { throw 'Steam が見つかりません。Steam をインストールしてください。' }
    return ($p -replace '/', '\')
}

function Get-GameDir {
    $steam = Get-SteamPath
    $libs = @($steam)
    $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
    if (Test-Path $vdf) {
        $libs += Select-String -Path $vdf -Pattern '"path"\s+"(.+)"' |
            ForEach-Object { $_.Matches[0].Groups[1].Value -replace '\\\\', '\' }
    }
    foreach ($l in ($libs | Select-Object -Unique)) {
        $exe = Join-Path $l 'steamapps\common\ROUNDS\ROUNDS.exe'
        if (Test-Path $exe) { return (Split-Path -Parent $exe) }
    }
    throw 'ROUNDS のインストール先が見つかりません。Steam で ROUNDS をインストールしてください。'
}

function Test-SameFile($a, $b) {
    (Test-Path $a) -and (Test-Path $b) -and ((Get-FileHash $a).Hash -eq (Get-FileHash $b).Hash)
}

switch ($Action) {
    'install' {
        $game = Get-GameDir
        Write-Host "ROUNDS のフォルダ: $game"
        foreach ($f in 'winhttp.dll', 'doorstop_config.ini') {
            $dst = Join-Path $game $f
            $src = Join-Path $GameFiles $f
            if ((Test-Path $dst) -and -not (Test-SameFile $src $dst)) {
                $bak = "$dst.bak_" + (Get-Date -Format 'yyyyMMddHHmmss')
                Move-Item $dst $bak
                Write-Host "既存の $f を $bak に退避しました"
            }
            Copy-Item $src $dst -Force
        }
        Write-Host ''
        Write-Host '導入しました。play.bat で mod 入り ROUNDS が起動します。'
        Write-Host '(Steam から普通に起動した場合は今まで通り mod なしです)'
    }
    'play' {
        if (-not (Test-Path $Preloader)) { throw "BepInEx が見つかりません: $Preloader" }
        $game = Get-GameDir
        if (-not (Test-SameFile (Join-Path $GameFiles 'winhttp.dll') (Join-Path $game 'winhttp.dll'))) {
            throw '先に install.bat を実行してください。'
        }
        if ($Preloader -match '[^\x20-\x7e]') {
            Write-Host '注意: このフォルダのパスに日本語などが含まれています。mod が読み込まれない場合は、英数字だけのパス (例: C:\Games\ROUNDS_RWF_Portable) に移してください。' -ForegroundColor Yellow
        }
        $steamExe = Join-Path (Get-SteamPath) 'steam.exe'
        $launchArgs = "-applaunch $AppId --doorstop-enable true --doorstop-target `"$Preloader`""
        Write-Host 'mod 入り ROUNDS を起動します...'
        Start-Process -FilePath $steamExe -ArgumentList $launchArgs
    }
    'uninstall' {
        $game = Get-GameDir
        foreach ($f in 'winhttp.dll', 'doorstop_config.ini') {
            $dst = Join-Path $game $f
            if (Test-SameFile (Join-Path $GameFiles $f) $dst) {
                Remove-Item $dst
                Write-Host "$f を削除しました"
            } elseif (Test-Path $dst) {
                Write-Host "$f はこのパッケージ以外のもの (r2modman など) なので残しました"
            }
        }
        Write-Host 'ゲームフォルダから取り除きました。このフォルダごと削除すれば完全に消えます。'
    }
}
