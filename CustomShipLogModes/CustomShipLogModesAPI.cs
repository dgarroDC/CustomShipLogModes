using System;

namespace CustomShipLogModes;

public class CustomShipLogModesAPI : ICustomShipLogModesAPI
{
    public void AddMode(ShipLogMode mode, Func<bool> isEnabledSupplier, Func<string> nameSupplier)
    {
        CustomShipLogModes.Instance.AddMode(mode, isEnabledSupplier, nameSupplier);
    }
}