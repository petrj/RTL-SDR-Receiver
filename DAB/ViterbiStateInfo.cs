using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
namespace DAB
{
    public class decision_t
    {
        public const int NUMSTATES = 64;

        public uint[] w = new uint[NUMSTATES/32];
    }

    public class metric_t
    {
        public const int NUMSTATES = 64;

        public uint[] t = new uint[NUMSTATES];
    }

    public class ViterbiStateInfo
    {
        public const int NUMSTATES = 64;

        private bool _swapped = false;

        private metric_t _metrics1 = new metric_t();
        private metric_t _metrics2 = new metric_t();

        public void Swap()
        {
            _swapped = !_swapped;
        }

        public metric_t old_metrics
        {
            get
            {
                if (_swapped)
                {
                    return _metrics2;
                }
                else
                {
                    return _metrics1;
                }
            }
        }

        public metric_t new_metrics
        {
            get
            {
                if (_swapped)
                {
                    return _metrics1;
                }
                else
                {
                    return _metrics2;
                }
            }
        }

        public List<decision_t> decisions { get; set; } = new List<decision_t> ();

        private int current_decision_index = -1;

        public void SetCurrentDecisionIndex(int index)
        {
            current_decision_index = index;
        }

        // current decision
        public decision_t d
        {
            get
            {
                if (decisions == null ||
                    decisions.Count == 0 ||
                    current_decision_index < 0 ||
                    current_decision_index > decisions.Count - 1)
                    return null;

                return decisions[current_decision_index];
            }
        }

        public ViterbiStateInfo(int NUMSTATES, int starting_state = 0)
        {
            for (int i = 0; i < NUMSTATES; i++)
                _metrics1.t[i] = 63;

            /* Bias known start state */
            old_metrics.t[starting_state & (NUMSTATES - 1)] = 0;
        }
    }
}
