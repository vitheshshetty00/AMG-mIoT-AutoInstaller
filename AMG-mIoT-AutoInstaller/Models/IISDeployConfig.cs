using System;
using System.IO;

namespace AMG_mIoT_AutoInstaller.Models;

public class IISDeployConfig : ComponentConfiguration
{
    public string WebsiteName { get; set; } = "";
    public string Port { get; set; } = "80";
    public string PhysicalPath { get; set; } = "";

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
