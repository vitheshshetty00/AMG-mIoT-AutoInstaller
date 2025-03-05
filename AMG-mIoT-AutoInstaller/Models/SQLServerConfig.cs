using System;
using AMG_mIoT_AutoInstaller.ViewModels;

namespace AMG_mIoT_AutoInstaller.Models;

public class SQLServerConfig : ComponentConfiguration
{
    private string _instanceName = "MSSQLSERVER";
    private string _instanceId = "MSSQLSERVER";
    private string? _installSharedDir;
    private string? _installSharedWowDir;
    private string? _instanceDir;
    private string? _sqlUserDbDir;
    private string? _sqlUserDbLogDir;
    private string? _sqlTempDbDir;
    private string? _sqlTempDbLogDir;
    private string? _saPassword;

    public string InstanceName
    {
        get => _instanceName;
        set
        {
            _instanceName = value;
            OnPropertyChanged();
        }
    }

    public string InstanceId
    {
        get => _instanceId;
        set
        {
            _instanceId = value;
            OnPropertyChanged();
        }
    }

    public string InstallSharedDir
    {
        get => _installSharedDir;
        set
        {
            _installSharedDir = value;
            OnPropertyChanged();
        }
    }

    public string InstallSharedWowDir
    {
        get => _installSharedWowDir;
        set
        {
            _installSharedWowDir = value;
            OnPropertyChanged();
        }
    }

    public string InstanceDir
    {
        get => _instanceDir;
        set
        {
            _instanceDir = value;
            OnPropertyChanged();
        }
    }

    public string SqlUserDbDir
    {
        get => _sqlUserDbDir;
        set
        {
            _sqlUserDbDir = value;
            OnPropertyChanged();
        }
    }

    public string SqlUserDbLogDir
    {
        get => _sqlUserDbLogDir;
        set
        {
            _sqlUserDbLogDir = value;
            OnPropertyChanged();
        }
    }

    public string SqlTempDbDir
    {
        get => _sqlTempDbDir;
        set
        {
            _sqlTempDbDir = value;
            OnPropertyChanged();
        }
    }

    public string SqlTempDbLogDir
    {
        get => _sqlTempDbLogDir;
        set
        {
            _sqlTempDbLogDir = value;
            OnPropertyChanged();
        }
    }

    public string SaPassword
    {
        get => _saPassword;
        set
        {
            _saPassword = value;
            OnPropertyChanged();
        }
    }

    public override bool Validate()
    {
        // Add validation logic (e.g., required fields, valid paths)
        return !string.IsNullOrEmpty(InstanceName) && (!string.IsNullOrEmpty(SaPassword));
    }
}
