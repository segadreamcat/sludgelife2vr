param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\SLUDGE LIFE 2')
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GamePath 'SludgeLife2_Data\Managed'
$core = Join-Path $GamePath 'BepInEx\core'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$references = @('mscorlib.dll','System.dll','System.Core.dll','Assembly-CSharp.dll','Rewired_Core.dll','PlayMaker.dll','TextMeshPro-2017.3-1.0.56-Runtime.dll') | ForEach-Object { '/reference:' + (Join-Path $managed $_) }
$references += Get-ChildItem -LiteralPath $managed -Filter 'UnityEngine*.dll' | ForEach-Object { '/reference:' + $_.FullName }
$references += @('BepInEx.dll','0Harmony.dll') | ForEach-Object { '/reference:' + (Join-Path $core $_) }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'src\VrPlugin.cs')) { $sources += Join-Path $PSScriptRoot 'lib\openvr_api.cs' }
New-Item -ItemType Directory -Force -Path (Join-Path $PSScriptRoot 'dist') | Out-Null
$output = Join-Path $PSScriptRoot 'dist\SludgeLife2VR.dll'
$tagGeometry = '/resource:' + (Join-Path $PSScriptRoot 'lib\tag-surfaces.bin') + ',VRTagSurfaces'
$suppliedCan = '/resource:' + (Join-Path $PSScriptRoot 'lib\supplied-can.bin') + ',VRSuppliedCan'
$mouseMethods = '/resource:' + (Join-Path $PSScriptRoot 'lib\mouse-methods.txt') + ',VRMouseMethods'
$gliderPose = '/resource:' + (Join-Path $PSScriptRoot 'lib\glider-pose.bin') + ',VRGliderPose'
$climbGeometry = '/resource:' + (Join-Path $PSScriptRoot 'lib\climb-surfaces.bin') + ',VRClimbSurfaces'
$drumKit = '/resource:' + (Join-Path $PSScriptRoot 'lib\drum-kit.bin') + ',VRDrumKit'
& $compiler /noconfig /nologo /target:library /platform:x64 /nostdlib+ /optimize+ "/out:$output" $tagGeometry $suppliedCan $climbGeometry $mouseMethods $gliderPose $drumKit $references $sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output "Built $output"
