using System;

namespace NoaaWeb.Data.SatellitePass
{
    [Flags]
    public enum ProjectionTypes
    {
        None = 0,
        MsaStereographic = 1 << 1,
        MsaMercator = 1 << 2,
        ThermStereographic = 1 << 3,
        ThermMercator = 1 << 4
    }
}
