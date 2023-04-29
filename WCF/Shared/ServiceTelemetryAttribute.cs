namespace Microsoft.ApplicationInsights.Wcf
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.ServiceModel.Description;
    using System.ServiceModel.Dispatcher;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Wcf.Implementation;

    /// <summary>
    /// Enables Application Insights telemetry when applied on a
    /// WCF service class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ServiceTelemetryAttribute : Attribute, IServiceBehavior
    {
        /// <summary>
        /// Gets or sets ConnectionString used in current version of Application Insights SDK to store telemetry data.
        /// </summary>
        public string ConnectionString { get; set; }

        void IServiceBehavior.AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters)
        {
        }

        void IServiceBehavior.ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHost)
        {
            try
            {
                var configuration = TelemetryConfiguration.Active;
                if (!string.IsNullOrEmpty(this.ConnectionString))
                {
                    configuration.ConnectionString = this.ConnectionString;
                }

                var contractFilter = BuildFilter(serviceDescription);
                WcfInterceptor interceptor = null;
                foreach (var channelDisp in serviceHost.ChannelDispatchers.Cast<ChannelDispatcher>())
                {
                    if (channelDisp.ErrorHandlers.OfType<WcfInterceptor>().Any())
                    {
                        // already added, ignore
                        continue;
                    }

                    if (interceptor == null)
                    {
                        interceptor = new WcfInterceptor(configuration, contractFilter);
                    }

                    channelDisp.ErrorHandlers.Insert(0, interceptor);
                    foreach (var ep in channelDisp.Endpoints)
                    {
                        if (!ep.IsSystemEndpoint)
                        {
                            ep.DispatchRuntime.MessageInspectors.Insert(0, interceptor);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WcfEventSource.Log.InitializationFailure(ex.ToString());
            }
        }

        void IServiceBehavior.Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
        }

        private static ContractFilter BuildFilter(ServiceDescription serviceDescription)
        {
            var contracts = from ep in serviceDescription.Endpoints
                            where !ep.IsSystemEndpoint
                            select ep.Contract;
            return new ContractFilter(contracts);
        }
    }
}
