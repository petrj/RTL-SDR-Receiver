﻿using System;
using System.Numerics;

namespace RTLSDR.DAB
{
    public class DataSyncPosition
    {
        public bool Synced { get; set; } = false;
        public int StartIndex { get; set; } = -1;
        public Complex[] FirstOFDMBuffer { get; set; }
    }
}
