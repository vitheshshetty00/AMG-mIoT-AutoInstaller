<#
.SYNOPSIS
    Installs or reinstalls a Windows service with enhanced error handling and features.

.DESCRIPTION
    This script installs or reinstalls a Windows service with robust error handling,
    detailed logging, service account options, recovery settings, and dependency handling.

.PARAMETER ServiceName
    The name of the service to install.

.PARAMETER ServicePath
    The full path to the service executable.

.PARAMETER DisplayName
    Optional display name for the service (defaults to ServiceName if not specified).

.PARAMETER Description
    Optional description for the service.

.PARAMETER StartupType
    The startup type for the service (Auto, Delayed, Manual, Disabled).

.PARAMETER ServiceAccount
    The account to run the service under (LocalSystem, LocalService, NetworkService, or a custom account).

.PARAMETER ServicePassword
    Password for custom service account (not needed for built-in accounts).

.PARAMETER Dependencies
    Comma-separated list of service dependencies.

.PARAMETER LogPath
    Path to save the log file (defaults to script directory).

.EXAMPLE
    .\Install-Service.ps1 -ServiceName "MyService" -ServicePath "C:\Path\To\Service.exe"

.EXAMPLE
    .\Install-Service.ps1 -ServiceName "MyService" -ServicePath "C:\Path\To\Service.exe" -StartupType Delayed -ServiceAccount NetworkService -Description "My custom service description"
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ServiceName,
    
    [Parameter(Mandatory = $true)]
    [string]$ServicePath,
    
    [Parameter(Mandatory = $false)]
    [string]$DisplayName,
    
    [Parameter(Mandatory = $false)]
    [string]$Description = "Windows service installed via PowerShell",
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("Auto", "Delayed", "Manual", "Disabled")]
    [string]$StartupType = "Auto",
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("LocalSystem", "LocalService", "NetworkService", "Custom")]
    [string]$ServiceAccount = "LocalSystem",
    
    [Parameter(Mandatory = $false)]
    [string]$ServicePassword,
    
    [Parameter(Mandatory = $false)]
    [string]$Dependencies
    
    )

# Initialize variables (for testing/development)
if (-not $PSBoundParameters.ContainsKey('ServiceName')) {
    $ServiceName = "Modbus"
}
if (-not $PSBoundParameters.ContainsKey('ServicePath')) {
    $ServicePath = "C:\Users\devteam\Downloads\ModbusWindowsService\ModbusWindowsService\ModbusWindowsService.exe"
}
if (-not $PSBoundParameters.ContainsKey('DisplayName')) {
    $DisplayName = $ServiceName
}

# Function to write log messages
function Write-Log {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,
        
        [Parameter(Mandatory = $false)]
        [ValidateSet("INFO", "WARNING", "ERROR", "SUCCESS")]
        [string]$Level = "INFO"
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    
    # Output to console with appropriate color
    switch ($Level) {
        "INFO"    { Write-Host $logMessage -ForegroundColor White }
        "WARNING" { Write-Host $logMessage -ForegroundColor Yellow }
        "ERROR"   { Write-Host $logMessage -ForegroundColor Red }
        "SUCCESS" { Write-Host $logMessage -ForegroundColor Green }
    }
    
    # Write to log file
  
}

