param(
    [ValidateSet('Install','Launch','Uninstall')][string]$Action='Install',
    [string]$GamePath,
    [switch]$DryRun
)
$ErrorActionPreference='Stop'
$package=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'package.json') -Raw | ConvertFrom-Json
$payload=Join-Path $PSScriptRoot 'Payload'
function Contained([string]$root,[string]$relative) {
    $base=[IO.Path]::GetFullPath($root).TrimEnd('\')+'\'
    $target=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(-not $target.StartsWith($base,[StringComparison]::OrdinalIgnoreCase)){throw "Invalid package path: $relative"}
    return $target
}
function Find-Game {
    $roots=New-Object Collections.Generic.List[string]
    foreach($reg in @('HKCU:\Software\Valve\Steam','HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
        $item=Get-ItemProperty -LiteralPath $reg -ErrorAction SilentlyContinue
        if($null -ne $item){foreach($value in @($item.SteamPath,$item.InstallPath)){if($value){$roots.Add($value)}}}
    }
    if(${env:ProgramFiles(x86)}){$roots.Add((Join-Path ${env:ProgramFiles(x86)} 'Steam'))}
    foreach($steam in @($roots.ToArray())) {
        $libraries=Join-Path $steam 'steamapps\libraryfolders.vdf'
        if(Test-Path -LiteralPath $libraries){
            $text=Get-Content -LiteralPath $libraries -Raw
            foreach($match in [regex]::Matches($text,'"path"\s+"([^"]+)"')){$roots.Add($match.Groups[1].Value.Replace('\\','\'))}
        }
    }
    foreach($root in @($roots.ToArray() | Select-Object -Unique)) {
        $candidate=Join-Path $root ('steamapps\common\'+$package.gameFolder)
        if(Test-Path -LiteralPath (Join-Path $candidate $package.exe)){return $candidate}
        $acf=Join-Path $root ('steamapps\appmanifest_'+$package.appId+'.acf')
        if(Test-Path -LiteralPath $acf){
            $match=[regex]::Match((Get-Content -LiteralPath $acf -Raw),'"installdir"\s+"([^"]+)"')
            if($match.Success){
                $candidate=Join-Path $root ('steamapps\common\'+$match.Groups[1].Value)
                if(Test-Path -LiteralPath (Join-Path $candidate $package.exe)){return $candidate}
            }
        }
    }
    return $null
}
if(-not $GamePath){$GamePath=Find-Game}
if(-not $GamePath){$GamePath=(Read-Host ('Paste the '+$package.gameName+' installation folder (the one containing '+$package.exe+')')).Trim('"')}
$GamePath=(Resolve-Path -LiteralPath $GamePath).Path.TrimEnd('\')
if(-not (Test-Path -LiteralPath (Join-Path $GamePath $package.exe))){throw ('Wrong game folder. Expected '+$package.exe)}
$managed=Join-Path $GamePath ($package.dataFolder+'\Managed')
if(-not (Test-Path -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll'))){throw 'The game Managed folder is missing. Select the installed Windows game.'}
$manifestPath=Contained $GamePath ('BepInEx\'+$package.profile+'-tester-install.json')
$backupRelative='BepInEx\'+$package.profile+'-tester-backup'
$backupRoot=Contained $GamePath $backupRelative
$pluginRelative='BepInEx\plugins\'+$package.profile+'\'+$package.profile+'.dll'
$pluginPath=Contained $GamePath $pluginRelative
if($Action -eq 'Launch') {
    if(-not (Test-Path -LiteralPath $pluginPath)){throw 'Install the VR port first: run INSTALL.cmd.'}
    if(Get-Process -Name $package.process -ErrorAction SilentlyContinue){throw 'This game is already running. No process was stopped.'}
    $arguments='-force-d3d11 -vrmode openvr -screen-fullscreen 0 -screen-width 1280 -screen-height 720'
    if($DryRun){Write-Output ('Would launch '+(Join-Path $GamePath $package.exe)+' '+$arguments);return}
    Start-Process -FilePath (Join-Path $GamePath $package.exe) -WorkingDirectory $GamePath -ArgumentList $arguments -WindowStyle Normal
    return
}
if(Get-Process -Name $package.process -ErrorAction SilentlyContinue){throw 'Close the game before installing or removing the mod. No process was stopped.'}
if($Action -eq 'Uninstall') {
    if(-not (Test-Path -LiteralPath $manifestPath)){throw 'No tester-package install record exists in this game folder. Nothing was removed.'}
    $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if($manifest.profile -ne $package.profile){throw 'Installation record belongs to a different game.'}
    $others=@()
    foreach($relative in @('BepInEx\plugins','BepInEx\patchers')) {
        $directory=Contained $GamePath $relative
        if(Test-Path -LiteralPath $directory){$others+=@(Get-ChildItem -LiteralPath $directory -Recurse -File -Filter '*.dll' | Where-Object {$_.FullName -ne $pluginPath})}
    }
    $kept=New-Object Collections.ArrayList
    foreach($entry in @($manifest.files)) {
        $target=Contained $GamePath $entry.path
        $shared=$entry.path -like 'BepInEx\core\*' -or $entry.path -in @('winhttp.dll','doorstop_config.ini','.doorstop_version')
        if($shared -and $others.Count -gt 0){Write-Output ('Kept shared loader for other mods: '+$entry.path);[void]$kept.Add($entry);continue}
        if(Test-Path -LiteralPath $target){
            if((Get-FileHash -LiteralPath $target).Hash -ne $entry.installedSha256){Write-Output ('Kept file changed since installation: '+$entry.path);[void]$kept.Add($entry);continue}
        }
        if($entry.originalExisted){
            $backup=Contained $backupRoot $entry.path
            if(-not (Test-Path -LiteralPath $backup)){throw ('Missing original backup: '+$entry.path)}
            if($DryRun){Write-Output ('Would restore '+$entry.path);continue}
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
            Copy-Item -LiteralPath $backup -Destination $target
        } elseif(Test-Path -LiteralPath $target){
            if($DryRun){Write-Output ('Would remove '+$entry.path);continue}
            Remove-Item -LiteralPath $target
        }
    }
    if(-not $DryRun){
        $manifest.files=@($kept.ToArray());$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    }
    Write-Output 'VR port removed where files still matched the install record. Saves, screenshots, logs and configuration were kept. Original backups were retained.'
    return
}
# Verify every shipped file before touching the installation.
$catalog=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'payload-sha256.json') -Raw | ConvertFrom-Json
foreach($entry in @($catalog)) {
    $file=Contained $payload $entry.path
    if(-not (Test-Path -LiteralPath $file)){throw ('Package file missing: '+$entry.path)}
    if((Get-FileHash -LiteralPath $file).Hash -ne $entry.sha256){throw ('Package file failed verification: '+$entry.path)}
}
$manifest=[pscustomobject]@{profile=$package.profile;version=$package.version;files=@()}
if(Test-Path -LiteralPath $manifestPath){
    $manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if($manifest.profile -ne $package.profile){throw 'Installation record belongs to a different game.'}
}
$entries=New-Object Collections.ArrayList
foreach($entry in @($manifest.files)){[void]$entries.Add($entry)}
# Refuse an unrelated/incompatible loader before any writes; leave an existing working loader alone.
foreach($entry in @($catalog)) {
    if($entry.path -notlike 'BepInEx\core\*' -and $entry.path -notin @('winhttp.dll','doorstop_config.ini','.doorstop_version')){continue}
    $target=Contained $GamePath $entry.path
    if(Test-Path -LiteralPath $target){
        $hash=(Get-FileHash -LiteralPath $target).Hash
        $owned=$entries | Where-Object {$_.path -eq $entry.path -and $_.installedSha256 -eq $hash} | Select-Object -First 1
        if($hash -ne $entry.sha256 -and $null -eq $owned){throw ('An existing loader differs at '+$entry.path+'. Use a clean game/mod setup before testing; nothing was installed.')}
    }
}
$patched=$null
if($package.openvrPatch){
    $patch=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'openvr-settings-patch.json') -Raw | ConvertFrom-Json
    $settings=Contained $GamePath ($package.dataFolder+'\globalgamemanagers')
    $hash=(Get-FileHash -LiteralPath $settings).Hash
    if($hash -eq $patch.originalSha256){
        $original=[IO.File]::ReadAllBytes($settings);$stream=New-Object IO.MemoryStream;$position=0
        foreach($hunk in $patch.hunks){
            $stream.Write($original,$position,[int]$hunk.offset-$position)
            $insert=[Convert]::FromBase64String($hunk.insert);$stream.Write($insert,0,$insert.Length)
            $position=[int]$hunk.offset+[int]$hunk.remove
        }
        $stream.Write($original,$position,$original.Length-$position);$patched=$stream.ToArray();$stream.Dispose()
        $sha=[Security.Cryptography.SHA256]::Create()
        try{$newHash=[BitConverter]::ToString($sha.ComputeHash($patched)).Replace('-','')}finally{$sha.Dispose()}
        if($newHash -ne $patch.patchedSha256){throw 'OpenVR settings patch did not validate. Nothing was installed.'}
    } elseif($hash -ne $patch.patchedSha256){throw 'This Sludge Life build has different settings from the verified Steam build. Nothing was installed; send the version/build number with your report.'}
}
function Save-Record {
    $manifest.version=$package.version;$manifest.files=@($entries.ToArray())
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $manifestPath) | Out-Null
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
}
function Write-ModFile([string]$relative,[byte[]]$bytes) {
    $target=Contained $GamePath $relative
    if($DryRun){Write-Output ('Would install '+$relative);return}
    $entry=$entries | Where-Object {$_.path -eq $relative} | Select-Object -First 1
    if($null -eq $entry){
        $existed=Test-Path -LiteralPath $target
        if($existed){
            $backup=Contained $backupRoot $relative
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $backup) | Out-Null
            Copy-Item -LiteralPath $target -Destination $backup
        }
        $entry=[pscustomobject]@{path=$relative;originalExisted=$existed;installedSha256=''}
        [void]$entries.Add($entry)
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    [IO.File]::WriteAllBytes($target,$bytes);$entry.installedSha256=(Get-FileHash -LiteralPath $target).Hash
    Save-Record
}
foreach($entry in @($catalog)) {
    $target=Contained $GamePath $entry.path
    if(Test-Path -LiteralPath $target){
        if((Get-FileHash -LiteralPath $target).Hash -eq $entry.sha256){continue}
        # Keep a game's own OpenVR runtime if it already ships one.
        if($entry.path -like '*\Plugins\openvr_api.dll'){continue}
    }
    Write-ModFile $entry.path ([IO.File]::ReadAllBytes((Contained $payload $entry.path)))
}
if($null -ne $patched){Write-ModFile ($package.dataFolder+'\globalgamemanagers') $patched}
if(-not $DryRun){Save-Record}
Write-Output ('Installed '+$package.gameName+' VR '+$package.version+'. Connect your headset through SteamVR, then use LAUNCH-VR.cmd. No game was launched.')
