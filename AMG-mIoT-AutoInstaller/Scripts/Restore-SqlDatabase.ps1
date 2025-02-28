<#
.SYNOPSIS
    Restores a SQL Server database from a .bak file.
.DESCRIPTION
    This PowerShell script restores a SQL Server database from a specified .bak file.
    It handles file relocation, takes care of existing connections, and manages various error scenarios.
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
    Optional. The path where to place the data files. If not specified, the default SQL Server data path will be used.
.PARAMETER LogFilePath
    Optional. The path where to place the log files. If not specified, the default SQL Server log path will be used.
.EXAMPLE
    .\Restore-SqlDatabase.ps1 -ServerInstance "localhost\SQLEXPRESS" -Username "sa" -Password "password123" -BackupFilePath "C:\Backups\MyDB.bak" -DatabaseName "MyDB"
.EXAMPLE
    .\Restore-SqlDatabase.ps1 -ServerInstance "localhost\SQLEXPRESS" -Username "sa" -Password "password123" -BackupFilePath "C:\Backups\MyDB.bak" -DatabaseName "MyDB" -DataFilePath "D:\SQLData" -LogFilePath "E:\SQLLogs"
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

function Write-Log {
    param (
        [string]$Message,
        [string]$Level = "INFO"
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] [$Level] $Message"
}

try {
    # Check if the backup file exists
    if (-not (Test-Path -Path $BackupFilePath)) {
        Write-Log "Backup file not found at path: $BackupFilePath" -Level "ERROR"
        exit 1
    }
    
    # Import the SQL Server module
    Write-Log "Importing SqlServer module..."
    
    try {
        Import-Module SqlServer -ErrorAction Stop
    }
    catch {
        Write-Log "SqlServer module not found. Attempting to install..." -Level "WARNING"
        try {
            Install-Module -Name SqlServer -Force -AllowClobber -Scope CurrentUser -ErrorAction Stop
            Import-Module SqlServer -ErrorAction Stop
            Write-Log "SqlServer module installed and imported successfully."
        }
        catch {
            Write-Log "Failed to install and import SqlServer module: $_" -Level "ERROR"
            exit 1
        }
    }
    
    # Create a secure string for the password
    $securePassword = ConvertTo-SecureString $Password -AsPlainText -Force
    $credential = New-Object System.Management.Automation.PSCredential ($Username, $securePassword)
    
    # Test connection to SQL Server
    Write-Log "Testing connection to SQL Server instance: $ServerInstance..."
    try {
        $testConnection = Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query "SELECT @@VERSION" -ConnectionTimeout 30 -ErrorAction Stop
        Write-Log "Successfully connected to SQL Server: $($testConnection.Column1)"
    }
    catch {
        Write-Log "Failed to connect to SQL Server instance: $_" -Level "ERROR"
        exit 1
    }
    
    # Get default data and log paths if not specified
    if (-not $DataFilePath -or -not $LogFilePath) {
        Write-Log "Retrieving default SQL Server data and log paths..."
        try {
            $defaultPaths = Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query "SELECT SERVERPROPERTY('InstanceDefaultDataPath') AS DataPath, SERVERPROPERTY('InstanceDefaultLogPath') AS LogPath" -ErrorAction Stop
            
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
    
    # Check if paths exist and are accessible
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
    
    # Get the logical file names from the backup
    Write-Log "Reading backup file information..."
    try {
        $backupInfo = Read-SqlBackupHeader -ServerInstance $ServerInstance -SqlCredential $credential -Path $BackupFilePath -ErrorAction Stop
        $fileList = Get-SqlFileList -ServerInstance $ServerInstance -SqlCredential $credential -Path $BackupFilePath -ErrorAction Stop
        
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
        $dbCheck = Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query "SELECT name FROM sys.databases WHERE name = '$DatabaseName'" -ErrorAction Stop
        if ($dbCheck) {
            $dbExists = $true
            Write-Log "Database '$DatabaseName' already exists." -Level "WARNING"
        }
    }
    catch {
        Write-Log "Error checking if database exists: $_" -Level "WARNING"
    }
    
    # If database exists, kill all active connections
    if ($dbExists) {
        Write-Log "Setting database to SINGLE_USER mode to disconnect all users..."
        try {
            $killConnections = @"
ALTER DATABASE [$DatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
ALTER DATABASE [$DatabaseName] SET OFFLINE;
"@
            Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query $killConnections -ErrorAction Stop
            Write-Log "All connections to database '$DatabaseName' have been terminated."
        }
        catch {
            Write-Log "Failed to set database to SINGLE_USER mode: $_" -Level "ERROR"
            # Continue with the restore attempt
        }
    }
    
    # Create the RESTORE command with MOVE options for each file
    Write-Log "Preparing database restore command..."
    $restoreCommand = "RESTORE DATABASE [$DatabaseName] FROM DISK = N'$BackupFilePath' WITH "
    
    $moveOptions = @()
    foreach ($file in $fileList) {
        $logicalName = $file.LogicalName
        $fileType = $file.Type
        $newFileName = [System.IO.Path]::GetFileName($file.PhysicalName)
        
        if ($fileType -eq 'D') {  # Data file
            $newFilePath = Join-Path -Path $DataFilePath -ChildPath $newFileName
            $moveOptions += "MOVE N'$logicalName' TO N'$newFilePath'"
        }
        else {  # Log file
            $newFilePath = Join-Path -Path $LogFilePath -ChildPath $newFileName
            $moveOptions += "MOVE N'$logicalName' TO N'$newFilePath'"
        }
    }
    
    $restoreCommand += ($moveOptions -join ", ") + ", REPLACE, STATS = 10, RECOVERY"
    
    # Execute the RESTORE command
    Write-Log "Starting database restore operation. This may take some time..."
    try {
        Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query $restoreCommand -QueryTimeout 0 -ErrorAction Stop
        Write-Log "Database '$DatabaseName' has been successfully restored from backup." -Level "INFO"
    }
    catch {
        Write-Log "Failed to restore database: $_" -Level "ERROR"
        # Try to bring the database back online if it exists
        if ($dbExists) {
            try {
                Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query "ALTER DATABASE [$DatabaseName] SET ONLINE; ALTER DATABASE [$DatabaseName] SET MULTI_USER;" -ErrorAction Stop
                Write-Log "Database '$DatabaseName' has been set back to MULTI_USER mode." -Level "WARNING"
            }
            catch {
                Write-Log "Failed to bring database back online: $_" -Level "ERROR"
            }
        }
        exit 1
    }
    
    # Set the database back to MULTI_USER mode
    Write-Log "Setting database to MULTI_USER mode..."
    try {
        Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query "ALTER DATABASE [$DatabaseName] SET MULTI_USER" -ErrorAction Stop
        Write-Log "Database '$DatabaseName' is now in MULTI_USER mode and ready for use."
    }
    catch {
        Write-Log "Failed to set database to MULTI_USER mode: $_" -Level "ERROR"
        # This is not critical, so we'll continue
    }
    
    # Verify the restore succeeded
    try {
        $verifyQuery = "SELECT DATABASEPROPERTYEX('$DatabaseName', 'Status') AS [Status], DATABASEPROPERTYEX('$DatabaseName', 'Recovery') AS [Recovery]"
        $verifyResult = Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query $verifyQuery -ErrorAction Stop
        
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
    # Clean up any resources if needed
    Write-Log "Script execution completed."
    [System.GC]::Collect()
}