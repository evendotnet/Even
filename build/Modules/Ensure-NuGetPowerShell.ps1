[CmdletBinding()]
param()
function Import-NuGetPowershell {
    [CmdletBinding()]
    param()

    $installScriptUrl = "https://raw.githubusercontent.com/ligershark/nuget-powershell/master/get-nugetps.ps1"
    $online = Test-Connection  "google.com" -Count ($Retries + 1) -Quiet

    if($online){
        Write-Host "Acquiring NuGet-Powershell using $installScriptUrl"
        (new-object Net.WebClient).DownloadString($installScriptUrl) | iex
    }
    else
    {
        Write-Host "Attempting to use a local version of NuGet-PowerShell"
        $module = Get-Module -Name nuget-powershell -ListAvailable
        if($module -eq $null) {
            # Attempt to get it in known location
            $nugetPsPath = Join-Path (Join-Path $env:LOCALAPPDATA "LigerShark") "nuget-ps"
            if (Test-Path $nugetPsPath) {
                $tools = Join-Path $nugetPsPath "tools"
                $module = ls -Path $tools -Include nuget-powershell.psd1  -Recurse -Force
            }
        }

        Import-Module -Name $module
    }
}

$module = Get-Module -Name nuget-powershell
if($module -eq $null) {
    Write-Host "NuGet-Powershell has not been imported. Importing..."
    Import-NuGetPowershell
}