[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SiteName,
    
    [Parameter(Mandatory = $false)]
    [string]$ApplicationPoolName,
    
    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 65535)]
    [int]$Port,
    
    [Parameter(Mandatory = $true)]
    [ValidateScript({ Test-Path $_ -PathType Container })]
    [string]$PhysicalPath,
    
    [Parameter(Mandatory = $false)]
    [string]$HostName = "",
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("v4.0", "v2.0", "No Managed Code")]
    [string]$DotNetVersion = "v4.0",
    
    [Parameter(Mandatory = $false)]
    [ValidateSet("Integrated", "Classic")]
    [string]$PipelineMode = "Integrated",
    
    [Parameter(Mandatory = $false)]
    [bool]$EnablePreload = $true,
    
       
    [Parameter(Mandatory = $false)]
    [bool]$AddToHostsFile = $false,
    
    [Parameter(Mandatory = $false)]
    [bool]$RemoveHostsEntryAfterTest = $true
)

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

# Function to check if a path can be written to
function Test-PathWritable {
    param (
        [string]$Path
    )
    
    try {
        $testFile = Join-Path -Path $Path -ChildPath "test_write_$(Get-Random).tmp"
        [System.IO.File]::CreateText($testFile).Close()
        Remove-Item -Path $testFile -Force
        return $true
    } catch {
        return $false
    }
}

# Function to check prerequisites
function Test-Prerequisites {
    try {
        # Check if running as administrator
        $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        if (-not $isAdmin) {
            Write-Log "This script must be run as Administrator." "ERROR"
            return $false
        }

       

        # Check if IIS is installed using multiple methods
        $iisInstalled = $false
        
        # Method 1: Check IIS Service
        $iisService = Get-Service -Name W3SVC -ErrorAction SilentlyContinue
        if ($iisService) {
            $iisInstalled = $true
            Write-Log "IIS Web Service (W3SVC) found." "SUCCESS"
        }
        
        # Method 2: Check IIS Installation Path
        if (Test-Path "$env:SystemRoot\System32\inetsrv\appcmd.exe") {
            $iisInstalled = $true
            Write-Log "IIS installation found (appcmd.exe exists)." "SUCCESS"
        }
        
        if (-not $iisInstalled) {
            Write-Log "IIS is not installed. Please install IIS first." "ERROR"
            return $false
        }

        # Try to import the WebAdministration module
        if (Get-Module -ListAvailable -Name WebAdministration) {
            Import-Module WebAdministration -ErrorAction Stop
            Write-Log "WebAdministration module imported successfully." "SUCCESS"
        } else {
            # Try loading Microsoft.Web.Administration assembly as fallback
            try {
                [System.Reflection.Assembly]::LoadWithPartialName("Microsoft.Web.Administration") | Out-Null
                if ([System.Reflection.Assembly]::LoadWithPartialName("Microsoft.Web.Administration")) {
                    Write-Log "Microsoft.Web.Administration assembly loaded successfully." "SUCCESS"
                } else {
                    Write-Log "Warning: Using appcmd.exe for IIS management." "WARNING"
                }
            } catch {
                Write-Log "Warning: Using appcmd.exe for IIS management." "WARNING"
            }
        }
        
        # Validate physical path
        if (-not (Test-Path -Path $PhysicalPath -PathType Container)) {
            Write-Log "Physical path '$PhysicalPath' does not exist or is not accessible." "ERROR"
            return $false
        }
        
        # Check if we can write to the physical path
        if (-not (Test-PathWritable -Path $PhysicalPath)) {
            Write-Log "WARNING: Cannot write to physical path '$PhysicalPath'. May need to set permissions manually later." "WARNING"
        }
        
        # Check for port availability using more reliable method
        try {
            $portInUse = $false
            $IPGlobalProperties = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties()
            $tcpConnections = $IPGlobalProperties.GetActiveTcpListeners()
            
            foreach ($connection in $tcpConnections) {
                if ($connection.Port -eq $Port) {
                    $portInUse = $true
                    break
                }
            }
            
            if ($portInUse) {
                # Check if the port is used by IIS
                $appcmdPath = "$env:SystemRoot\System32\inetsrv\appcmd.exe"
                if (Test-Path $appcmdPath) {
                    $existingSites = & $appcmdPath list site | Select-String -Pattern "site\s+name:""([^""]+)"".*bindings:.*:${Port}:" -AllMatches
                    if ($existingSites) {
                        $conflictingSites = $existingSites | ForEach-Object { $_.Matches.Groups[1].Value }
                        Write-Log "Port $Port is already in use by IIS site(s): $($conflictingSites -join ', ')." "ERROR"
                        return $false
                    } else {
                        Write-Log "Port $Port is in use by another application (not an IIS site)." "ERROR"
                        return $false
                    }
                } else {
                    Write-Log "Port $Port is in use, but can't verify which application is using it." "ERROR"
                    return $false
                }
            }
        } catch {
            # Fallback to netstat method if .NET approach fails
            Write-Log "Falling back to netstat for port check due to error: $_" "WARNING"
            
            $netstatOutput = netstat -ano | Select-String -Pattern ":$Port\s"
            if ($netstatOutput) {
                # Port is in use, now check if it's IIS
                $appcmdPath = "$env:SystemRoot\System32\inetsrv\appcmd.exe"
                if (Test-Path $appcmdPath) {
                    $existingSites = & $appcmdPath list site | Select-String -Pattern "site\s+name:""([^""]+)"".*bindings:.*:${Port}:" -AllMatches
                    if ($existingSites) {
                        $conflictingSites = $existingSites | ForEach-Object { $_.Matches.Groups[1].Value }
                        Write-Log "Port $Port is already in use by site(s): $($conflictingSites -join ', ')." "ERROR"
                        return $false
                    } else {
                        Write-Log "Port $Port is in use by another application (not an IIS site)." "ERROR"
                        return $false
                    }
                } else {
                    Write-Log "Port $Port appears to be in use, but can't verify which application is using it." "ERROR"
                    return $false
                }
            }
        }
        
        return $true
    }
    catch {
        Write-Log "Error checking prerequisites: $_" "ERROR"
        return $false
    }
}

