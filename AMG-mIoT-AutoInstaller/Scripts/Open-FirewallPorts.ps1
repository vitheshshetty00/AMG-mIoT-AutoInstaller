param (
    [Parameter(Mandatory=$true, 
               HelpMessage="Specify ports to open. Can be a single port (80), comma-separated list (80,443,8080), or range (1000-2000)")]
    [string]$Ports,
    
    [Parameter(Mandatory=$false, 
               HelpMessage="Protocol to use (TCP or UDP)")]
    [string]$Protocol = "TCP",
    
    [Parameter(Mandatory=$false, 
               HelpMessage="Name for the firewall rule")]
    [string]$RuleName = "Custom Port Rule",
    
    [Parameter(Mandatory=$false, 
               HelpMessage="Description for the firewall rule")]
    [string]$Description = "Rule created by script"
)

# Validate protocol parameter
if ($Protocol -ne "TCP" -and $Protocol -ne "UDP") {
    Write-Error "Protocol must be either TCP or UDP"
    exit 1
}

# Function to process ports and create appropriate rules
function Process-Ports {
    param (
        [string]$PortsString
    )
    
    # Check if it's a range (contains a hyphen)
    if ($PortsString -match "(\d+)-(\d+)") {
        $startPort = [int]$Matches[1]
        $endPort = [int]$Matches[2]
        
        if ($startPort -gt $endPort) {
            Write-Error "Invalid port range: start port ($startPort) is greater than end port ($endPort)"
            exit 1
        }
        
        $portRange = "$startPort-$endPort"
        Write-Host "Creating firewall rule for port range $portRange using $Protocol protocol..."
        
        New-NetFirewallRule -DisplayName "$RuleName ($portRange)" `
                           -Description $Description `
                           -Direction Inbound `
                           -Protocol $Protocol `
                           -LocalPort $startPort-$endPort `
                           -Action Allow
    }
    # Check if it's a comma-separated list
    elseif ($PortsString -match ",") {
        $portList = $PortsString -split "," | ForEach-Object { $_.Trim() }
        
        # Validate all ports
        foreach ($port in $portList) {
            if (-not ($port -match "^\d+$")) {
                Write-Error "Invalid port number: $port"
                exit 1
            }
            
            if ([int]$port -lt 1 -or [int]$port -gt 65535) {
                Write-Error "Port number out of range (1-65535): $port"
                exit 1
            }
        }
        
        Write-Host "Creating firewall rule for ports $PortsString using $Protocol protocol..."
        
        New-NetFirewallRule -DisplayName "$RuleName ($PortsString)" `
                           -Description $Description `
                           -Direction Inbound `
                           -Protocol $Protocol `
                           -LocalPort $portList `
                           -Action Allow
    }
    # Single port
    else {
        if (-not ($PortsString -match "^\d+$")) {
            Write-Error "Invalid port number: $PortsString"
            exit 1
        }
        
        $port = [int]$PortsString
        
        if ($port -lt 1 -or $port -gt 65535) {
            Write-Error "Port number out of range (1-65535): $port"
            exit 1
        }
        
        Write-Host "Creating firewall rule for port $port using $Protocol protocol..."
        
        New-NetFirewallRule -DisplayName "$RuleName (Port $port)" `
                           -Description $Description `
                           -Direction Inbound `
                           -Protocol $Protocol `
                           -LocalPort $port `
                           -Action Allow
    }
}

# Check if running with administrator privileges
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
$isAdmin = $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Error "This script requires administrator privileges. Please run PowerShell as Administrator."
    exit 1
}

# Process the ports
Process-Ports -PortsString $Ports

Write-Host "Firewall rule(s) successfully created." -ForegroundColor Green