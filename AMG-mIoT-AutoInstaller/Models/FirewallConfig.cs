using System;
using System.ComponentModel;
using System.Linq;

namespace AMG_mIoT_AutoInstaller.Models
{
    public class FirewallConfig : ComponentConfiguration, IDataErrorInfo
    {
        private string _ports = "";
        private string _protocol = "TCP";
        private string _ruleName = "Custom Port Rule";
        private string _description = "Rule created by installer";

        public string Ports
        {
            get => _ports;
            set
            {
                if (_ports != value)
                {
                    _ports = value;
                    OnPropertyChanged(nameof(Ports));
                }
            }
        }

        public string Protocol
        {
            get => _protocol;
            set
            {
                if (_protocol != value)
                {
                    _protocol = value;
                    OnPropertyChanged(nameof(Protocol));
                }
            }
        }

        public string[] Protocols => new[] { "TCP", "UDP" };

        public string RuleName
        {
            get => _ruleName;
            set
            {
                if (_ruleName != value)
                {
                    _ruleName = value;
                    OnPropertyChanged(nameof(RuleName));
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged(nameof(Description));
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
                    case nameof(Ports):
                        if (string.IsNullOrWhiteSpace(Ports))
                            return "Ports are required";

                        var portEntries = Ports.Split(
                            new[] { ',' },
                            StringSplitOptions.RemoveEmptyEntries
                        );
                        foreach (var entry in portEntries)
                        {
                            var trimmed = entry.Trim();
                            if (trimmed.Contains('-'))
                            {
                                var parts = trimmed.Split(new[] { '-' }, 2);
                                if (
                                    parts.Length != 2
                                    || !int.TryParse(parts[0], out int start)
                                    || !int.TryParse(parts[1], out int end)
                                    || start < 1
                                    || end > 65535
                                    || start > end
                                )
                                    return "Invalid port range format or values";
                            }
                            else if (
                                !int.TryParse(trimmed, out int port)
                                || port < 1
                                || port > 65535
                            )
                            {
                                return "Invalid port number";
                            }
                        }
                        return null!;

                    case nameof(Protocol):
                        return Protocols.Contains(Protocol) ? null! : "Protocol must be TCP or UDP";

                    case nameof(RuleName):
                        return string.IsNullOrWhiteSpace(RuleName)
                            ? "Rule name is required"
                            : null!;

                    default:
                        return null!;
                }
            }
        }

        public override bool Validate()
        {
            // Check required fields
            if (string.IsNullOrWhiteSpace(Ports) || string.IsNullOrWhiteSpace(RuleName))
                return false;

            // Validate ports format
            var portEntries = Ports.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in portEntries)
            {
                var trimmed = entry.Trim();
                if (trimmed.Contains('-'))
                {
                    var parts = trimmed.Split(new[] { '-' }, 2);
                    if (
                        parts.Length != 2
                        || !int.TryParse(parts[0], out int start)
                        || !int.TryParse(parts[1], out int end)
                        || start < 1
                        || end > 65535
                        || start > end
                    )
                        return false;
                }
                else if (!int.TryParse(trimmed, out int port) || port < 1 || port > 65535)
                {
                    return false;
                }
            }

            // Validate protocol
            if (!Protocols.Contains(Protocol))
                return false;

            return true;
        }
    }
}
