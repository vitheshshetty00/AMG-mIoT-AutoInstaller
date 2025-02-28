<#
.SYNOPSIS
    Executes a SQL script on a specified SQL Server instance.
.DESCRIPTION
    This PowerShell script executes a specified SQL script on a SQL Server instance using SQL authentication.
    It handles various errors and ensures proper cleanup of resources.
.PARAMETER ServerInstance
    The SQL Server instance name (e.g., 'localhost', 'server\instance').
.PARAMETER Username
    The SQL Server authentication username.
.PARAMETER Password
    The SQL Server authentication password.
.PARAMETER ScriptPath
    The full path to the SQL script file to execute.
.EXAMPLE
    .\Execute-SqlScript.ps1 -ServerInstance "localhost\SQLEXPRESS" -Username "sa" -Password "password123" -ScriptPath "C:\Scripts\MyScript.sql"
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
    [string]$ScriptPath
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
    # Check if the SQL script file exists
    if (-not (Test-Path -Path $ScriptPath)) {
        Write-Log "SQL script file not found at path: $ScriptPath" -Level "ERROR"
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
    
    # Read the SQL script content
    Write-Log "Reading SQL script from $ScriptPath..."
    try {
        $scriptContent = Get-Content -Path $ScriptPath -Raw -ErrorAction Stop
    }
    catch {
        Write-Log "Failed to read SQL script file: $_" -Level "ERROR"
        exit 1
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
    
    # Execute the SQL script
    Write-Log "Executing SQL script..."
    try {
        Invoke-Sqlcmd -ServerInstance $ServerInstance -Credential $credential -Query $scriptContent -QueryTimeout 0 -ErrorAction Stop
        Write-Log "SQL script executed successfully."
    }
    catch {
        Write-Log "Error executing SQL script: $_" -Level "ERROR"
        exit 1
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