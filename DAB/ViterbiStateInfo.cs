using System;
namespace DAB
{
    public class ViterbiStateInfo
    {
        public ViterbiStateInfo(int NUMSTATES, int starting_state = 0)
        {
            metrics1 = new int[NUMSTATES];
            metrics2 = new int[NUMSTATES];
            old_metrics = new int[NUMSTATES];
            new_metrics = new int[NUMSTATES];

            for (int i = 0; i < NUMSTATES; i++)
                metrics1[i] = 63;

            old_metrics = metrics1;
            new_metrics = metrics2;
            /* Bias known start state */
            old_metrics[starting_state & (NUMSTATES - 1)] = 0;
        }

        public int[] metrics1 { get; set; }
        public int[] metrics2 { get; set; }

        public int[] old_metrics { get; set; }
        public int[] new_metrics { get; set; }
    }
}
