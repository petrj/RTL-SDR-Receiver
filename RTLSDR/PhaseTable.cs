using System;
using System.Collections.Generic;
using System.Numerics;
using LoggerService;

namespace RTLSDR
{
    public class PhaseTable
    {
        // https://github.com/AlbrechtL/welle.io/blob/master/src/backend/phasetable.cpp

        private ILoggingService _loggingService;

        public Complex[] RefTable { get; set; } = null;
        private const int K = 1536;

        private List<PhaseTableElement> CurrentTable { get; set; } = null;

        public PhaseTable(ILoggingService loggingService, int INPUT_RATE, int T_u)
        {
            _loggingService = loggingService;

            BuildRefTable(INPUT_RATE, T_u);
            BuildPhaseTable();
        }

        private void BuildPhaseTable()
        {
            CurrentTable = new List<PhaseTableElement>();
            for (var i=0;i<PhaseTableModeI.GetLength(0);i++)
            {
                CurrentTable.Add(new PhaseTableElement()
                {
                    KMin = PhaseTableModeI[i, 0],
                    KMax = PhaseTableModeI[i, 1],
                    I = PhaseTableModeI[i, 2],
                    N = PhaseTableModeI[i, 3]
                });
            }
        }

        private void BuildRefTable(int INPUT_RATE, int T_u)
        {
            double phi_k;

            RefTable = new Complex[INPUT_RATE];

            for (int i = 1; i <= K / 2; i++)
            {
                phi_k = get_Phi(i);
                RefTable[i] = new Complex(Math.Cos(phi_k), Math.Sin(phi_k));

                phi_k = get_Phi(-i);
                RefTable[T_u - i] = new Complex(Math.Cos(phi_k), Math.Sin(phi_k));
            }
        }

        private double get_Phi(int k)
        {
            for (int j = 0; CurrentTable[j].KMin != -1000; j++)
            {
                if ((CurrentTable[j].KMin <= k) && (k <= CurrentTable[j].KMax))
                {
                    int k_prime = CurrentTable[j].KMin;
                    int i = CurrentTable[j].I;
                    int n = CurrentTable[j].N;

                    return Math.PI / 2.0 * (h_table(i, k - k_prime) + n);
                }
            }
            throw new Exception("Invalid k in get_Phi");
        }

        private static short[] h0 = new short[] {
            0, 2, 0, 0, 0, 0, 1, 1, 2, 0, 0, 0, 2, 2, 1, 1,
            0, 2, 0, 0, 0, 0, 1, 1, 2, 0, 0, 0, 2, 2, 1, 1 };

        private static short[] h1 = new short[] {
            0, 3, 2, 3, 0, 1, 3, 0, 2, 1, 2, 3, 2, 3, 3, 0,
            0, 3, 2, 3, 0, 1, 3, 0, 2, 1, 2, 3, 2, 3, 3, 0};

        private static short[] h2 = new short[] {
            0, 0, 0, 2, 0, 2, 1, 3, 2, 2, 0, 2, 2, 0, 1, 3,
            0, 0, 0, 2, 0, 2, 1, 3, 2, 2, 0, 2, 2, 0, 1, 3};

        private static short[] h3 = new short[] {
            0, 1, 2, 1, 0, 3, 3, 2, 2, 3, 2, 1, 2, 1, 3, 2,
            0, 1, 2, 1, 0, 3, 3, 2, 2, 3, 2, 1, 2, 1, 3, 2 };

        private int h_table(int i, int j)
        {
            switch (i)
            {
                case 0:
                    return h0[j];
                case 1:
                    return h1[j];
                case 2:
                    return h2[j];
                case 3:
                    return h3[j];
                default:
                    throw new Exception("Invalid i in h_table");
            }
        }

        private readonly int[,] PhaseTableModeI =
        {
            {-768, -737, 0, 1},
            {-736, -705, 1, 2},
            {-704, -673, 2, 0},
            {-672, -641, 3, 1},
            {-640, -609, 0, 3},
            {-608, -577, 1, 2},
            {-576, -545, 2, 2},
            {-544, -513, 3, 3},
            {-512, -481, 0, 2},
            {-480, -449, 1, 1},
            {-448, -417, 2, 2},
            {-416, -385, 3, 3},
            {-384, -353, 0, 1},
            {-352, -321, 1, 2},
            {-320, -289, 2, 3},
            {-288, -257, 3, 3},
            {-256, -225, 0, 2},
            {-224, -193, 1, 2},
            {-192, -161, 2, 2},
            {-160, -129, 3, 1},
            {-128,  -97, 0, 1},
            {-96,   -65, 1, 3},
            {-64,   -33, 2, 1},
            {-32,    -1, 3, 2},
            {  1,    32, 0, 3},
            { 33,    64, 3, 1},
            { 65,    96, 2, 1},
            //  { 97,   128, 2, 1},  found bug 2014-09-03 Jorgen Scott
            { 97,   128, 1, 1},
            { 129,  160, 0, 2},
            { 161,  192, 3, 2},
            { 193,  224, 2, 1},
            { 225,  256, 1, 0},
            { 257,  288, 0, 2},
            { 289,  320, 3, 2},
            { 321,  352, 2, 3},
            { 353,  384, 1, 3},
            { 385,  416, 0, 0},
            { 417,  448, 3, 2},
            { 449,  480, 2, 1},
            { 481,  512, 1, 3},
            { 513,  544, 0, 3},
            { 545,  576, 3, 3},
            { 577,  608, 2, 3},
            { 609,  640, 1, 0},
            { 641,  672, 0, 3},
            { 673,  704, 3, 0},
            { 705,  736, 2, 1},
            { 737,  768, 1, 1},
            { -1000, -1000, 0, 0}
        };
    }
}
