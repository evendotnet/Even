
function Invoke-EnsureNuGetPowerShell  {
    $bootstrapper = Join-Path $PSScriptRoot "Ensure-NuGetPowerShell.ps1"
    $result = . $bootstrapper
}

function Get-GitVersionCommandline {    
    Invoke-EnsureNuGetPowerShell 

    return (Join-Path (Get-NuGetPackage GitVersion.CommandLine -prerelease -binpath) 'GitVersion.exe')    
}

function Invoke-GitVersion {
    [CmdletBinding()]
    param(
        [string]$OutputPath,
        [switch]$UpdateAssemblyInfo,
        [string[]]$AssemblyInfoPaths,
        [string]$GitVersionExePath
    )

    if($GitVersionExePath -eq $null){
        $GitVersionExePath = Get-GitVersionCommandline
    }

    if ([String]::IsNullOrEmpty($OutputPath)) {
        $outPath = [System.IO.Path]::GetTempFileName()
        $OutputPath = [System.IO.Path]::ChangeExtension($outPath,".json")        
        Rename-Item $outPath $OutputPath
    }

    Write-Host "Writing VersionInfo to: $OutputPath"

    if($UpdateAssemblyInfo.IsPresent) {
        $cmdArgs = @("/updateassemblyinfo")
        $cmdArgs += $AssemblyInfoPaths
        & $GitVersionExePath $cmdArgs >> $OutputPath
        if ($LASTEXITCODE -ne 0){
            throw "GitVersion: An error occurred while running GitVersion"
        }
    } else {
        & $GitVersionExePath >> $OutputPath
        if ($LASTEXITCODE -ne 0){
            throw "GitVersion: An error occurred while running GitVersion"
        }
    }

    $versionInfoText = Get-Content $OutputPath -Raw
    $versionInfo = New-VersionInfo $versionInfoText
    ConvertTo-Json $versionInfo | Out-File $OutputPath
    $versionInfo 
}

function New-VersionInfo {
    [CmdletBinding()]
    param(
        [parameter(Position=0,Mandatory=$true)]
        [string]$VersionInfoJsonText
    )
    $versionInfo = ConvertFrom-Json $VersionInfoJsonText

    $prereleaseLabel = $versionInfo.PreReleaseLabel
	$prereleaseNumber = "{0:0000}" -f (($VersionInfo).PreReleaseNumber)
    $buildMeta = ($VersionInfo).BuildMetadataPadded
    $versionSuffix = "{0}{1}-{2}" -f $prereleaseLabel, $prereleaseNumber, $buildMeta
    $versionInfo | Add-Member -type NoteProperty -name VersionSuffix -value $versionSuffix
    Write-Host "VINFO[2]: $versionInfo"
    if ($versionInfo.BranchName -ne "master"){        
        $packageVersion = "{0}-{1}" -f $versionInfo.MajorMinorPatch, $versionInfo.VersionSuffix
        $VersionInfo | Add-Member -type NoteProperty -name PackageVersion -value $packageVersion
    }else{
        $VersionInfo | Add-Member -type NoteProperty -name PackageVersion -value $versionInfo.MajorMinorPatch
    }

    $versionInfo
}