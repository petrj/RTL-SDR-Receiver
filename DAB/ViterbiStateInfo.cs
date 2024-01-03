using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
namespace DAB
{
    public class ViterbiDecision
    {
        public const int NUMSTATES = 64;

        public uint[] w = new uint[NUMSTATES/32];

        public override string ToString()
        {
            return $"W[0]={w[0]}, W[1]={w[1]}";
        }
    }

    public class ViterbiMetric
    {
        public const int NUMSTATES = 64;

        public uint[] t = new uint[NUMSTATES];
    }

    public class ViterbiStateInfo
    {
        public const int NUMSTATES = 64;

        private bool _swapped = false;

        private ViterbiMetric _metrics1 = new ViterbiMetric();
        private ViterbiMetric _metrics2 = new ViterbiMetric();

        /// <summary>
        /// Swap new and old metrics
        /// </summary>
        public void Swap()
        {
            _swapped = !_swapped;
        }

        public ViterbiMetric OldMetrics
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

        public ViterbiMetric NewMetrics
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

        public List<ViterbiDecision> Decisions { get; set; } = new List<ViterbiDecision> ();

        private int _current_decision_index = -1;

        public void SetCurrentDecisionIndex(int index)
        {
            _current_decision_index = index;
        }

        // current decision
        public ViterbiDecision d
        {
            get
            {
                if (Decisions == null ||
                    Decisions.Count == 0 ||
                    _current_decision_index < 0 ||
                    _current_decision_index > Decisions.Count - 1)
                    return null;

                return Decisions[_current_decision_index];
            }
        }

        public ViterbiStateInfo(int NUMSTATES, int starting_state = 0)
        {
            for (int i = 0; i < NUMSTATES; i++)
                _metrics1.t[i] = 63;

            /* Bias known start state */
            OldMetrics.t[starting_state & (NUMSTATES - 1)] = 0;
        }
    }
}
