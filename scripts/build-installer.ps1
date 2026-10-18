[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')]
    [string] $ProductVersion = '1.0.0',
    [switch] $AcceptWixEula
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$solution = Join-Path $repoRoot 'Finder.slnx'
$appProject = Join-Path $repoRoot 'src\Finder.App\Finder.App.csproj'
$installerProject = Join-Path $repoRoot 'installer\Finder.Installer.wixproj'
$licensePath = Join-Path $repoRoot 'installer\license.rtf'
$iconPath = Join-Path $repoRoot 'src\Finder.App\Assets\Icons\Finder.ico'
$publishDir = Join-Path $repoRoot 'artifacts\publish\win-x64'
$installerDir = Join-Path $repoRoot 'artifacts\installer'
$payloadAuthoring = Join-Path $installerDir 'Payload.wxs'
$msiPath = Join-Path $installerDir 'FinderSetup.msi'

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]] $Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Escape-Xml([string] $Value) {
    return [System.Security.SecurityElement]::Escape($Value)
}

function Get-RelativePath([string] $BaseDirectory, [string] $TargetPath) {
    $baseFullPath = [System.IO.Path]::GetFullPath($BaseDirectory)
    $targetFullPath = [System.IO.Path]::GetFullPath($TargetPath)
    $baseRoot = [System.IO.Path]::GetPathRoot($baseFullPath)
    $targetRoot = [System.IO.Path]::GetPathRoot($targetFullPath)
    if (-not [string]::Equals($baseRoot, $targetRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Cannot make a relative path across different drives or shares: '$baseFullPath' and '$targetFullPath'."
    }

    $separator = [System.IO.Path]::DirectorySeparatorChar
    if (-not $baseFullPath.EndsWith([string]$separator) -and -not $baseFullPath.EndsWith([string][System.IO.Path]::AltDirectorySeparatorChar)) {
        $baseFullPath += $separator
    }
    $targetIsDirectory = [System.IO.Directory]::Exists($targetFullPath) -or
        $TargetPath.EndsWith([string][System.IO.Path]::DirectorySeparatorChar) -or
        $TargetPath.EndsWith([string][System.IO.Path]::AltDirectorySeparatorChar)
    if ($targetIsDirectory -and -not $targetFullPath.EndsWith([string]$separator) -and -not $targetFullPath.EndsWith([string][System.IO.Path]::AltDirectorySeparatorChar)) {
        $targetFullPath += $separator
    }

    $baseUri = [System.Uri]::new($baseFullPath, [System.UriKind]::Absolute)
    $targetUri = [System.Uri]::new($targetFullPath, [System.UriKind]::Absolute)
    $relativeUri = $baseUri.MakeRelativeUri($targetUri)
    if ($relativeUri.IsAbsoluteUri) {
        throw "Cannot make a relative path across different drives or shares: '$baseFullPath' and '$targetFullPath'."
    }

    return [System.Uri]::UnescapeDataString($relativeUri.ToString()).Replace('/', $separator)
}

function Get-StableId([string] $Prefix, [string] $Value) {
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $algorithm.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Value.ToLowerInvariant()))
    }
    finally {
        $algorithm.Dispose()
    }
    return $Prefix + ([BitConverter]::ToString($hash, 0, 12).Replace('-', ''))
}

function Get-StableGuid([string] $Value) {
    $algorithm = [System.Security.Cryptography.MD5]::Create()
    try {
        $hash = $algorithm.ComputeHash([System.Text.Encoding]::UTF8.GetBytes("Finder.Installer:$($Value.ToLowerInvariant())"))
    }
    finally {
        $algorithm.Dispose()
    }
    $hash[7] = ($hash[7] -band 0x0f) -bor 0x50
    $hash[8] = ($hash[8] -band 0x3f) -bor 0x80
    return ([Guid]::new($hash)).ToString('B').ToUpperInvariant()
}