function Test-AdminPrivileges {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-ServiceExists {
    param (
        [string]$Name
    )
    
    $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
    return ($null -ne $service)
}

function Stop-RunningService {
    param (
        [string]$Name
    )
    
    try {
        $service = Get-Service -Name $Name -ErrorAction Stop
        $serviceStatus = $service.Status
        
        if ($serviceStatus -eq 'Running') {
            Write-Log "Stopping service '$Name'..." "INFO"
            
            # Try to stop service gracefully first
            Stop-Service -Name $Name -Force -ErrorAction Stop
            
            # Wait for service to stop with timeout
            $timeout = New-TimeSpan -Seconds 30
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            $serviceStopped = $false
            
            while ($stopwatch.Elapsed -lt $timeout) {
                $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
                if ($null -eq $service -or $service.Status -eq 'Stopped') {
                    $serviceStopped = $true
                    break
                }
                Start-Sleep -Milliseconds 500
            }
            
            if (-not $serviceStopped) {
                # Force kill process if service won't stop
                Write-Log "Service did not stop gracefully within timeout period. Attempting to terminate process..." "WARNING"
                $serviceWMI = Get-WmiObject -Class Win32_Service -Filter "Name='$Name'" -ErrorAction SilentlyContinue
                if ($serviceWMI -and $serviceWMI.ProcessId -gt 0) {
                    $process = Get-Process -Id $serviceWMI.ProcessId -ErrorAction SilentlyContinue
                    if ($process) {
                        $process | Stop-Process -Force -ErrorAction SilentlyContinue
                        Write-Log "Process forcefully terminated." "WARNING"
                    }
                }
            } else {
                Write-Log "Service '$Name' stopped successfully." "SUCCESS"
            }
        } else {
            Write-Log "Service '$Name' is not running (current state: $serviceStatus)." "INFO"
        }
        return $true
    } catch {
        Write-Log "Failed to stop service '$Name': $_" "ERROR"
        return $false
    }
}

function Remove-ExistingService {
    param (
        [string]$Name
    )
    
    try {
        Write-Log "Deleting service '$Name'..." "INFO"
        
        # Try to use sc.exe to delete the service
        $deleteResult = sc.exe delete $Name
        
        # Wait for service to be fully deleted
        $timeout = New-TimeSpan -Seconds 10
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $serviceDeleted = $false
        
        while ($stopwatch.Elapsed -lt $timeout) {
            if (-not (Test-ServiceExists -Name $Name)) {
                $serviceDeleted = $true
                break
            }
            Start-Sleep -Milliseconds 500
        }
        
        if ($serviceDeleted) {
            Write-Log "Service '$Name' deleted successfully." "SUCCESS"
            return $true
        } else {
            Write-Log "Service deletion command was issued but service may still exist. Will attempt to continue..." "WARNING"
            return $true
        }
    } catch {
        Write-Log "Failed to delete service '$Name': $_" "ERROR"
        return $false
    }
}

function Install-WindowsService {
    param (
        [string]$Name,
        [string]$Path,
        [string]$Display,
        [string]$Desc,
        [string]$StartType,
        [string]$Account,
        [string]$Password,
        [string]$Deps
    )
    
    try {
        Write-Log "Installing service '$Name' from path '$Path'..." "INFO"
        
        # Build the sc.exe create command
        $scParams = "create $Name binPath= `"$Path`""
        
        # Add display name if provided
        if ($Display) {
            $scParams += " DisplayName= `"$Display`""
        }
        
        # Add startup type
        $startParam = switch ($StartType) {
            "Auto"     { "auto" }
            "Delayed"  { "delayed-auto" }
            "Manual"   { "demand" }
            "Disabled" { "disabled" }
            default    { "auto" }
        }
        $scParams += " start= $startParam"
        
        # Add service account
        $accountParam = switch ($Account) {
            "LocalSystem"    { "LocalSystem" }
            "LocalService"   { "NT Authority\LocalService" }
            "NetworkService" { "NT Authority\NetworkService" }
            "Custom"         { $ServicePassword }
            default          { "LocalSystem" }
        }
        $scParams += " obj= `"$accountParam`""
        
        # Add password for custom account
        if ($Account -eq "Custom" -and $Password) {
            $scParams += " password= `"$Password`""
        }
        
        # Add dependencies if specified
        if ($Deps) {
            $scParams += " depend= $Deps"
        }
        
        # Execute the command
        $createCommand = "sc.exe $scParams"
        $createResult = Invoke-Expression $createCommand
        
        if ($LASTEXITCODE -eq 0 -or $createResult -like "*SUCCESS*") {
            Write-Log "Service '$Name' installed successfully." "SUCCESS"
            
            # Set description
            if ($Desc) {
                sc.exe description $Name "$Desc" | Out-Null
            }
            
            # Configure service recovery options (restart after 1 min, then 2 mins, then after 5 mins)
            sc.exe failure $Name reset= 86400 actions= restart/60000/restart/120000/restart/300000 | Out-Null
            
            return $true
        } else {
            Write-Log "Failed to install service '$Name'. Result: $createResult" "ERROR"
            return $false
        }
    } catch {
        Write-Log "An error occurred while installing the service: $_" "ERROR"
        return $false
    }
}

function Set-ServiceDelayedAuto {
    param (
        [string]$Name
    )
    
    if ($StartupType -eq "Delayed") {
        try {
            # Set the delayed auto-start using the registry
            $regPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$Name"
            if (Test-Path $regPath) {
                Set-ItemProperty -Path $regPath -Name "DelayedAutoStart" -Value 1 -Type DWORD
                Write-Log "Set delayed auto-start for service '$Name'." "SUCCESS"
            } else {
                Write-Log "Could not find registry path for service '$Name'." "WARNING"
            }
        } catch {
            Write-Log "Failed to set delayed auto-start for service '$Name': $_" "WARNING"
        }
    }
}

