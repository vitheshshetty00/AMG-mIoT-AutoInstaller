using System;
using AMG_mIoT_AutoInstaller.ViewModels;

namespace AMG_mIoT_AutoInstaller.Models;

public abstract class ComponentConfiguration : BaseViewModel
{
    public abstract bool Validate();
}