function Write-PayloadAuthoring([string] $SourceRoot, [string] $Destination) {
    $files = @(Get-ChildItem -LiteralPath $SourceRoot -File -Recurse | Sort-Object FullName)
    if ($files.Count -eq 0) { throw "Publish output is empty: $SourceRoot" }

    $directoryIds = @{}
    $components = [System.Collections.Generic.List[string]]::new()
    $childrenByDirectory = @{}
    $rootKey = ''
    $childrenByDirectory[$rootKey] = [System.Collections.Generic.List[string]]::new()

    foreach ($file in $files) {
        $relative = (Get-RelativePath $SourceRoot $file.FullName).Replace('\', '/')
        $parts = $relative.Split('/')
        $parentKey = $rootKey
        for ($i = 0; $i -lt $parts.Length - 1; $i++) {
            $pathKey = if ($parentKey) { "$parentKey/$($parts[$i])" } else { $parts[$i] }
            if (-not $directoryIds.ContainsKey($pathKey)) {
                $directoryIds[$pathKey] = Get-StableId 'dir_' $pathKey
                $childrenByDirectory[$pathKey] = [System.Collections.Generic.List[string]]::new()
                $childrenByDirectory[$parentKey].Add($pathKey)
            }
            $parentKey = $pathKey
        }

        $componentId = Get-StableId 'cmp_' $relative
        $guid = Get-StableGuid $relative
        $source = Escape-Xml $file.FullName
        $fileId = Get-StableId 'fil_' $relative
        $valueName = Escape-Xml $componentId
        $componentXml = @"
      <Component Id="$componentId" Guid="$guid">
        <RegistryValue Root="HKCU" Key="Software\Finder\Installer\Components" Name="$valueName" Type="integer" Value="1" KeyPath="yes" />
        <File Id="$fileId" Source="$source" />
      </Component>
"@
        if (-not $childrenByDirectory.ContainsKey("component:$parentKey")) {
            $childrenByDirectory["component:$parentKey"] = [System.Collections.Generic.List[string]]::new()
        }
        $childrenByDirectory["component:$parentKey"].Add($componentXml.TrimEnd())
        $components.Add("      <ComponentRef Id=`"$componentId`" />")
    }

    function Write-DirectoryContents([string] $ParentKey, [int] $Depth) {
        $indent = '  ' * $Depth
        if ($ParentKey) {
            $directoryId = $directoryIds[$ParentKey]
            $cleanupComponentId = Get-StableId 'cmp_dirclean_' $ParentKey
            $cleanupFileId = Escape-Xml $cleanupComponentId
            $cleanupGuid = Get-StableGuid "directory:$ParentKey"
            $lines.Add("$indent<Component Id=`"$cleanupComponentId`" Guid=`"$cleanupGuid`">")
            $lines.Add("$indent  <RegistryValue Root=`"HKCU`" Key=`"Software\Finder\Installer\Directories`" Name=`"$cleanupFileId`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" />")
            $lines.Add("$indent  <RemoveFolder Id=`"$(Get-StableId 'rm_' $ParentKey)`" Directory=`"$directoryId`" On=`"uninstall`" />")
            $lines.Add("$indent</Component>")
            $components.Add("      <ComponentRef Id=`"$cleanupComponentId`" />")
        }
        foreach ($component in $childrenByDirectory["component:$ParentKey"]) {
            foreach ($line in $component -split "`r?`n") { $lines.Add($indent + $line) }
        }
        foreach ($childKey in $childrenByDirectory[$ParentKey]) {
            $leafName = $childKey.Substring($ParentKey.Length + $(if ($ParentKey) { 1 } else { 0 }))
            $leafName = $leafName.Split('/')[0]
            $directoryId = $directoryIds[$childKey]
            $lines.Add("$indent<Directory Id=`"$directoryId`" Name=`"$(Escape-Xml $leafName)`">")
            Write-DirectoryContents $childKey ($Depth + 1)
            $lines.Add("$indent</Directory>")
        }
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
    $lines.Add('  <Fragment>')
    $lines.Add('    <DirectoryRef Id="FinderInstallFolder">')
    Write-DirectoryContents $rootKey 3
    $lines.Add('    </DirectoryRef>')
    $lines.Add('    <ComponentGroup Id="FinderPayload">')
    foreach ($component in $components) { $lines.Add($component) }
    $lines.Add('    </ComponentGroup>')
    $lines.Add('  </Fragment>')
    $lines.Add('</Wix>')
    [IO.File]::WriteAllLines($Destination, $lines, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Authored $($files.Count) MSI payload components."
}

foreach ($required in @($solution, $appProject, $installerProject, $iconPath, $licensePath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required installer input is missing: $required" }
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'The .NET SDK is required but dotnet was not found on PATH.' }
if (-not $AcceptWixEula) {
    throw 'WiX 7 requires explicit EULA acceptance. Review https://wixtoolset.org/osmf/ and, if you agree to its terms, rerun with -AcceptWixEula.'
}

$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
foreach ($generatedPath in @($publishDir, $installerDir)) {
    $fullPath = [IO.Path]::GetFullPath($generatedPath)
    if (-not $fullPath.StartsWith($artifactsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clear a generated output path outside artifacts: $fullPath"
    }
}
if (Test-Path -LiteralPath $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force }
if (Test-Path -LiteralPath $msiPath) { Remove-Item -LiteralPath $msiPath -Force }

New-Item -ItemType Directory -Force -Path $publishDir, $installerDir | Out-Null
Write-Host 'Restoring solution and self-contained win-x64 runtime packs...'
Invoke-DotNet @('restore', $solution)
Invoke-DotNet @('restore', $appProject, '-r', 'win-x64', '-p:SelfContained=true')

Write-Host 'Building complete solution in Release mode...'
Invoke-DotNet @('build', $solution, '-c', 'Release', '--no-restore')

Write-Host 'Running existing Release test suite...'
Invoke-DotNet @('test', $solution, '-c', 'Release', '--no-build', '--no-restore')

Write-Host 'Publishing self-contained win-x64 application...'
Invoke-DotNet @('publish', $appProject, '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '--no-restore', '-o', $publishDir, '-p:PublishTrimmed=false', '-p:PublishAot=false', '-p:DebugSymbols=false', '-p:DebugType=None')

Write-PayloadAuthoring $publishDir $payloadAuthoring

Write-Host 'Building MSI...'
Invoke-DotNet @('build', $installerProject, '-c', 'Release', "-p:ProductVersion=$ProductVersion", '-p:AcceptEula=wix7', "-p:PayloadAuthoring=$payloadAuthoring", "-p:FinderIconPath=$iconPath", "-p:LicenseRtfPath=$licensePath", "-p:OutputPath=$installerDir", '-p:OutputName=FinderSetup', '-p:InstallerPlatform=x64')
if (-not (Test-Path -LiteralPath $msiPath -PathType Leaf)) { throw "WiX build succeeded but expected MSI was not found at $msiPath" }
$msi = Get-Item -LiteralPath $msiPath
if ($msi.Length -le 0) { throw "MSI output is empty: $msiPath" }
Write-Host "MSI built successfully: $($msi.FullName) ($($msi.Length) bytes; $([Math]::Round($msi.Length / 1MB, 2)) MB)"