# Function to create or validate application pool 
function New-OrValidateAppPool {
    param (
        [string]$PoolName
    )
    
    try {
        $appcmdPath = "$env:SystemRoot\System32\inetsrv\appcmd.exe"
        
        # Check if application pool exists
        $appPoolExists = $false
        $appPoolList = & $appcmdPath list apppool /name:"$PoolName" 2>&1
        if ($appPoolList -like "*$PoolName*") {
            $appPoolExists = $true
            Write-Log "Application pool '$PoolName' already exists. Verifying settings..." "INFO"
            
            # Update app pool settings if needed
            $modified = $false
            
            # Get current app pool settings
            $appPoolInfo = & $appcmdPath list apppool "$PoolName" /text:*
            
            # Check .NET version
            $currentRuntime = ($appPoolInfo | Select-String "managedRuntimeVersion:(.*)").Matches.Groups[1].Value
            # Handle "No Managed Code" case specially
            $runtimeValueToSet = $DotNetVersion
            if ($DotNetVersion -eq "No Managed Code") {
                $runtimeValueToSet = ""
            }
            
            if ($currentRuntime -ne $runtimeValueToSet) {
                & $appcmdPath set apppool "$PoolName" /managedRuntimeVersion:$runtimeValueToSet
                Write-Log "Updated .NET version from '$currentRuntime' to '$DotNetVersion'." "INFO"
                $modified = $true
            }
            
            # Check pipeline mode
            $currentPipelineMode = ($appPoolInfo | Select-String "managedPipelineMode:(.*)").Matches.Groups[1].Value
            if ($currentPipelineMode -ne $PipelineMode) {
                & $appcmdPath set apppool "$PoolName" /managedPipelineMode:$PipelineMode
                Write-Log "Updated pipeline mode from '$currentPipelineMode' to '$PipelineMode'." "INFO"
                $modified = $true
            }
            
            # Check preload enabled
            $currentStartMode = ($appPoolInfo | Select-String "startMode:(.*)").Matches.Groups[1].Value
            $targetStartMode = if ($EnablePreload) { "AlwaysRunning" } else { "OnDemand" }
            if ($currentStartMode -ne $targetStartMode) {
                & $appcmdPath set apppool "$PoolName" /startMode:$targetStartMode
                Write-Log "Updated preload setting to '$EnablePreload'." "INFO"
                $modified = $true
            }
            
            if ($modified) {
                Write-Log "Application pool '$PoolName' settings have been updated." "SUCCESS"
            } else {
                Write-Log "Application pool '$PoolName' already has the correct settings." "SUCCESS"
            }
        } else {
            # Create new application pool
            Write-Log "Creating new application pool '$PoolName'..." "INFO"
            & $appcmdPath add apppool /name:"$PoolName"
            
            # Configure application pool settings
            # Handle "No Managed Code" case specially
            $runtimeValueToSet = $DotNetVersion
            if ($DotNetVersion -eq "No Managed Code") {
                $runtimeValueToSet = ""
            }
            & $appcmdPath set apppool "$PoolName" /managedRuntimeVersion:$runtimeValueToSet
            & $appcmdPath set apppool "$PoolName" /managedPipelineMode:$PipelineMode
            
            if ($EnablePreload) {
                & $appcmdPath set apppool "$PoolName" /startMode:AlwaysRunning
            }
            
            Write-Log "Application pool '$PoolName' created successfully." "SUCCESS"
        }
        
        # Start the application pool if it's stopped
        $appPoolState = & $appcmdPath list apppool "$PoolName" /text:state
        if ($appPoolState -ne "Started") {
            & $appcmdPath start apppool "$PoolName"
            Write-Log "Started application pool '$PoolName'." "SUCCESS"
        }
        
        return $true
    }
    catch {
        Write-Log "Error creating/configuring application pool '$PoolName': $_" "ERROR"
        return $false
    }
}

