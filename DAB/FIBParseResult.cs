using System;
using System.Collections.Generic;
using System.Text;

namespace DAB
{
    public class FIBParseResult
    {
        private Dictionary<int, string> _services = new Dictionary<int, string>();
        private Dictionary<int, string> _ensembles = new Dictionary<int, string>();

        public List<ServiceDescriptor> Services { get; private set; } = null;
        public List<EnsembleDescriptor> Ensembles { get; private set; } = null;

        public void AddService(ServiceDescriptor service)
        {
            if (Services == null)
            {
                Services = new List<ServiceDescriptor>();
            }

            if (_services.ContainsKey(service.ServiceIdentifier))
            {
                return;
            }

            Services.Add(service);
            _services.Add(service.ServiceIdentifier, service.ServiceLabel);
        }

        public void AddEnsemble(EnsembleDescriptor ensemble)
        {
            if (Ensembles == null)
            {
                Ensembles = new List<EnsembleDescriptor>();
            }

            if (_ensembles.ContainsKey(ensemble.EnsembleIdentifier))
            {
                return;
            }

            Ensembles.Add(ensemble);
            _ensembles.Add(ensemble.EnsembleIdentifier, ensemble.EnsembleLabel);
        }

        public void AddResult(FIBParseResult result)
        {
            if (result == null)
                return;

            if (result.Services != null)
            {
                foreach (var s in result.Services)
                {
                    AddService(s);
                }
            }
            if (result.Ensembles != null)
            {
                foreach (var ens in result.Ensembles)
                {
                    AddEnsemble(ens);
                }
            }
        }

        public override string ToString()
        {
            var res = new StringBuilder();
            res.Append("FIBParseResult: ");

            if (Services != null)
            {
                foreach (var s in Services)
                {
                    res.Append($"Service: {s} ");
                }
            }
            if (Ensembles != null)
            {
                foreach (var e in Ensembles)
                {
                    res.Append($"Ensemble: {e} ");
                }
            }

            return res.ToString();
        }
    }
}
