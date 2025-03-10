<#
.SYNOPSIS
    Installs the ASP.NET Core Hosting Bundle for .NET.

.DESCRIPTION
    This script downloads and installs the ASP.NET Core Hosting Bundle for .NET versions 7, 8, or 9.
    It includes robust error handling, version checking, and verbose logging.

.PARAMETER dotNetVersion
    The .NET version to install (7, 8, or 9). Default is 9.

.PARAMETER Force
    Forces reinstallation even if the specified version is already installed.

.EXAMPLE
    .\Install-DotNetHosting.ps1 -dotNetVersion 8

.NOTES
    Requires administrator privileges to install the hosting bundle.
#>

[CmdletBinding()]
param (
    [Parameter(Position = 0)]
    [ValidateSet("7", "8", "9")]
    [string]$dotNetVersion = "9",
    
    [Parameter()]
    [switch]$Force
)

#region Functions

function Write-LogMessage {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string]$Message,
        
        [Parameter()]
        [ValidateSet("Info", "Warning", "Error", "Success")]
        [string]$Level = "Info"
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $colorMapping = @{
        "Info" = "White"
        "Warning" = "Yellow"
        "Error" = "Red"
        "Success" = "Green"
    }
    
    $color = $colorMapping[$Level]
    Write-Host "[$timestamp] " -NoNewline
    Write-Host "[$Level] " -NoNewline -ForegroundColor $color
    Write-Host "$Message"
}

function Test-AdminPrivileges {
    $currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-DotNetVersion {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    try {
        Write-LogMessage "Checking if .NET $Version is already installed..." -Level "Info"
        
        # Check for ASP.NET Core Module in IIS
        $aspNetCoreModuleInstalled = $false
        if (Get-Command "Get-WindowsOptionalFeature" -ErrorAction SilentlyContinue) {
            $iisInstalled = (Get-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole).State -eq "Enabled"
            if ($iisInstalled) {
                $aspNetCoreModuleInstalled = (Get-WindowsOptionalFeature -Online -FeatureName IIS-ASPNET45).State -eq "Enabled"
            }
        }
        
        # Check .NET runtime versions
        $dotnetInfo = & dotnet --list-runtimes 2>&1
        
        if ($dotnetInfo -is [System.Management.Automation.ErrorRecord]) {
            Write-LogMessage "dotnet command not found. .NET SDK may not be installed." -Level "Warning"
            return $false
        }
        
        $aspNetRuntimePattern = "Microsoft.AspNetCore.App $Version.*"
        $aspNetRuntimeInstalled = $dotnetInfo | Where-Object { $_ -match $aspNetRuntimePattern }
        
        if ($aspNetRuntimeInstalled) {
            $foundVersion = ($aspNetRuntimeInstalled -split " ")[1]
            Write-LogMessage "Found ASP.NET Core Runtime $foundVersion" -Level "Success"
            
            if ($aspNetCoreModuleInstalled) {
                Write-LogMessage "ASP.NET Core Module is installed in IIS" -Level "Success"
            } else {
                Write-LogMessage "ASP.NET Core Runtime is installed, but the IIS module might not be properly configured" -Level "Warning"
            }
            
            return $true
        } else {
            Write-LogMessage "ASP.NET Core Runtime $Version not found" -Level "Info"
            return $false
        }
    } catch {
        Write-LogMessage "Error checking .NET version: $_" -Level "Error"
        return $false
    }
}

function Get-HostingBundleInfo {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string]$Version
    )
    
    $versionDetails = @{
        "7" = @{
            Url = "https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/7.0.20/dotnet-hosting-7.0.20-win.exe"
            FileName = "dotnet-hosting-7.0.20-win.exe"
            FullVersion = "7.0.20"
        }
        "8" = @{
            Url = "https://download.visualstudio.microsoft.com/download/pr/0f847bc4-a961-4905-b1c2-93ebcff6604d/2b84b548511efc82dc679e9bed6bbf9b/dotnet-hosting-8.0.13-win.exe"
            FileName = "dotnet-hosting-8.0.13-win.exe"
            FullVersion = "8.0.13"
        }
        "9" = @{
            Url = "https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/9.0.2/dotnet-hosting-9.0.2-win.exe"
            FileName = "dotnet-hosting-9.0.2-win.exe"
            FullVersion = "9.0.2"
        }
    }
    
    return $versionDetails[$Version]
}

