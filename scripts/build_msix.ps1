# Tapster MSIX Package Build Script (Windows 現代安裝包 / 微軟商店封裝)
$ErrorActionPreference = "Stop"
. "$PSScriptRoot/build_common.ps1"

$root = "$PSScriptRoot/.."
$packagingDir = "$root/packaging/msix"
$layoutDir = "$packagingDir/Layout"
$publishDir = "$root/publish"
$propsContent = [System.IO.File]::ReadAllText("$root/Directory.Build.props", [System.Text.Encoding]::UTF8)
$pkgVersion = if ($propsContent -match '<Version>(?<v>[\d\.]+)</Version>') { $Matches['v'] } else { "1.1.0.0" }
$msixPath = "$publishDir/Tapster-v$pkgVersion.msix"
$certPath = "$packagingDir/TapsterDev.pfx"

Add-WindowsSdkToolsToPath

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Building Tapster MSIX Package (v$pkgVersion)" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

try {
    # 1. Clean previous build & layout
    if (Test-Path $layoutDir) { Remove-Item -Recurse -Force $layoutDir }
    if (Test-Path $msixPath) { Remove-Item -Force $msixPath }
    if (-not (Test-Path $publishDir)) { New-Item -ItemType Directory -Path $publishDir | Out-Null }
    New-Item -ItemType Directory -Path $layoutDir | Out-Null

    # 2. Publish Tapster.Fluent
    Write-Host "[Build] Compiling Tapster.Fluent for MSIX (self-contained .NET runtime)..." -ForegroundColor Yellow
    $fluentBuildDir = "$root/Tapster.Fluent/bin/Release"
    $fluentObjDir = "$root/Tapster.Fluent/obj/Release"
    if (Test-Path $fluentBuildDir) { Remove-Item -Recurse -Force $fluentBuildDir }
    if (Test-Path $fluentObjDir) { Remove-Item -Recurse -Force $fluentObjDir }

    dotnet publish "$root/Tapster.Fluent/Tapster.Fluent.csproj" `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishTrimmed=true `
        -p:TrimMode=partial `
        -p:WindowsPackageType=MSIX `
        -p:WindowsAppSDKSelfContained=false `
        -p:WindowsAppSdkDeploymentManagerInitialize=false

    Assert-NativeSuccess

    # 3. Assemble Layout & Exclude Unused Heavy AI Dependencies
    Write-Host "[Layout] Assembling package files into $layoutDir..." -ForegroundColor Yellow
    Copy-Item "$packagingDir/AppxManifest.xml" "$layoutDir/AppxManifest.xml" -Force
    Sync-WindowsAppRuntimeDependency `
        -ProjectPath "$root/Tapster.Fluent/Tapster.Fluent.csproj" `
        -ManifestPath "$layoutDir/AppxManifest.xml"

    Copy-Item -Recurse "$packagingDir/Assets" "$layoutDir/" -Force
    if (Test-Path "$packagingDir/Strings") {
        Copy-Item -Recurse "$packagingDir/Strings" "$layoutDir/" -Force
    }

    $publishMatches = Get-ChildItem "$root/Tapster.Fluent/bin/Release/net8.0-windows*/win-x64/publish" -Directory -ErrorAction SilentlyContinue
    $fluentPublishSource = if ($publishMatches) { $publishMatches[0].FullName } else {
        (Get-ChildItem "$root/Tapster.Fluent/bin/Release/net8.0-windows*/win-x64" -Directory -ErrorAction SilentlyContinue)[0].FullName
    }

    $strictWhitelist = @(
        # App binaries & runtime configuration
        "Tapster.Fluent.exe",
        "Tapster.Fluent.dll",
        "Tapster.Core.dll",
        "Tapster.Fluent.deps.json",
        "Tapster.Fluent.runtimeconfig.json",
        "resources.pri",
        "app.ico",
        # CoreCLR Native Runtime Engine
        "coreclr.dll",
        "clrjit.dll",
        "clrgc.dll",
        "clretwrc.dll",
        "mscorrc.dll",
        "hostfxr.dll",
        "hostpolicy.dll",
        # WinUI 3 & WinRT Projections
        "Microsoft.WinUI.dll",
        "Microsoft.Windows.SDK.NET.dll",
        "WinRT.Runtime.dll",
        "Microsoft.WindowsAppRuntime.Bootstrap.dll",
        "Microsoft.WindowsAppRuntime.Bootstrap.Net.dll",
        "Microsoft.InteractiveExperiences.Projection.dll",
        "Microsoft.Windows.ApplicationModel.Resources.Projection.dll",
        "Microsoft.Web.WebView2.Core.Projection.dll",
        "Microsoft.Web.WebView2.Core.dll",
        "WebView2Loader.dll",
        # Managed Dependency Closure
        "System.Private.CoreLib.dll",
        "System.Runtime.dll",
        "System.Runtime.CompilerServices.Unsafe.dll",
        "System.Runtime.InteropServices.dll",
        "System.Runtime.Intrinsics.dll",
        "System.Runtime.Loader.dll",
        "System.Runtime.Numerics.dll",
        "System.Collections.dll",
        "System.Collections.Concurrent.dll",
        "System.Collections.Immutable.dll",
        "System.Collections.NonGeneric.dll",
        "System.Collections.Specialized.dll",
        "System.ComponentModel.dll",
        "System.ComponentModel.Primitives.dll",
        "System.Memory.dll",
        "System.Text.Json.dll",
        "System.Text.Encodings.Web.dll",
        "System.Text.Encoding.Extensions.dll",
        "System.Text.RegularExpressions.dll",
        "System.Threading.dll",
        "System.Threading.Thread.dll",
        "System.Threading.ThreadPool.dll",
        "System.Linq.dll",
        "System.Linq.Expressions.dll",
        "System.Numerics.Vectors.dll",
        "System.ObjectModel.dll",
        "System.Private.Uri.dll",
        "Microsoft.Win32.Registry.dll",
        "Microsoft.Win32.Primitives.dll",
        "System.Diagnostics.Process.dll",
        "System.Diagnostics.FileVersionInfo.dll",
        "System.Diagnostics.Tracing.dll",
        "System.Security.Cryptography.dll",
        "System.Security.AccessControl.dll",
        "System.Security.Claims.dll",
        "System.Security.Principal.Windows.dll",
        "System.Reflection.Metadata.dll",
        "System.Reflection.Emit.dll",
        "System.Reflection.Emit.ILGeneration.dll",
        "System.Reflection.Emit.Lightweight.dll",
        "System.Reflection.Primitives.dll",
        "System.IO.Compression.dll",
        "System.IO.Compression.Native.dll",
        "System.IO.MemoryMappedFiles.dll",
        "System.Net.Primitives.dll",
        "System.Console.dll",
        "System.Formats.Asn1.dll"
    )

    foreach ($item in $strictWhitelist) {
        $sourceFile = Join-Path $fluentPublishSource $item
        if (Test-Path $sourceFile) {
            Copy-Item $sourceFile "$layoutDir/" -Force
        }
    }

    $runtimeSource = "$fluentPublishSource/runtimes/win-x64/native"
    if (Test-Path $runtimeSource) {
        $runtimeTarget = "$layoutDir/runtimes/win-x64/native"
        New-Item -ItemType Directory -Path $runtimeTarget -Force | Out-Null
        Copy-Item "$runtimeSource/WebView2Loader.dll" "$runtimeTarget/" -ErrorAction SilentlyContinue
    }

    if (Test-Path "$root/Tapster.Fluent/app.ico") {
        Copy-Item "$root/Tapster.Fluent/app.ico" "$layoutDir/app.ico" -Force
    }

    # 4. Pack MSIX using makeappx.exe
    Write-Host "[Pack] Creating MSIX package with makeappx.exe..." -ForegroundColor Cyan
    & "makeappx.exe" pack /d "$layoutDir" /p $msixPath /o
    Assert-NativeSuccess

    # 5. Sign MSIX package using signtool.exe
    Invoke-SignMsixPackage `
        -ManifestPath "$packagingDir/AppxManifest.xml" `
        -MsixPath $msixPath `
        -CertPath $certPath

    Write-Host "==========================================" -ForegroundColor Green
    Write-Host " ✅ MSIX Package Complete!" -ForegroundColor Green
    Write-Host " MSIX Package: $msixPath" -ForegroundColor Gray
    Write-Host " Certificate:  $certPath" -ForegroundColor Gray
    Write-Host "==========================================" -ForegroundColor Green
}
finally {
    Stop-BuildServers
}
