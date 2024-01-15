﻿using System;
using System.Collections.Generic;
using LoggerService;

namespace DAB
{
    public class FIGParser
    {
        public List<DABProgrammeServiceLabel> ProgrammeServiceLabels { get; set; } = new List<DABProgrammeServiceLabel>();
        private FIB _fib = null;
        private List<DABService> _DABServices = new List<DABService>();
        private ILoggingService _loggingService = null;

        private Dictionary<uint, DABSubChannel> SubChanels { get; set; }
        private Dictionary<uint, DABServiceComponentGlobalDefinition> GlobalDefinitions { get; set; }
        private Dictionary<int, DABProgrammeServiceLabel> ServiceLabels { get; set; }

        public FIGParser(ILoggingService loggingService, FIB fib, List<DABService> services)
        {
            _fib = fib;
            _DABServices = services;
            _loggingService = loggingService;

            SubChanels = new Dictionary<uint, DABSubChannel>();
            GlobalDefinitions = new Dictionary<uint, DABServiceComponentGlobalDefinition>();
            ServiceLabels = new Dictionary<int, DABProgrammeServiceLabel>();

            _fib.ProgrammeServiceLabelFound += _fib_ProgramServiceLabelFound;
            _fib.EnsembleFound += _fib_EnsembleFound;
            _fib.SubChannelFound += _fib_SubChannelFound;
            _fib.ServiceComponentFound += _fib_ServiceComponentFound;
            _fib.ServiceComponentGlobalDefinitionFound += _fib_ServiceComponentGlobalDefinitionFound;
        }

        private DABService GetServiceByNumber(uint serviceNumber)
        {
            foreach (var service in _DABServices)
            {
                if (service.ServiceNumber == serviceNumber)
                {
                    return service;
                }
            }
            return null;
        }

        private DABService GetServiceByIdentifier(int serviceIdentifier)
        {
            foreach (var service in _DABServices)
            {
                if (service.ServiceIdentifier == serviceIdentifier)
                {
                    return service;
                }
            }
            return null;
        }

        private DABService GetServiceBySubChId(uint subChId)
        {
            foreach (var service in _DABServices)
            {
                var c = service.GetComponentBySubChId(subChId);
                if (c != null)
                {
                    return service;
                }
            }
            return null;
        }

        private void _fib_ServiceComponentGlobalDefinitionFound(object sender, EventArgs e)
        {
            if (e is ServiceComponentGlobalDefinitionFoundEventArgs gde)
            {
                var service = GetServiceBySubChId(gde.ServiceGlobalDefinition.SubChId);
                if ((service != null) && (service.ServiceIdentifier == -1))
                {
                    if (service.ServiceIdentifier == -1)
                    {
                        service.ServiceIdentifier = Convert.ToInt32(gde.ServiceGlobalDefinition.ServiceIdentifier);
                        _loggingService.Info($"Setting ServiceIdentifier:{Environment.NewLine}{gde.ServiceGlobalDefinition}{Environment.NewLine}{service}");
                        service.SetSubChannels(SubChanels);
                        service.SetServiceLabels(ServiceLabels);
                    }
                } else
                {
                    if (!GlobalDefinitions.ContainsKey(gde.ServiceGlobalDefinition.ServiceIdentifier))
                    {
                        GlobalDefinitions.Add(gde.ServiceGlobalDefinition.ServiceIdentifier, gde.ServiceGlobalDefinition);
                    }
                }
            }
        }

        private void _fib_EnsembleFound(object sender, EventArgs e)
        {
            if (e is EnsembleFoundEventArgs ensembleArgs)
            {
            }
        }

        private void _fib_ProgramServiceLabelFound(object sender, EventArgs e)
        {
            if (e is ProgrammeServiceLabelFoundEventArgs sla)
            {
                var service = GetServiceByIdentifier(sla.ProgrammeServiceLabel.ServiceIdentifier);
                if (service != null)                 
                {
                    if (service.ServiceName == null)
                    {
                        service.ServiceName = sla.ProgrammeServiceLabel.ServiceLabel;
                        _loggingService.Info($"Setting service label:{Environment.NewLine}{sla.ProgrammeServiceLabel}{Environment.NewLine}{service}");
                    }
                } else
                {
                    if (!ServiceLabels.ContainsKey(sla.ProgrammeServiceLabel.ServiceIdentifier))
                    {
                        ServiceLabels.Add(sla.ProgrammeServiceLabel.ServiceIdentifier, sla.ProgrammeServiceLabel);
                    }
                }
            }
        }

        private void _fib_ServiceComponentFound(object sender, EventArgs e)
        {
            if (e is ServiceComponentFoundEventArgs serviceArgs)
            {
                var service = GetServiceByNumber(serviceArgs.ServiceComponent.ServiceNumber);
                if (service == null)
                {
                    // adding service
                    service  = new DABService()
                    {
                        ServiceNumber = serviceArgs.ServiceComponent.ServiceNumber,
                        Components = serviceArgs.ServiceComponent.Components,
                        CountryId = serviceArgs.ServiceComponent.CountryId,
                        ExtendedCountryCode = serviceArgs.ServiceComponent.ExtendedCountryCode,
                    };

                    _DABServices.Add(service);

                    service.SetSubChannels(SubChanels);
                    service.SetGlobalDefinitions(GlobalDefinitions);
                    service.SetServiceLabels(ServiceLabels);

                    _loggingService.Info($"Added service:{Environment.NewLine}{service}");
                }
            }
        }

        private void _fib_SubChannelFound(object sender, EventArgs e)
        {
            if (e is SubChannelFoundEventArgs s)
            {
                var service = GetServiceBySubChId(s.SubChannel.SubChId);
                if (service != null)
                {
                    service.SetSubChannels(new Dictionary<uint, DABSubChannel>() { { s.SubChannel.SubChId, s.SubChannel } });
                    service.SetGlobalDefinitions(GlobalDefinitions);
                    service.SetServiceLabels(ServiceLabels);

                    _loggingService.Info($"Setting service subchannel:{Environment.NewLine}{service}");
                } else
                {
                    if (!SubChanels.ContainsKey(s.SubChannel.SubChId))
                    {
                        SubChanels.Add(s.SubChannel.SubChId, s.SubChannel);
                    }
                }
            }
        }
    }
}
