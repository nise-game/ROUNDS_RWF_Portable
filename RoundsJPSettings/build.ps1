# RoundsJPSettings をビルドして、ポータブル版の BepInEx\plugins に入れる
# 使い方: powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$portable = Join-Path $here '..\ROUNDS_RWF_Portable'
$bep = Join-Path $portable 'BepInEx'

$steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam').SteamPath -replace '/', '\'
$libs = @($steam)
$vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
if (Test-Path $vdf) { $libs += Select-String -Path $vdf -Pattern '"path"\s+"(.+)"' | ForEach-Object { $_.Matches[0].Groups[1].Value -replace '\\\\', '\' } }
$managed = $null
foreach ($l in ($libs | Select-Object -Unique)) { $p = Join-Path $l 'steamapps\common\ROUNDS\Rounds_Data\Managed'; if (Test-Path $p) { $managed = $p; break } }
if (-not $managed) { throw "ROUNDS not found" }

$refs = @(
    "$managed\mscorlib.dll", "$managed\System.dll", "$managed\System.Core.dll", "$managed\netstandard.dll",
    "$managed\Assembly-CSharp.dll", "$managed\UnityEngine.dll", "$managed\UnityEngine.CoreModule.dll",
    "$managed\UnityEngine.UI.dll", "$managed\UnityEngine.UIModule.dll", "$managed\UnityEngine.TextRenderingModule.dll",
    "$managed\Unity.TextMeshPro.dll", "$managed\PhotonUnityNetworking.dll", "$managed\PhotonRealtime.dll", "$managed\Photon3Unity3D.dll",
    "$bep\core\BepInEx.dll", "$bep\core\0Harmony.dll",
    "$bep\plugins\UnboundLib\UnboundLib.dll"
)
$out = Join-Path $here 'bin\RoundsJPSettings.dll'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$args = @('/nologo', '/codepage:65001', '/target:library', '/optimize+', '/nostdlib+', '/noconfig', "/out:$out") + ($refs | ForEach-Object { "/reference:$_" }) + @((Join-Path $here 'src\*.cs'))
& $csc @args
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)" }

$dest = Join-Path $bep 'plugins\RoundsJPSettings'
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item $out $dest -Force
Write-Host "Built and copied to $dest"
