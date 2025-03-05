using System;

namespace AMG_mIoT_AutoInstaller.Models;

public class FirewallConfig : ComponentConfiguration
{
    public string Ports { get; set; } = "";
    private string _protocol = "TCP";
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

    public string RuleName { get; set; } = "Custom Port Rule";
    public string Description { get; set; } = "Rule created by installer";

    public override bool Validate()
    {
        return !string.IsNullOrWhiteSpace(Ports) && !string.IsNullOrWhiteSpace(RuleName);
    }
}
