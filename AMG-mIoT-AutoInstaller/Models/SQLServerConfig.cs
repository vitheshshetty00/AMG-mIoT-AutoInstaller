using System;
using System.ComponentModel;
using System.Linq;

namespace AMG_mIoT_AutoInstaller.Models
{
    public class SQLServerConfig : ComponentConfiguration, IDataErrorInfo
    {
        private string _instanceName = "MSSQLSERVER";
        private string _instanceId = "MSSQLSERVER";
        private string _installSharedDir = "";
        private string _installSharedWowDir = "";
        private string _instanceDir = "";
        private string _sqlUserDbDir = "";
        private string _sqlUserDbLogDir = "";
        private string _sqlTempDbDir = "";
        private string _sqlTempDbLogDir = "";
        private string _saPassword = "";

        public string InstanceName
        {
            get => _instanceName;
            set
            {
                if (_instanceName != value)
                {
                    _instanceName = value;
                    OnPropertyChanged(nameof(InstanceName));
                }
            }
        }

        public string InstanceId
        {
            get => _instanceId;
            set
            {
                if (_instanceId != value)
                {
                    _instanceId = value;
                    OnPropertyChanged(nameof(InstanceId));

                    // Update dependent folder directories based on the new InstanceId
                    SqlUserDbDir = $"C:\\Program Files\\Microsoft SQL Server\\MSSQL16.{_instanceId}\\MSSQL\\DATA";
                    SqlUserDbLogDir = $"C:\\Program Files\\Microsoft SQL Server\\MSSQL16.{_instanceId}\\MSSQL\\DATA";
                    SqlTempDbDir = $"C:\\Program Files\\Microsoft SQL Server\\MSSQL16.{_instanceId}\\MSSQL\\DATA";
                    SqlTempDbLogDir = $"C:\\Program Files\\Microsoft SQL Server\\MSSQL16.{_instanceId}\\MSSQL\\DATA";
                }
            }
        }

        public string InstallSharedDir
        {
            get => _installSharedDir;
            set
            {
                if (_installSharedDir != value)
                {
                    _installSharedDir = value;
                    OnPropertyChanged(nameof(InstallSharedDir));
                }
            }
        }

        public string InstallSharedWowDir
        {
            get => _installSharedWowDir;
            set
            {
                if (_installSharedWowDir != value)
                {
                    _installSharedWowDir = value;
                    OnPropertyChanged(nameof(InstallSharedWowDir));
                }
            }
        }

        public string InstanceDir
        {
            get => _instanceDir;
            set
            {
                if (_instanceDir != value)
                {
                    _instanceDir = value;
                    OnPropertyChanged(nameof(InstanceDir));
                }
            }
        }

        public string SqlUserDbDir
        {
            get => _sqlUserDbDir;
            set
            {
                if (_sqlUserDbDir != value)
                {
                    _sqlUserDbDir = value;
                    OnPropertyChanged(nameof(SqlUserDbDir));
                }
            }
        }

        public string SqlUserDbLogDir
        {
            get => _sqlUserDbLogDir;
            set
            {
                if (_sqlUserDbLogDir != value)
                {
                    _sqlUserDbLogDir = value;
                    OnPropertyChanged(nameof(SqlUserDbLogDir));
                }
            }
        }

        public string SqlTempDbDir
        {
            get => _sqlTempDbDir;
            set
            {
                if (_sqlTempDbDir != value)
                {
                    _sqlTempDbDir = value;
                    OnPropertyChanged(nameof(SqlTempDbDir));
                }
            }
        }

        public string SqlTempDbLogDir
        {
            get => _sqlTempDbLogDir;
            set
            {
                if (_sqlTempDbLogDir != value)
                {
                    _sqlTempDbLogDir = value;
                    OnPropertyChanged(nameof(SqlTempDbLogDir));
                }
            }
        }

        public string SaPassword
        {
            get => _saPassword;
            set
            {
                if (_saPassword != value)
                {
                    _saPassword = value;
                    OnPropertyChanged(nameof(SaPassword));
                }
            }
        }

        // IDataErrorInfo implementation
        public string Error => null!;

        public string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(InstanceName):
                        return string.IsNullOrWhiteSpace(InstanceName)
                            ? "Instance Name is required."
                            : null!;
                    case nameof(InstanceId):
                        return string.IsNullOrWhiteSpace(InstanceId)
                            ? "Instance ID is required."
                            : null!;
                    case nameof(InstanceDir):
                        return string.IsNullOrWhiteSpace(InstanceDir)
                            ? "Instance Directory is required."
                            : null!;
                    case nameof(SqlUserDbDir):
                        return string.IsNullOrWhiteSpace(SqlUserDbDir)
                            ? "SQL User Database Directory is required."
                            : null!;
                    case nameof(SqlUserDbLogDir):
                        return string.IsNullOrWhiteSpace(SqlUserDbLogDir)
                            ? "SQL User Database Log Directory is required."
                            : null!;
                    case nameof(SqlTempDbDir):
                        return string.IsNullOrWhiteSpace(SqlTempDbDir)
                            ? "SQL Temp Database Directory is required."
                            : null!;
                    case nameof(SqlTempDbLogDir):
                        return string.IsNullOrWhiteSpace(SqlTempDbLogDir)
                            ? "SQL Temp Database Log Directory is required."
                            : null!;
                    case nameof(SaPassword):
                        return string.IsNullOrWhiteSpace(SaPassword)
                            ? "SA Password is required."
                            : null!;
                    default:
                        return null!;
                }
            }
        }

        public override bool Validate()
        {
            // Validate required fields (folder existence is not checked because they will be created)
            return !string.IsNullOrWhiteSpace(InstanceName)
                && !string.IsNullOrWhiteSpace(InstanceId)
                && !string.IsNullOrWhiteSpace(InstanceDir)
                && !string.IsNullOrWhiteSpace(SqlUserDbDir)
                && !string.IsNullOrWhiteSpace(SqlUserDbLogDir)
                && !string.IsNullOrWhiteSpace(SqlTempDbDir)
                && !string.IsNullOrWhiteSpace(SqlTempDbLogDir)
                && !string.IsNullOrWhiteSpace(SaPassword);
        }
    }
}
