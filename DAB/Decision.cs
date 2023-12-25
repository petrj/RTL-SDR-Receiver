using System;
namespace DAB
{
    public class Decision
    {
        private const int NUMSTATES = 64;

        public uint[] W { get; set; }

        public Decision()
        {
            W = new uint[NUMSTATES / 32];
        }
    }
}
