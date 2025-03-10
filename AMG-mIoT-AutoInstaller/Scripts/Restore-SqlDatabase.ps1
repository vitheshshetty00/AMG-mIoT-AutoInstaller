<#
.SYNOPSIS
    Restores a SQL Server database from a .bak file.
.DESCRIPTION
    This PowerShell script restores a SQL Server database from a specified .bak file.
    It ensures the SQL Server service account has read permissions on the backup file,
    handles file relocation, manages existing connections, and includes comprehensive error handling.
.PARAMETER ServerInstance
    The SQL Server instance name (e.g., 'localhost', 'server\instance').
.PARAMETER Username
    The SQL Server authentication username.
.PARAMETER Password
    The SQL Server authentication password.
.PARAMETER BackupFilePath
    The full path to the .bak file to restore from.
.PARAMETER DatabaseName
    The name of the database to restore to.
.PARAMETER DataFilePath
    Optional. The path where data files will be placed. Defaults to the SQL Server data path if not specified.
.PARAMETER LogFilePath
    Optional. The path where log files will be placed. Defaults to the SQL Server log path if not specified.
.EXAMPLE
    .\Restore-SqlDatabase.ps1 -ServerInstance "DESKTOP-DMME89S\AMIT" -Username "sa" -Password "pass!123#" -BackupFilePath "C:\Users\devteam\Desktop\AMGmIoT.bak" -DatabaseName "AMGIOT"
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$ServerInstance,
    
    [Parameter(Mandatory = $true)]
    [string]$Username,
    
    [Parameter(Mandatory = $true)]
    [string]$Password,
    
    [Parameter(Mandatory = $true)]
    [string]$BackupFilePath,
    
    [Parameter(Mandatory = $true)]
    [string]$DatabaseName,
    
    [Parameter(Mandatory = $false)]
    [string]$DataFilePath,
    
    [Parameter(Mandatory = $false)]
    [string]$LogFilePath
)
# Suppress all confirmation prompts
$ConfirmPreference = 'None'
$ProgressPreference = 'SilentlyContinue'  # Hide progress bars

# Force PowerShellGet to skip prompts during module installation
$env:POWERSHELL_UPDATECHECK = 'Off'
# Function to log messages with timestamps
function Write-Log {
    param (
        [string]$Message,
        [string]$Level = "INFO"
    )
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] [$Level] $Message"
}

