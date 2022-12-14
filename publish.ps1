param(
    [Parameter(Mandatory)]
    [ValidateSet('Debug','Release')]
    [System.String]$Target,
    
    [Parameter(Mandatory)]
    [System.String]$TargetPath,
    
    [Parameter(Mandatory)]
    [System.String]$TargetAssembly,

    [Parameter(Mandatory)]
    [System.String]$ValheimPath,

    [Parameter(Mandatory)]
    [System.String]$ProjectPath,
    
    [System.String]$DeployPath
)

# Make sure Get-Location is the script path
Push-Location -Path (Split-Path -Parent $MyInvocation.MyCommand.Path)

# Test some preliminaries
("$TargetPath",
 "$ValheimPath",
 "$(Get-Location)\libraries"
) | % {
    if (!(Test-Path "$_")) {Write-Error -ErrorAction Stop -Message "$_ folder is missing"}
}

# Plugin name without ".dll"
$name = "$TargetAssembly" -Replace('.dll')
$preReleaseTag = ""

# Create the mdb file
$pdb = "$TargetPath\$name.pdb"
if (Test-Path -Path "$pdb") {
    Write-Host "Create mdb file for plugin $name"
    Invoke-Expression "& `"$(Get-Location)\libraries\Debug\pdb2mdb.exe`" `"$TargetPath\$TargetAssembly`""
}

# Main Script
Write-Host "Publishing for $Target from $TargetPath"

if ($Target.Equals("Debug")) {
    if ($DeployPath.Equals("")){
      $DeployPath = "$ValheimPath\BepInEx\plugins"
    }
    
    $plug = New-Item -Type Directory -Path "$DeployPath\$name" -Force
    New-Item -Type Directory -Path "$plug\Assets\Translations\" -Force
    
    Write-Host "Copy $TargetAssembly to $plug"
    Copy-Item -Path "$TargetPath\$name.dll" -Destination "$plug" -Force
    Copy-Item -Path "$TargetPath\$name.pdb" -Destination "$plug" -Force
    Copy-Item -Path "$TargetPath\$name.dll.mdb" -Destination "$plug" -Force
    
    Copy-Item -Recurse -Path "$ProjectPath\Assets\Translations\" -Destination "$plug\Assets\" -Force
}

if($Target.Equals("Release")) {
    Write-Host "Packaging for ThunderStore..."
    $Package="Package"
    $PackageName="PortalStations"
    $PackagePath="$ProjectPath\$Package"
    $Version=(Get-Command "$TargetPath\$TargetAssembly").FileVersionInfo.FileVersion.Replace(".", "_")
    $FullVersion="$Version$preReleaseTag"

    Write-Host "$PackagePath\$TargetAssembly"
    New-Item -Type Directory -Path "$PackagePath\plugins\$PackageName" -Force
    New-Item -Type Directory -Path "$PackagePath\plugins\$PackageName\Assets\Translations\" -Force
    New-Item -Type Directory -Path "$ProjectPath\..\dist\ThunderStore" -Force
    
    Copy-Item -Path "$TargetPath\$TargetAssembly" -Destination "$PackagePath\plugins\$PackageName\$TargetAssembly" -Force
    Copy-Item -Path "$ProjectPath\..\CHANGELOG.md" -Destination "$PackagePath\plugins\$PackageName\CHANGELOG.md" -Force
    Copy-Item -Path "$ProjectPath\..\LICENSE" -Destination "$PackagePath\plugins\$PackageName\LICENSE" -Force
    Copy-Item -Recurse -Path "$ProjectPath\Assets\Translations\" -Destination "$PackagePath\plugins\$PackageName\Assets\" -Force

    Copy-Item -Path "$ProjectPath\..\LICENSE" -Destination "$PackagePath\LICENSE" -Force
    Copy-Item -Path "$ProjectPath\..\CHANGELOG.md" -Destination "$PackagePath\CHANGELOG.md" -Force
    Copy-Item -Path "$ProjectPath\..\README.md" -Destination "$PackagePath\README.md" -Force
    
    Compress-Archive -Path "$PackagePath\*" -DestinationPath "$ProjectPath\..\dist\ThunderStore\Valheim_$($name)_v$FullVersion.zip" -Force
    Compress-Archive -Path "$PackagePath\plugins\*" -DestinationPath "$ProjectPath\..\dist\Valheim_$($name)_v$FullVersion.zip" -Force
}

# Pop Location
Pop-Location