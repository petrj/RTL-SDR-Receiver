using System;
namespace DAB
{
    public class DABEnsemble
    {
        public string EnsembleLabel { get; set; } = null;
        public int EnsembleIdentifier { get; set; } = -1;

        public override string ToString()
        {
            return $"{EnsembleLabel} (id {EnsembleIdentifier})";
        }
    }
}