# Function to parse bindings from appcmd output
function Parse-Bindings {
    param (
        [string]$bindingsString
    )
    
    $bindingsArray = @()
    if ($bindingsString -match "bindings:(.*)") {
        $bindingsContent = $matches[1]
        # Split by commas but not within quotes
        $bindings = $bindingsContent -split ',(?=(?:[^"]*"[^"]*")*[^"]*$)'
        foreach ($binding in $bindings) {
            if ($binding.Trim()) {
                $bindingsArray += $binding.Trim()
            }
        }
    }
    
    return $bindingsArray
}

# Function to create or update website using appcmd
function New-OrUpdateWebSite {
    try {
        $appcmdPath = "$env:SystemRoot\System32\inetsrv\appcmd.exe"
        
        # Check if the site already exists
        $siteList = & $appcmdPath list site "$SiteName" 2>&1
        $siteExists = $siteList -like "*$SiteName*"
        
        # Prepare binding information
        $bindingInfo = "http/*:${Port}:"
        if ($HostName) {
            $bindingInfo = "http/*:${Port}:$HostName"
        }
        
        if ($siteExists) {
            Write-Log "Site '$SiteName' already exists. Checking configuration..." "INFO"
            
            # Get current site information
            $siteInfo = & $appcmdPath list site "$SiteName" /text:*
            
            # Update physical path if different
            $currentPath = ($siteInfo | Select-String "physicalPath:(.*)").Matches.Groups[1].Value
            if ($currentPath -ne $PhysicalPath) {
                & $appcmdPath set site "$SiteName" /physicalPath:"$PhysicalPath"
                Write-Log "Updated physical path from '$currentPath' to '$PhysicalPath'." "INFO"
            }
            
            # Check application pool
            $currentAppPool = ($siteInfo | Select-String "applicationPool:(.*)").Matches.Groups[1].Value
            if ($currentAppPool -ne $ApplicationPoolName) {
                & $appcmdPath set site "$SiteName" /applicationPool:"$ApplicationPoolName"
                Write-Log "Updated application pool from '$currentAppPool' to '$ApplicationPoolName'." "INFO"
            }
            
            # Get current bindings
            $bindingsLine = ($siteInfo | Select-String "bindings:(.*)").Matches.Groups[0].Value
            $currentBindingsArray = Parse-Bindings -bindingsString $bindingsLine
            
            # Check if our desired binding exists
            $bindingExists = $false
            $portBindingExists = $false
            foreach ($binding in $currentBindingsArray) {
                if ($binding -eq $bindingInfo) {
                    $bindingExists = $true
                    break
                }
                if ($binding -match ":${Port}:") {
                    $portBindingExists = $true
                }
            }
            
            if (-not $bindingExists) {
                Write-Log "Updating site bindings..." "INFO"
                
                # If we're using a hostname and there's an existing binding for the same port,
                # we need to remove that binding to avoid conflicts
                if ($portBindingExists -and $HostName) {
                    $updatedBindings = @()
                    foreach ($binding in $currentBindingsArray) {
                        if ($binding -notmatch ":${Port}:") {
                            $updatedBindings += $binding
                        } else {
                            Write-Log "Removing conflicting binding: $binding" "INFO"
                        }
                    }
                    $updatedBindings += $bindingInfo
                    
                    # Convert back to comma-separated string
                    $bindingsString = $updatedBindings -join ","
                    
                    # Set new bindings
                    & $appcmdPath set site "$SiteName" /bindings:"$bindingsString"
                } else {
                    # Just add the new binding
                    & $appcmdPath set site "$SiteName" /+bindings."$bindingInfo"
                }
                
                Write-Log "Updated site bindings to include: $bindingInfo" "INFO"
            }
            
            Write-Log "Site '$SiteName' configuration has been updated." "SUCCESS"
        } else {
            # Create the new website
            Write-Log "Creating new site '$SiteName'..." "INFO"
            & $appcmdPath add site /name:"$SiteName" /physicalPath:"$PhysicalPath" /bindings:"$bindingInfo"
            # Then add this line immediately after:
            & $appcmdPath set app "$SiteName/" /applicationPool:"$ApplicationPoolName"
            if ($LASTEXITCODE -ne 0) {
                Write-Log "Failed to create site using appcmd. Error code: $LASTEXITCODE" "ERROR"
                return $false
            }
            
            Write-Log "Site '$SiteName' created successfully." "SUCCESS"
        }
        
        # Start the website if it's stopped
        $siteState = & $appcmdPath list site "$SiteName" /text:state
        if ($siteState -ne "Started") {
            & $appcmdPath start site "$SiteName"
            Write-Log "Started website '$SiteName'." "SUCCESS"
        }
        
        return $true
    }
    catch {
        Write-Log "Error creating/configuring website '$SiteName': $_" "ERROR"
        return $false
    }
}

