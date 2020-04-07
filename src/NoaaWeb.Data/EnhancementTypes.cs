﻿using System;

namespace NoaaWeb.Data
{
    [Flags]
    public enum EnhancementTypes
    {
        Za = 1 << 0,
        No = 1 << 1,
        Msa = 1 << 2,
        Mcir = 1 << 3,
        Therm = 1 << 4
    }
}