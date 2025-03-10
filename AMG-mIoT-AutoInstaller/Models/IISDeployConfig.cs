using System;
using System.ComponentModel;
using System.IO;

namespace AMG_mIoT_AutoInstaller.Models
{
    public class IISDeployConfig : ComponentConfiguration, IDataErrorInfo
    {
        private string _websiteName = "";
        private string _physicalPath = "";
        private string _port = "80";

        public string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(WebsiteName):
                        return string.IsNullOrWhiteSpace(WebsiteName)
                            ? "Website Name is required"
                            : null!;
                    case nameof(Port):
                        if (string.IsNullOrWhiteSpace(Port))
                            return "Port is required";
                        if (!int.TryParse(Port, out int portNum) || portNum < 1 || portNum > 65535)
                            return "Port must be a valid number between 1 and 65535";
                        return null!;
                    case nameof(PhysicalPath):
                        if (string.IsNullOrWhiteSpace(PhysicalPath))
                            return "Physical Path is required";
                        if (!Directory.Exists(PhysicalPath))
                            return "Physical Path does not exist";
                        return null!;
                    default:
                        return null!;
                }
            }
        }

        public string Error => null!;

        public string WebsiteName
        {
            get => _websiteName;
            set
            {
                _websiteName = value;
                OnPropertyChanged(nameof(WebsiteName));
            }
        }

        public string Port
        {
            get => _port;
            set
            {
                _port = value;
                OnPropertyChanged(nameof(Port));
            }
        }

        public string PhysicalPath
        {
            get => _physicalPath;
            set
            {
                _physicalPath = value;
                OnPropertyChanged(nameof(PhysicalPath));
            }
        }

        public override bool Validate()
        {
            if (
                string.IsNullOrWhiteSpace(WebsiteName)
                || string.IsNullOrWhiteSpace(Port)
                || string.IsNullOrWhiteSpace(PhysicalPath)
            )
                return false;

            if (!Directory.Exists(PhysicalPath))
                return false;

            if (!int.TryParse(Port, out int portNum) || portNum < 1 || portNum > 65535)
                return false;

            return true;
        }
    }
}