# Function to add a hosts file entry
function Add-HostsEntry {
    param (
        [string]$HostName,
        [string]$IpAddress = "127.0.0.1"
    )
    
    try {
        $hostsPath = "$env:SystemRoot\System32\drivers\etc\hosts"
        
        # Check if we can write to the hosts file
        $canWrite = $false
        try {
            [System.IO.File]::OpenWrite($hostsPath).Close()
            $canWrite = $true
        } catch {
            $canWrite = $false
        }
        
        if (-not $canWrite) {
            Write-Log "Cannot write to hosts file. You may need to add the following entry manually:" "WARNING"
            Write-Log "$IpAddress $HostName" "WARNING"
            return $false
        }
        
        # Read current hosts file
        $hostsContent = Get-Content -Path $hostsPath -Raw
        
        # Check if entry already exists
        if ($hostsContent -match "(?m)^[^#]*\b$IpAddress\s+$([regex]::Escape($HostName))\b") {
            Write-Log "Hosts file entry for '$HostName' already exists." "INFO"
            return $true
        }
        
        # Add the new entry
        Write-Log "Adding entry to hosts file: $IpAddress $HostName" "INFO"
        Add-Content -Path $hostsPath -Value "`n$IpAddress $HostName # Added by IIS deployment script - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
        
        return $true
    } catch {
        Write-Log "Error adding hosts file entry: $_" "ERROR"
        return $false
    }
}

# Function to remove a hosts file entry
function Remove-HostsEntry {
    param (
        [string]$HostName,
        [string]$IpAddress = "127.0.0.1"
    )
    
    try {
        $hostsPath = "$env:SystemRoot\System32\drivers\etc\hosts"
        
        # Check if we can write to the hosts file
        $canWrite = $false
        try {
            [System.IO.File]::OpenWrite($hostsPath).Close()
            $canWrite = $true
        } catch {
            $canWrite = $false
        }
        
        if (-not $canWrite) {
            Write-Log "Cannot write to hosts file. You may need to remove the entry manually." "WARNING"
            return $false
        }
        
        # Read current hosts file
        $hostsContent = Get-Content -Path $hostsPath
        
        # Remove the entry (including the comment we added)
        $newContent = $hostsContent | Where-Object {
            $_ -notmatch "^\s*$IpAddress\s+$([regex]::Escape($HostName))\s*#\s*Added by IIS deployment script" -and
            $_ -notmatch "^\s*$IpAddress\s+$([regex]::Escape($HostName))\s*$"
        }
        
        # Write the updated content back
        Set-Content -Path $hostsPath -Value $newContent
        Write-Log "Removed hosts file entry for '$HostName'." "INFO"
        
        return $true
    } catch {
        Write-Log "Error removing hosts file entry: $_" "ERROR"
        return $false
    }
}