function Download-File {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string]$Url,
        
        [Parameter(Mandatory = $true)]
        [string]$OutputPath,
        
        [Parameter()]
        [int]$RetryCount = 3,
        
        [Parameter()]
        [int]$RetryWaitSeconds = 5
    )
    
    $attempt = 0
    $success = $false
    
    do {
        $attempt++
        try {
            Write-LogMessage "Download attempt $attempt of $RetryCount..." -Level "Info"
            
            $progressPreference = 'SilentlyContinue'  # Hide progress bar for faster downloads
            
            # Create a web session with appropriate headers
            $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
            $session.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) PowerShell/7.0"
            $session.Headers.Add("Accept", "application/octet-stream")
            $session.Headers.Add("Referer", "https://dotnet.microsoft.com/en-us/download/dotnet")
            
            # Download with progress tracking
            $start = Get-Date
            Invoke-WebRequest -Uri $Url -OutFile $OutputPath -WebSession $session -MaximumRedirection 20 -UseBasicParsing
            $elapsed = (Get-Date) - $start
            
            # Verify the download
            if (Test-Path $OutputPath) {
                $fileSize = (Get-Item $OutputPath).Length
                if ($fileSize -gt 1MB) {
                    Write-LogMessage "Downloaded $([Math]::Round($fileSize / 1MB, 2)) MB in $([Math]::Round($elapsed.TotalSeconds, 1)) seconds" -Level "Success"
                    $success = $true
                } else {
                    throw "Downloaded file is too small ($fileSize bytes). Expected at least 1MB."
                }
            } else {
                throw "File was not created at the specified path."
            }
        } catch {
            Write-LogMessage "Download failed: $_" -Level "Error"
            
            if (Test-Path $OutputPath) {
                Write-LogMessage "Removing partial download..." -Level "Info"
                Remove-Item -Path $OutputPath -Force -ErrorAction SilentlyContinue
            }
            
            if ($attempt -lt $RetryCount) {
                Write-LogMessage "Retrying in $RetryWaitSeconds seconds..." -Level "Warning"
                Start-Sleep -Seconds $RetryWaitSeconds
            } else {
                Write-LogMessage "Maximum retry count reached. Download failed." -Level "Error"
                throw "Failed to download file after $RetryCount attempts: $_"
            }
        }
    } while (-not $success -and $attempt -lt $RetryCount)
    
    return $success
}

function Install-DotNetHostingBundle {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string]$InstallerPath,
        
        [Parameter()]
        [string]$LogPath
    )
    
    try {
        Write-LogMessage "Starting installation of .NET Hosting Bundle..." -Level "Info"
        
        # Create install arguments
        $arguments = @("/install", "/quiet", "/norestart")
        if ($LogPath) {
            $arguments += "/log:$LogPath"
        }
        
        # Start installation process
        $process = Start-Process -FilePath $InstallerPath -ArgumentList $arguments -NoNewWindow -PassThru -Wait
        
        # Check the exit code
        if ($process.ExitCode -eq 0) {
            Write-LogMessage "Installation completed successfully (Exit code: $($process.ExitCode))" -Level "Success"
            return $true
        } elseif ($process.ExitCode -eq 3010) {
            Write-LogMessage "Installation completed successfully, but a reboot is required (Exit code: $($process.ExitCode))" -Level "Warning"
            return $true
        } else {
            Write-LogMessage "Installation failed with exit code: $($process.ExitCode)" -Level "Error"
            
            # Provide more helpful information based on common error codes
            switch ($process.ExitCode) {
                1602 { Write-LogMessage "User cancelled the installation" -Level "Warning" }
                1603 { Write-LogMessage "Fatal error during installation" -Level "Error" }
                1618 { Write-LogMessage "Another installation is already in progress" -Level "Error" }
                1641 { Write-LogMessage "Restart initiated - installation will complete on reboot" -Level "Warning" }
                5100 { Write-LogMessage "Generic error - check Windows Event Log for details" -Level "Error" }
                default { Write-LogMessage "Unexpected error - check installer log for details" -Level "Error" }
            }
            
            if ($LogPath -and (Test-Path $LogPath)) {
                Write-LogMessage "Installation log is available at: $LogPath" -Level "Info"
            }
            
            return $false
        }
    } catch {
        Write-LogMessage "Exception occurred during installation: $_" -Level "Error"
        return $false
    }
}

#endregion Functions

#region Main Script

# Clear the screen and display script header
Clear-Host
Write-LogMessage "===============================================" -Level "Info"
Write-LogMessage "   .NET Hosting Bundle Installation Script     " -Level "Info"
Write-LogMessage "===============================================" -Level "Info"
Write-LogMessage "Starting script execution" -Level "Info"

# Check for administrator privileges
if (-not (Test-AdminPrivileges)) {
    Write-LogMessage "This script requires administrator privileges to install the .NET Hosting Bundle." -Level "Error"
    Write-LogMessage "Please restart PowerShell as Administrator and run this script again." -Level "Error"
    exit 1
}

