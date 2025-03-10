using System.IO;

namespace AMG_mIoT_AutoInstaller.Models;

public class DBRestoreConfig : ComponentConfiguration
{
    private string _serverInstance = "";
    private string _username = "";
    private string _password = "";
    private string _backupFilePath = "";
    private string _databaseName = "";

    public string ServerInstance
    {
        get => _serverInstance;
        set
        {
            if (_serverInstance != value)
            {
                _serverInstance = value;
                OnPropertyChanged(nameof(ServerInstance));
            }
        }
    }

    public string Username
    {
        get => _username;
        set
        {
            if (_username != value)
            {
                _username = value;
                OnPropertyChanged(nameof(Username));
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (_password != value)
            {
                _password = value;
                OnPropertyChanged(nameof(Password));
            }
        }
    }

    public string BackupFilePath
    {
        get => _backupFilePath;
        set
        {
            if (_backupFilePath != value)
            {
                _backupFilePath = value;
                OnPropertyChanged(nameof(BackupFilePath));
            }
        }
    }

    public string DatabaseName
    {
        get => _databaseName;
        set
        {
            if (_databaseName != value)
            {
                _databaseName = value;
                OnPropertyChanged(nameof(DatabaseName));
            }
        }
    }

    // Validate required fields and check that the backup file exists
    public override bool Validate()
    {
        if (string.IsNullOrWhiteSpace(ServerInstance))
            return false;
        if (string.IsNullOrWhiteSpace(Username))
            return false;
        if (string.IsNullOrWhiteSpace(Password))
            return false;
        if (string.IsNullOrWhiteSpace(BackupFilePath))
            return false;
        if (string.IsNullOrWhiteSpace(DatabaseName))
            return false;

        // Validate the backup file exists
        if (!File.Exists(BackupFilePath))
            return false;

        return true;
    }
}
