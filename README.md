# AMG-mIoT AutoInstaller

A sophisticated Windows automation tool designed to streamline the deployment of server components through an intuitive graphical interface. Built using modern WPF technology and following MVVM architectural patterns for maintainability and scalability.

## Features

### Server Component Installation

- **IIS Configuration and Setup**

  - Automated website creation and configuration
  - Application pool management
  - SSL certificate installation
  - URL rewrite rules setup

- **SQL Server Management**

  - Instance installation and configuration
  - Database creation and restoration
  - User permissions setup
  - Backup scheduling configuration

- **Windows Services**

  - Service installation and configuration
  - Startup type management
  - Credentials setup
  - Dependency chain handling

- **Security Configuration**
  - Firewall rules management
  - Port configuration
  - Protocol settings
  - Security policy implementation

### Additional Features

- Advanced PowerShell script execution engine
- Real-time installation progress monitoring
- Detailed logging system
- Error handling and recovery mechanisms
- Configuration export/import functionality

## Requirements

### System Requirements

- Windows 10/11 (64-bit)
- .NET 9.0 Runtime
- Minimum 4GB RAM
- 10GB available disk space
- Administrative privileges

### Prerequisites

- Visual Studio 2022 (for development)
- SQL Server Management Studio (optional)
- IIS Management Console (optional)

## Getting Started

1. **Installation**

   ```powershell
   git clone https://github.com/your-org/AMG-mIoT-AutoInstaller.git
   cd AMG-mIoT-AutoInstaller
   ```

2. **Development Setup**

   - Open `AMG-mIoT-AutoInstaller.sln` in Visual Studio 2022
   - Restore NuGet packages
   - Build the solution
   - Run the application

3. **First-Time Configuration**
   - Configure installation paths
   - Set up default parameters
   - Verify system prerequisites

## Configuration Options

### IIS Features

- Web server role services
- Application Development features
- Security configurations
- Management tools

### SQL Server Settings

- Instance configuration
- Network protocols
- Authentication modes
- Backup strategies

### Service Management

- Service identity
- Recovery options
- Dependent services
- Startup parameters

### Firewall Configuration

- Inbound/outbound rules
- Port management
- Application rules
- Security profiles

## Technical Architecture

### Technology Stack

- **.NET 9.0**: Core framework
- **WPF**: UI framework
- **MVVM Pattern**: Architecture design
- **FluentWPF**: Modern UI components
- **MaterialDesignThemes**: Visual styling

### Key Components

- Dependency Injection Container
- Async Command Framework
- Logging Infrastructure
- Error Handling System
- Configuration Management

## Troubleshooting

Common issues and solutions:

- Installation failures
- Permission problems
- Network connectivity issues
- Database restoration errors

