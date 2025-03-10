param (
    [Parameter(Mandatory=$true, HelpMessage="Path to the ConfigurationFile.ini")]
    [string]$ConfigFile,

    [Parameter(Mandatory=$true, HelpMessage="Path to the directory containing or receiving SQLEXPR_x64_ENU.exe")]
    [string]$SetupFilesDirectory
)

# Function to write verbose log messages with timestamps
function Write-Log {
    param (
        [string]$Message,
        [string]$Level = "INFO"
    )
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $color = switch ($Level) {
        "INFO" { "White" }
        "WARNING" { "Yellow" }
        "ERROR" { "Red" }
    }
    Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
}

# Start the script execution
Write-Log "Starting SQL Server installation script..."

# Ensure the script is running as Administrator
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Log "This script must be run as an Administrator. Please restart with elevated privileges." "ERROR"
    exit 1
}
Write-Log "Script is running with Administrator privileges."

# Construct paths

$sqlInstallerExe = Join-Path -Path $SetupFilesDirectory -ChildPath "SQLEXPR_x64_ENU.exe"
$downloadUrl = "https://download.microsoft.com/download/5/1/4/5145fe04-4d30-4b85-b0d1-39533663a2f1/SQL2022-SSEI-Expr.exe"

# Validate the configuration file exists
if (-not (Test-Path $configFile)) {
    Write-Log "Configuration file not found at: $configFile" "ERROR"
    exit 1
}
Write-Log "Configuration file located at: $configFile"

# Function to validate the downloaded file size
function Test-FileValidity {
    param (
        [string]$FilePath
    )
    if (-not (Test-Path $FilePath)) {
        return $false
    }
    $fileSize = (Get-Item $FilePath).Length
    return ($fileSize -gt 1MB) # Basic validation: file should be larger than 1MB
}

# Handle SQL Server installer
if (-not (Test-Path $sqlInstallerExe)) {
    Write-Log "SQL installer not found at: $sqlInstallerExe. Initiating download..."
    
    # Ensure the setup directory exists
    if (-not (Test-Path $SetupFilesDirectory)) {
        New-Item -Path $SetupFilesDirectory -ItemType Directory -Force | Out-Null
        Write-Log "Created directory for setup files: $SetupFilesDirectory"
    }

    # Retry logic for downloading the installer
    $maxRetries = 3
    $retryCount = 0
    while ($retryCount -lt $maxRetries) {
        try {
            Write-Log "Downloading SQL Server installer from $downloadUrl (Attempt $($retryCount + 1) of $maxRetries)..."
            Invoke-WebRequest -Uri $downloadUrl -OutFile $sqlInstallerExe -ErrorAction Stop

            if (Test-FileValidity -FilePath $sqlInstallerExe) {
                Write-Log "SQL Server installer downloaded successfully to: $sqlInstallerExe"
                break
            } else {
                Write-Log "Downloaded file appears invalid (size too small). Retrying..." "WARNING"
                Remove-Item $sqlInstallerExe -Force -ErrorAction SilentlyContinue
            }
        } catch {
            Write-Log "Download failed. Error: $_" "ERROR"
        }

        $retryCount++
        if ($retryCount -ge $maxRetries) {
            Write-Log "Failed to download SQL Server installer after $maxRetries attempts. Exiting." "ERROR"
            exit 1
        }
        Start-Sleep -Seconds 5 # Brief delay before retrying
    }
} else {
    Write-Log "Using existing SQL Server installer found at: $sqlInstallerExe"
}

# Install SQL Server
Write-Log "Starting Microsoft SQL Server 2022 Express installation..."

$installArgs = "/ConfigurationFile=`"$configFile`" /IACCEPTSQLSERVERLICENSETERMS /Q"
Write-Log "Installation command: $sqlInstallerExe $installArgs"

try {
    Write-Log "Executing SQL Server installation..."
    $process = Start-Process -FilePath $sqlInstallerExe -ArgumentList $installArgs -Wait -PassThru -NoNewWindow -ErrorAction Stop

    if ($process.ExitCode -ne 0) {
        Write-Log "SQL Server installation failed with exit code: $($process.ExitCode)" "ERROR"
        exit 1
    }
    Write-Log "SQL Server installation completed successfully."
} catch {
    Write-Log "Installation failed. Error: $_" "ERROR"
    exit 1
}

# Cleanup: Remove the installer if it was downloaded
if (Test-Path $sqlInstallerExe) {
    try {
        Remove-Item $sqlInstallerExe -Force -ErrorAction Stop
        Write-Log "Cleaned up downloaded installer: $sqlInstallerExe"
    } catch {
        Write-Log "Failed to clean up installer. Error: $_" "WARNING"
    }
}

Write-Log "SQL Server installation and configuration completed successfully. Relax and enjoy!"
