using System;

namespace AMG_mIoT_AutoInstaller.Models;

public class DotNetConfig : ComponentConfiguration
{
    public required string Version { get; set; }
    public required string InstallPath { get; set; }

    public override bool Validate()
    {
        return !string.IsNullOrWhiteSpace(Version);
    }
}