function Start-InstalledService {
    param (
        [string]$Name
    )
    
    try {
        Write-Log "Starting service '$Name'..." "INFO"
        
        $startResult = Start-Service -Name $Name -ErrorAction Stop
        
        # Wait for service to start
        $timeout = New-TimeSpan -Seconds 20
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $serviceStarted = $false
        
        while ($stopwatch.Elapsed -lt $timeout) {
            $service = Get-Service -Name $Name -ErrorAction Stop
            if ($service.Status -eq 'Running') {
                $serviceStarted = $true
                break
            }
            Start-Sleep -Milliseconds 500
        }
        
        if ($serviceStarted) {
            Write-Log "Service '$Name' started successfully." "SUCCESS"
            return $true
        } else {
            Write-Log "Service did not start within the timeout period. Current status: $($service.Status)" "ERROR"
            return $false
        }
    } catch {
        Write-Log "Failed to start service '$Name': $_" "ERROR"
        return $false
    }
}

function Validate-ServicePath {
    param (
        [string]$Path
    )
    
    # Verify file exists
    if (-not (Test-Path $Path -PathType Leaf)) {
        Write-Log "Service executable not found at path: $Path" "ERROR"
        return $false
    }
    
    # Check if it's a valid executable
    $extension = [System.IO.Path]::GetExtension($Path).ToLower()
    if ($extension -ne ".exe") {
        Write-Log "Service path does not point to an executable (.exe) file: $Path" "ERROR"
        return $false
    }
    
    # Verify file is not locked
    try {
        $fileStream = [System.IO.File]::Open($Path, 'Open', 'Read', 'None')
        $fileStream.Close()
        $fileStream.Dispose()
    } catch {
        Write-Log "Service executable file is locked or cannot be accessed: $Path" "ERROR"
        return $false
    }
    
    return $true
}

# Begin script execution
try {

    
    Write-Log "======== SERVICE INSTALLATION STARTED ========" "INFO"
    Write-Log "Service Name: $ServiceName" "INFO"
    Write-Log "Service Path: $ServicePath" "INFO"
    Write-Log "Display Name: $DisplayName" "INFO"
    Write-Log "Description: $Description" "INFO"
    Write-Log "Startup Type: $StartupType" "INFO"
    Write-Log "Service Account: $ServiceAccount" "INFO"
    
    # Check for administrator privileges
    if (-not (Test-AdminPrivileges)) {
        Write-Log "This script must be run as an administrator." "ERROR"
        exit 1
    }
    
    # Validate service path
    if (-not (Validate-ServicePath -Path $ServicePath)) {
        Write-Log "Invalid service path. Installation aborted." "ERROR"
        exit 1
    }
    
    # Check if the service already exists
    if (Test-ServiceExists -Name $ServiceName) {
        Write-Log "Service '$ServiceName' already exists and will be reinstalled." "WARNING"
        
        # Stop and delete the existing service
        $stopResult = Stop-RunningService -Name $ServiceName
        if (-not $stopResult) {
            Write-Log "Failed to stop existing service. Continuing with deletion attempt..." "WARNING"
        }
        
        $deleteResult = Remove-ExistingService -Name $ServiceName
        if (-not $deleteResult) {
            Write-Log "Failed to remove existing service. Installation cannot continue." "ERROR"
            exit 1
        }
        
        # Short pause to ensure service is fully removed
        Start-Sleep -Seconds 2
    }
    
    # Install the service
    $installResult = Install-WindowsService -Name $ServiceName -Path $ServicePath -Display $DisplayName -Desc $Description -StartType $StartupType -Account $ServiceAccount -Password $ServicePassword -Deps $Dependencies
    
    if (-not $installResult) {
        Write-Log "Service installation failed. See above errors for details." "ERROR"
        exit 1
    }
    
    # Set delayed auto-start if needed
    if ($StartupType -eq "Delayed") {
        Set-ServiceDelayedAuto -Name $ServiceName
    }
    
    # Start the service if not set to Disabled or Manual
    if ($StartupType -ne "Disabled" -and $StartupType -ne "Manual") {
        $startResult = Start-InstalledService -Name $ServiceName
        if (-not $startResult) {
            Write-Log "Service was installed but failed to start." "WARNING"
            exit 1
        }
    } else {
        Write-Log "Service installed successfully but not started due to startup type: $StartupType" "INFO"
    }
    
    # Final verification
    $finalService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($finalService) {
        Write-Log "======== SERVICE INSTALLATION COMPLETED ========" "SUCCESS"
        Write-Log "Service Name: $ServiceName" "INFO"
        Write-Log "Current Status: $($finalService.Status)" "INFO"
        Write-Log "Start Mode: $($finalService.StartType)" "INFO"
        
        # Output to console for quick reference
        Write-Host ""
        Write-Host "Service installation completed successfully!" -ForegroundColor Green
        Write-Host "Log file: $LogPath" -ForegroundColor Cyan
    } else {
        Write-Log "Service verification failed. The service may not have been installed correctly." "ERROR"
        exit 1
    }
} catch {
    Write-Log "An unexpected error occurred: $_" "ERROR"
    Write-Log $_.ScriptStackTrace "ERROR"
    exit 1
}