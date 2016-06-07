Param(
	[string]$task,
	[string]$buildNumber = 0)

if($task -eq $null) {
	$task = read-host "Enter Task"
}

$scriptPath = $(Split-Path -parent $MyInvocation.MyCommand.path)
Write-Host "ScriptPath: $scriptPath"
. .\build\psake.ps1 -buildFile default.ps1 -scriptPath $scriptPath -t $task -properties @{ build_number=$buildNumber }