# Main script execution
try {
    # Initialize
    Write-Log "Starting IIS deployment for site: $SiteName" "INFO"
    Write-Log "Parameters:" "INFO"
    Write-Log "  Site Name: $SiteName" "INFO"
    Write-Log "  Application Pool: $ApplicationPoolName" "INFO"
    Write-Log "  Port: $Port" "INFO"
    Write-Log "  Physical Path: $PhysicalPath" "INFO"
    Write-Log "  Host Name: $HostName" "INFO"
    Write-Log "  .NET Version: $DotNetVersion" "INFO"
    Write-Log "  Pipeline Mode: $PipelineMode" "INFO"
    Write-Log "  Enable Preload: $EnablePreload" "INFO"
    
    # If application pool name is not provided, use site name
    if (-not $ApplicationPoolName) {
        $ApplicationPoolName = $SiteName
        Write-Log "Application pool name not provided. Using site name: $SiteName" "INFO"
    }
    
    # Check prerequisites
    Write-Log "Checking prerequisites..." "INFO"
    if (-not (Test-Prerequisites)) {
        Write-Log "Prerequisite check failed. Exiting script." "ERROR"
        exit 1
    }
    Write-Log "Prerequisites checked successfully." "SUCCESS"
    
    # Create or validate application pool
    Write-Log "Setting up application pool..." "INFO"
    if (-not (New-OrValidateAppPool -PoolName $ApplicationPoolName)) {
        Write-Log "Failed to create or validate application pool. Exiting script." "ERROR"
        exit 1
    }
    
    # Create or update website
    Write-Log "Setting up website..." "INFO"
    if (-not (New-OrUpdateWebSite)) {
        Write-Log "Failed to create or update website. Exiting script." "ERROR"
        exit 1
    }
    
    # Add hosts file entry if hostname provided and flag set
    $addedHostsEntry = $false
    if ($HostName -and $AddToHostsFile) {
        $addedHostsEntry = Add-HostsEntry -HostName $HostName
    }
    
    # Set folder permissions
    try {
        Write-Log "Setting folder permissions for IIS_IUSRS..." "INFO"
        $acl = Get-Acl $PhysicalPath
        $accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS_IUSRS", "ReadAndExecute, Write", "ContainerInherit, ObjectInherit", "None", "Allow")
        $acl.SetAccessRule($accessRule)
        Set-Acl $PhysicalPath $acl
        Write-Log "Folder permissions set successfully." "SUCCESS"
    }
    catch {
        Write-Log "Warning: Unable to set folder permissions: $_" "WARNING"
        Write-Log "You may need to manually set appropriate permissions on '$PhysicalPath'." "WARNING"
    }
    
    # Verify site is reachable
    try {
        Write-Log "Verifying site is accessible..." "INFO"
        $testUrl = "http://localhost:$Port"
        
        if ($HostName) {
            # If hostname is specified but couldn't add to hosts file, still try localhost
            if ($AddToHostsFile -and $addedHostsEntry) {
                $testUrl = "http://$HostName`:$Port"
            } else {
                Write-Log "Note: Testing with localhost instead of hostname due to hosts file limitations." "INFO"
            }
        }
        
        # Add a retry mechanism for site testing
        $maxRetries = 3
        $retryDelay = 2 # seconds
        $success = $false
        
        for ($i = 1; $i -le $maxRetries; $i++) {
            try {
                $webClient = New-Object System.Net.WebClient
                $response = $webClient.DownloadString($testUrl)
                Write-Log "Site verification successful: Site is accessible at $testUrl" "SUCCESS"
                $success = $true
                break
            } catch {
                if ($i -lt $maxRetries) {
                    Write-Log "Attempt $i failed: $_. Retrying in $retryDelay seconds..." "WARNING"
                    Start-Sleep -Seconds $retryDelay
                } else {
                    throw
                }
            }
        }
        
        if (-not $success) {
            throw "Site verification failed after $maxRetries attempts."
        }
    }
    catch {
        Write-Log "Warning: Unable to verify site is accessible: $_" "WARNING"
        Write-Log "The site may still be deploying or there might be configuration issues." "WARNING"
        Write-Log "Please verify manually by browsing to $testUrl" "WARNING"
    }
    
    # Clean up hosts entry if requested
    if ($HostName -and $AddToHostsFile -and $addedHostsEntry -and $RemoveHostsEntryAfterTest) {
        Remove-HostsEntry -HostName $HostName
    }
    
    Write-Log "IIS deployment completed successfully!" "SUCCESS"
    Write-Log "Site '$SiteName' is deployed" "SUCCESS"
    
    # Provide access information
    if ($HostName) {
        Write-Log "Site can be accessed at: http://$HostName`:$Port" "SUCCESS"
    } else {
        Write-Log "Site can be accessed at: http://localhost:$Port" "SUCCESS"
    }
    
    Write-Log "Application Pool: $ApplicationPoolName" "SUCCESS"
    Write-Log "Physical Path: $PhysicalPath" "SUCCESS"
    
    return 0  # Success exit code
}
catch {
    Write-Log "Fatal error in IIS deployment script: $_" "ERROR"
    exit 1
}