# Function to grant read permissions to the SQL Server service account
function Set-BackupFilePermissions {
    param (
        [string]$BackupFilePath,
        [string]$ServerInstance
    )
    
    # Extract the instance name to determine the service account
    $instanceName = $ServerInstance.Split('\')[1]
    if (-not $instanceName) {
        $instanceName = "MSSQLSERVER"  # Default instance
    }
    $serviceAccount = "NT Service\MSSQL`$$instanceName"
    
    Write-Log "Granting read permissions to $serviceAccount on $BackupFilePath..."
    
    try {
        $acl = Get-Acl -Path $BackupFilePath
        $permission = $serviceAccount, "Read", "Allow"
        $accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule $permission
        $acl.SetAccessRule($accessRule)
        Set-Acl -Path $BackupFilePath -AclObject $acl
        Write-Log "Permissions set successfully for $serviceAccount."
    }
    catch {
        Write-Log "Failed to set permissions on ${BackupFilePath}: $_" -Level "ERROR"
        exit 1
    }
}

try {
    # Check if the backup file exists
    if (-not (Test-Path -Path $BackupFilePath)) {
        Write-Log "Backup file not found at path: $BackupFilePath" -Level "ERROR"
        exit 1
    }

    # Grant permissions to the SQL Server service account
    Set-BackupFilePermissions -BackupFilePath $BackupFilePath -ServerInstance $ServerInstance

    # Import the SqlServer module
    Write-Log "Importing SqlServer module..."
    try {
        Import-Module SqlServer -ErrorAction Stop
    }
    catch {
        Write-Log "SqlServer module not found. Attempting to install..." -Level "WARNING"
        try {
            # Check if NuGet is already installed before attempting installation
            if (-not (Get-PackageProvider -Name NuGet -ListAvailable -ErrorAction SilentlyContinue)) {
                Write-Log "NuGet provider not found. Installing NuGet provider..."
                Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -Scope CurrentUser
            }
            else {
                Write-Log "NuGet provider is already installed."
            }

            # Check if SqlServer module is already installed before attempting installation
            if (-not (Get-Module -Name SqlServer -ListAvailable -ErrorAction SilentlyContinue)) {
                Write-Log "SqlServer module not found. Installing SqlServer module..."
                Install-Module -Name SqlServer -Force -AllowClobber -Scope CurrentUser -SkipPublisherCheck -ErrorAction Stop
            }
            else {
                Write-Log "SqlServer module is already installed."
            }

            # Import the module
            Import-Module SqlServer -ErrorAction Stop
            Write-Log "SqlServer module imported successfully."
        }
        catch {
            Write-Log "Failed to install SqlServer module: $_" -Level "ERROR"
            exit 1
        }
    }

    # Create secure credentials
    $securePassword = ConvertTo-SecureString $Password -AsPlainText -Force
    $credential = New-Object System.Management.Automation.PSCredential ($Username, $securePassword)

    # Test SQL Server connection
    Write-Log "Testing connection to SQL Server instance: $ServerInstance..."
    try {
        $testConnection = Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query "SELECT @@VERSION" -ConnectionTimeout 30 -Encrypt Optional -TrustServerCertificate
        Write-Log "Successfully connected to SQL Server: $($testConnection.Column1)"
    }
    catch {
        Write-Log "Failed to connect to SQL Server instance: $_" -Level "ERROR"
        exit 1
    }

    # Determine default data and log paths if not provided
    if (-not $DataFilePath -or -not $LogFilePath) {
        Write-Log "Retrieving default SQL Server data and log paths..."
        try {
            $defaultPaths = Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query "SELECT SERVERPROPERTY('InstanceDefaultDataPath') AS DataPath, SERVERPROPERTY('InstanceDefaultLogPath') AS LogPath" -ErrorAction Stop -Encrypt Optional -TrustServerCertificate
            if (-not $DataFilePath) {
                $DataFilePath = $defaultPaths.DataPath.TrimEnd('\')
                Write-Log "Using default data path: $DataFilePath"
            }
            if (-not $LogFilePath) {
                $LogFilePath = $defaultPaths.LogPath.TrimEnd('\')
                Write-Log "Using default log path: $LogFilePath"
            }
        }
        catch {
            Write-Log "Failed to retrieve default paths: $_" -Level "WARNING"
            if (-not $DataFilePath) { $DataFilePath = "C:\Program Files\Microsoft SQL Server\MSSQL15.MSSQLSERVER\MSSQL\DATA" }
            if (-not $LogFilePath) { $LogFilePath = "C:\Program Files\Microsoft SQL Server\MSSQL15.MSSQLSERVER\MSSQL\DATA" }
            Write-Log "Using fallback data path: $DataFilePath" -Level "WARNING"
            Write-Log "Using fallback log path: $LogFilePath" -Level "WARNING"
        }
    }

    # Ensure data and log paths exist
    foreach ($path in @($DataFilePath, $LogFilePath)) {
        if (-not (Test-Path -Path $path)) {
            try {
                New-Item -Path $path -ItemType Directory -Force -ErrorAction Stop | Out-Null
                Write-Log "Created directory: $path"
            }
            catch {
                Write-Log "Failed to create directory ${path}: $_" -Level "ERROR"
                exit 1
            }
        }
    }

    # Retrieve backup file information
    Write-Log "Reading backup file information..."
    try {
        $headerQuery = "RESTORE HEADERONLY FROM DISK = N'$BackupFilePath'"
        $backupInfo = Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query $headerQuery -ErrorAction Stop -Encrypt Optional -TrustServerCertificate

        $fileListQuery = "RESTORE FILELISTONLY FROM DISK = N'$BackupFilePath'"
        $fileList = Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query $fileListQuery -ErrorAction Stop -Encrypt Optional -TrustServerCertificate

        Write-Log "Backup information retrieved successfully."
        Write-Log "Backup set name: $($backupInfo.BackupName)"
        Write-Log "Backup set created on: $($backupInfo.BackupStartDate)"
        Write-Log "Database name in backup: $($backupInfo.DatabaseName)"
        Write-Log "Found $(($fileList | Measure-Object).Count) logical files in the backup."
    }
    catch {
        Write-Log "Failed to read backup file information: $_" -Level "ERROR"
        exit 1
    }

    # Check if the database already exists
    $dbExists = $false
    try {
        $dbCheck = Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query "SELECT name FROM sys.databases WHERE name = '$DatabaseName'" -ErrorAction Stop -Encrypt Optional -TrustServerCertificate
        if ($dbCheck) {
            $dbExists = $true
            Write-Log "Database '$DatabaseName' already exists." -Level "WARNING"
        }
    }
    catch {
        Write-Log "Error checking if database exists: $_" -Level "WARNING"
    }

    # If database exists, disconnect all users
    if ($dbExists) {
        Write-Log "Setting database to SINGLE_USER mode to disconnect all users..."
        try {
            $killConnections = @"
ALTER DATABASE [$DatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
ALTER DATABASE [$DatabaseName] SET OFFLINE;
"@
            Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query $killConnections -ErrorAction Stop -Encrypt Optional -TrustServerCertificate
            Write-Log "All connections to database '$DatabaseName' have been terminated."
        }
        catch {
            Write-Log "Failed to set database to SINGLE_USER mode: $_" -Level "ERROR"
            # Proceed with restore attempt despite failure
        }
    }

    # Prepare the RESTORE command with MOVE options
    Write-Log "Preparing database restore command..."
    $restoreCommand = "RESTORE DATABASE [$DatabaseName] FROM DISK = N'$BackupFilePath' WITH "
    $moveOptions = @()
    foreach ($file in $fileList) {
        $logicalName = $file.LogicalName
        $fileType = $file.Type
        $newFileName = [System.IO.Path]::GetFileName($file.PhysicalName)
        if ($fileType -eq 'D') {
            # Data file
            $newFilePath = Join-Path -Path $DataFilePath -ChildPath $newFileName
            $moveOptions += "MOVE N'$logicalName' TO N'$newFilePath'"
        }
        else {
            # Log file
            $newFilePath = Join-Path -Path $LogFilePath -ChildPath $newFileName
            $moveOptions += "MOVE N'$logicalName' TO N'$newFilePath'"
        }
    }
    $restoreCommand += ($moveOptions -join ", ") + ", REPLACE, STATS = 10, RECOVERY"

    # Execute the restore
    Write-Log "Starting database restore operation. This may take some time..."
    try {
        Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query $restoreCommand -QueryTimeout 0 -ErrorAction Stop -Encrypt Optional -TrustServerCertificate
        Write-Log "Database '$DatabaseName' has been successfully restored from backup." -Level "INFO"
    }
    catch {
        Write-Log "Failed to restore database: $_" -Level "ERROR"
        if ($dbExists) {
            try {
                Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query "ALTER DATABASE [$DatabaseName] SET ONLINE; ALTER DATABASE [$DatabaseName] SET MULTI_USER;" -ErrorAction Stop -Encrypt Optional -TrustServerCertificate
                Write-Log "Database '$DatabaseName' has been set back to MULTI_USER mode." -Level "WARNING"
            }
            catch {
                Write-Log "Failed to bring database back online: $_" -Level "ERROR"
            }
        }
        exit 1
    }

    # Set database to MULTI_USER mode
    Write-Log "Setting database to MULTI_USER mode..."
    try {
        Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query "ALTER DATABASE [$DatabaseName] SET MULTI_USER" -ErrorAction Stop -Encrypt Optional -TrustServerCertificate
        Write-Log "Database '$DatabaseName' is now in MULTI_USER mode and ready for use."
    }
    catch {
        Write-Log "Failed to set database to MULTI_USER mode: $_" -Level "WARNING"
    }

    # Verify the restore
    Write-Log "Verifying database restore..."
    try {
        $verifyQuery = "SELECT DATABASEPROPERTYEX('$DatabaseName', 'Status') AS [Status], DATABASEPROPERTYEX('$DatabaseName', 'Recovery') AS [Recovery]"
        $verifyResult = Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query $verifyQuery -ErrorAction Stop -Encrypt Optional -TrustServerCertificate
        Write-Log "Database status: $($verifyResult.Status), Recovery: $($verifyResult.Recovery)"
        if ($verifyResult.Status -eq "ONLINE") {
            Write-Log "Database restore verification completed successfully. Database is ONLINE." -Level "INFO"
        }
        else {
            Write-Log "Database is not ONLINE after restore. Current status: $($verifyResult.Status)" -Level "WARNING"
        }
    }
    catch {
        Write-Log "Failed to verify database status: $_" -Level "WARNING"
    }
}
catch {
    Write-Log "An unexpected error occurred: $_" -Level "ERROR"
    exit 1
}
finally {
    Write-Log "Script execution completed."
    [System.GC]::Collect()
}