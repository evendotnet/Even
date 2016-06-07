function Test-InternetConnection {
    [CmdletBinding()]
    param(
        [int]$Retries = 0
    )
    $EstablishedConnection = $false
    $result = Test-Connection  "google.com" -Count ($Retries + 1) -Quiet
    if ( -not $result) {
        Write-Host "Offline" -ForegroundColor Yellow
    }
    $result
}