param (
    [string]$ScriptDirectory
)

Write-Output "Starting SQL script execution..."

# Ensure the script is running as Administrator
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "Please run this script as an Administrator." -ForegroundColor Red
    exit
}
Write-Output "Script is running as Administrator."

# Use the passed ScriptDirectory parameter or fallback to script directory
$scriptPath = if ($ScriptDirectory) { $ScriptDirectory } else { $PSScriptRoot }
Write-Host "Script path: $scriptPath"

# Check for config.ini file
$configFile = "$scriptPath\config.ini"
if (-not (Test-Path $configFile)) {
    Write-Host "Configuration file not found: $configFile" -ForegroundColor Red
    exit
}
Write-Output "Found configuration file: $configFile"

# Define SQL Server installer URL and local path
$sqlInstallerUrl = "https://go.microsoft.com/fwlink/p/?linkid=2215158&clcid=0x4009&culture=en-in&country=in" # SQL Server 2022 Developer Edition
$sqlInstallerExe = "$env:TEMP\SQLServer2022Developer.exe"

# Function to validate the downloaded file
function Validate-File {
    param (
        [string]$FilePath
    )
    if (-not (Test-Path $FilePath)) {
        return $false
    }
    $fileSize = (Get-Item $FilePath).Length
    return ($fileSize -gt 1MB) # Ensure the file is larger than 1MB
}

# Retry logic for downloading the installer
$maxRetries = 3
$retryCount = 0
while ($retryCount -lt $maxRetries) {
    try {
        Write-Host "Downloading SQL Server installer from $sqlInstallerUrl... (Attempt $($retryCount + 1))" -ForegroundColor Cyan
        Invoke-WebRequest -Uri $sqlInstallerUrl -OutFile $sqlInstallerExe

        if (Validate-File -FilePath $sqlInstallerExe) {
            Write-Host "SQL Server installer downloaded successfully." -ForegroundColor Green
            break
        } else {
            Write-Host "Downloaded file is invalid. Retrying..." -ForegroundColor Yellow
        }
    } catch {
        Write-Host "Failed to download SQL Server installer. Error: $_" -ForegroundColor Red
    }

    $retryCount++
    if ($retryCount -ge $maxRetries) {
        Write-Host "Maximum retries reached. Exiting script." -ForegroundColor Red
        exit
    }
}

# Install SQL Server using the downloaded installer
Write-Host "Installing Microsoft SQL Server 2022 Developer..." -ForegroundColor Green

# Construct the installation command
$installCommand = "`"$sqlInstallerExe`" /ConfigurationFile=`"$configFile`" /IACCEPTSQLSERVERLICENSETERMS /Q"

Write-Host "Running command: $installCommand" -ForegroundColor Yellow

try {
    Write-Host "Executing SQL Server installation..." -ForegroundColor Cyan
    $process = Start-Process -FilePath $sqlInstallerExe -ArgumentList "/ConfigurationFile=`"$configFile`" /IACCEPTSQLSERVERLICENSETERMS /Q" -Wait -PassThru

    if ($process.ExitCode -ne 0) {
        Write-Host "SQL Server installation failed with exit code: $($process.ExitCode)" -ForegroundColor Red
        exit
    }

    Write-Host "SQL Server installation completed successfully." -ForegroundColor Green
} catch {
    Write-Host "SQL Server installation failed. Error: $_" -ForegroundColor Red
    exit
}

Write-Host "All components have been installed/configured. You can now sit back and relax!" -ForegroundColor Magenta