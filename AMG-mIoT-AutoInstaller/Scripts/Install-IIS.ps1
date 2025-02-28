# Ensure the script runs as Administrator
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "Please run this script as an Administrator." -ForegroundColor Red
    exit
}


# Function to enable IIS and its sub-features
function Enable-IIS {
    Write-Host "Enabling IIS and its sub-features..." -ForegroundColor Green
    $iisFeatures = @(
    'IIS-WebServerRole',            # Web Server (IIS)
    'IIS-WebServer',                # Core Web Server functionality
    'IIS-CommonHttpFeatures',       # Common HTTP Features
    'IIS-DefaultDocument',          # Default Document
    'IIS-DirectoryBrowsing',        # Directory Browsing
    'IIS-HttpErrors',               # HTTP Errors
    'IIS-HttpLogging',              # HTTP Logging
    'IIS-Performance',              # Performance Features
    'IIS-RequestFiltering',         # Request Filtering
    'IIS-Security',                 # Security Features
    'IIS-StaticContent',            # Static Content
    'IIS-ISAPIExtensions',          # ISAPI Extensions
    'IIS-ISAPIFilter',              # ISAPI Filters
    'IIS-NetFxExtensibility45',     # .NET Extensibility 4.5
    'IIS-ASPNET45',                 # ASP.NET 4.5
    'IIS-ManagementConsole',        # IIS Management Console
    'IIS-HttpCompressionStatic'     # Static Content Compression
    )

    foreach ($feature in $iisFeatures) {
        Write-Host "Enabling feature: $feature" -ForegroundColor Yellow
        Enable-WindowsOptionalFeature -Online -FeatureName $feature -All -NoRestart
    }
    Write-Host "IIS and sub-features enabled successfully." -ForegroundColor Green
}


# Main Execution
try {

    # Enable IIS and its sub-features
    Enable-IIS

    Write-Host "IIS configurations completed successfully." -ForegroundColor Green
} catch {
    Write-Host "An error occurred: $_" -ForegroundColor Red
}