# Create logs directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$logsDir = Join-Path -Path $scriptDir -ChildPath "logs"
if (-not (Test-Path $logsDir)) {
    Write-LogMessage "Creating logs directory at $logsDir" -Level "Info"
    New-Item -Path $logsDir -ItemType Directory -Force | Out-Null
}

# Generate log file paths
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logFilePath = Join-Path -Path $logsDir -ChildPath "dotnet_install_$timestamp.log"
$installerLogPath = Join-Path -Path $logsDir -ChildPath "dotnet_installer_$timestamp.log"

# Write basic system information to log
Write-LogMessage "System Information:" -Level "Info"
Write-LogMessage "- Computer Name: $env:COMPUTERNAME" -Level "Info"
Write-LogMessage "- OS Version: $(Get-CimInstance Win32_OperatingSystem | Select-Object -ExpandProperty Caption)" -Level "Info"
Write-LogMessage "- PowerShell Version: $($PSVersionTable.PSVersion)" -Level "Info"
Write-LogMessage "- .NET Version to Install: $dotNetVersion" -Level "Info"

# Get hosting bundle information
$bundleInfo = Get-HostingBundleInfo -Version $dotNetVersion
Write-LogMessage "Selected .NET Hosting Bundle Version: $($bundleInfo.FullVersion)" -Level "Info"

# Check if the version is already installed
$isInstalled = Test-DotNetVersion -Version $dotNetVersion
if ($isInstalled -and -not $Force) {
    Write-LogMessage ".NET $dotNetVersion is already installed. Use -Force to reinstall." -Level "Info"
    exit 0
} elseif ($isInstalled -and $Force) {
    Write-LogMessage ".NET $dotNetVersion is already installed, but will be reinstalled due to -Force flag." -Level "Warning"
}

# Ensure the Downloads folder exists
$downloadsPath = [System.IO.Path]::Combine($env:USERPROFILE, "Downloads")
if (-not (Test-Path $downloadsPath)) {
    Write-LogMessage "Downloads folder not found at $downloadsPath. Creating folder..." -Level "Info"
    New-Item -Path $downloadsPath -ItemType Directory -Force | Out-Null
}

# Set installer path
$installerPath = [System.IO.Path]::Combine($downloadsPath, $bundleInfo.FileName)
Write-LogMessage "Installer will be downloaded to: $installerPath" -Level "Info"

# Download the installer
Write-LogMessage "Downloading .NET Hosting Bundle from $($bundleInfo.Url)..." -Level "Info"
try {
    $downloadSuccess = Download-File -Url $bundleInfo.Url -OutputPath $installerPath -RetryCount 3 -RetryWaitSeconds 5
    if (-not $downloadSuccess) {
        Write-LogMessage "Failed to download the installer after multiple attempts." -Level "Error"
        exit 1
    }
} catch {
    Write-LogMessage "Fatal error during download: $_" -Level "Error"
    exit 1
}

# Install the Hosting Bundle
Write-LogMessage "Running the installer..." -Level "Info"
$installSuccess = Install-DotNetHostingBundle -InstallerPath $installerPath -LogPath $installerLogPath

# Clean up
if (Test-Path $installerPath) {
    Write-LogMessage "Removing installer file..." -Level "Info"
    Remove-Item -Path $installerPath -Force -ErrorAction SilentlyContinue
    if (-not (Test-Path $installerPath)) {
        Write-LogMessage "Installer file removed successfully" -Level "Success"
    } else {
        Write-LogMessage "Failed to remove installer file" -Level "Warning"
    }
}

# Final status
if ($installSuccess) {
    Write-LogMessage "====================================================" -Level "Info"
    Write-LogMessage "Installation completed successfully!" -Level "Success"
    Write-LogMessage "- .NET Version: $($bundleInfo.FullVersion)" -Level "Info"
    Write-LogMessage "- Log file: $logFilePath" -Level "Info"
    Write-LogMessage "- Installer log: $installerLogPath" -Level "Info"
    
    $needsReboot = $false
    try {
        $pendingReboot = (Get-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager" -Name PendingFileRenameOperations -ErrorAction SilentlyContinue)
        if ($pendingReboot) {
            $needsReboot = $true
        }
    } catch {
        # Ignore errors checking for pending reboot
    }
    
    if ($needsReboot) {
        Write-LogMessage "A system restart is recommended to complete the installation" -Level "Warning"
    }
    
    Write-LogMessage "====================================================" -Level "Info"
    exit 0
} else {
    Write-LogMessage "====================================================" -Level "Info"
    Write-LogMessage "Installation failed or completed with warnings" -Level "Error"
    Write-LogMessage "Please check the installer log for details: $installerLogPath" -Level "Info"
    Write-LogMessage "====================================================" -Level "Info"
    exit 1
}

#endregion Main Script