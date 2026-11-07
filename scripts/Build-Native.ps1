#Requires -Version 7.0
param([string]$SdkRoot, [switch]$Research)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$pin=Get-Content (Join-Path $repoRoot 'native/upstream.json') -Raw | ConvertFrom-Json
$root=Join-Path $repoRoot '.cache/native-source'
if(!(Test-Path (Join-Path $root '.git'))){
 New-Item -ItemType Directory -Path $root -Force | Out-Null
 & git -C $root init --quiet
 & git -C $root remote add origin $pin.repository
}
& git -C $root fetch --depth 1 origin $pin.commit
if($LASTEXITCODE){throw 'Pinned native source fetch failed.'}
$actual=(& git -C $root rev-parse FETCH_HEAD).Trim()
if($actual -ne $pin.commit){throw 'Native commit mismatch.'}
# Only the disposable build cache is reset; no user checkout is changed.
& git -C $root checkout --detach --force $pin.commit
if($LASTEXITCODE){throw 'Native cache checkout failed.'}
& git -C $root apply --ignore-space-change (Join-Path $repoRoot 'native/phonedesk-stable.patch')
if($LASTEXITCODE){throw 'Stable native patch failed.'}
if($Research){
 & git -C $root apply --ignore-space-change (Join-Path $repoRoot 'research/diagnostics.patch')
 if($LASTEXITCODE){throw 'Research patch failed.'}
}
if(!$SdkRoot){
 Copy-Item (Join-Path $repoRoot 'native/bootstrap-upstream.ps1') (Join-Path $root 'scripts/build-airplay-helper.ps1') -Force
 & (Join-Path $root 'scripts/build-airplay-helper.ps1')
 $SdkRoot=Join-Path $root 'work/airplay-helper-build/sdk/ucrt64'
}
$SdkRoot=(Resolve-Path $SdkRoot).Path.Replace('\','/')
$build=Join-Path $root 'work/phonedesk-build'
$oldPath=$env:PATH
try{
 $env:PATH=$SdkRoot+'/bin;'+$oldPath
 $env:GCC_EXEC_PREFIX=$SdkRoot+'/lib/gcc/'
 $gccVersion=(Get-ChildItem ($SdkRoot+'/lib/gcc/x86_64-w64-mingw32') -Directory | Sort-Object Name -Descending | Select-Object -First 1).Name
 $env:LIBRARY_PATH=$SdkRoot+'/lib;'+$SdkRoot+'/lib/gcc/x86_64-w64-mingw32/'+$gccVersion
 & ($SdkRoot+'/bin/cmake.exe') -S (Join-Path $root 'vendor/uxplay') -B $build -G Ninja -DCMAKE_BUILD_TYPE=Release -DUSE_MDNS=ON -DNO_X11_DEPS=ON -DNO_MARCH_NATIVE=ON ('-DCMAKE_C_COMPILER='+$SdkRoot+'/bin/gcc.exe') ('-DCMAKE_CXX_COMPILER='+$SdkRoot+'/bin/g++.exe') ('-DCMAKE_MAKE_PROGRAM='+$SdkRoot+'/bin/ninja.exe') ('-DPKG_CONFIG_EXECUTABLE='+$SdkRoot+'/bin/pkg-config.exe')
 if($LASTEXITCODE){throw 'Native configure failed. Use a short ASCII checkout and TEMP/TMP directory.'}
 $ninja=Join-Path $build 'build.ninja'
 $text=[IO.File]::ReadAllText($ninja).Replace('-isystem '+$SdkRoot+'/include','')
 [IO.File]::WriteAllText($ninja,$text)
 & ($SdkRoot+'/bin/cmake.exe') --build $build --parallel 4
 if($LASTEXITCODE){throw 'Native build failed.'}
 $out=Join-Path $repoRoot 'dist/PhoneDesk/resources/app/receiver'
 Copy-Item (Join-Path $build 'uxplay.exe') (Join-Path $out 'AirPlay/UxPlay-original.exe') -Force
 $licenses=Join-Path $out 'licenses/native'
 New-Item -ItemType Directory -Path $licenses -Force | Out-Null
 Compress-Archive -Path (Join-Path $root 'vendor/uxplay') -DestinationPath (Join-Path $licenses 'UxPlay-PhoneDesk-source.zip') -Force
}finally{$env:PATH=$oldPath}
