namespace DiversityService
{
    using Splat;
    using System;
    using System.Configuration;
    using System.ComponentModel;
    using Microsoft.ApplicationInsights;
    using System.Collections.Generic;

    class InsightsLogger : ILogger
    {
        private const string INSIGHTS_API_KEY = "InsightsKey";

        TelemetryClient Client;

        public InsightsLogger(string apiKey)
        {
            Client = new TelemetryClient();

            Client.InstrumentationKey = apiKey;
        }

        public LogLevel Level
        {
            get; set; 
        }

        public static void ConfigureLogging()
        {
            // Only register once
            var logger = Locator.Current.GetService<ILogger>();
            if (logger is InsightsLogger)
            {
                return;
            }

            var key = ConfigurationManager.AppSettings.Get(INSIGHTS_API_KEY);
            if(!string.IsNullOrWhiteSpace(key))
            {
                try
                {
                    Locator.CurrentMutable.RegisterConstant(new InsightsLogger(key), typeof(ILogger));
                }
                catch(Exception)
                {
                    // No Logging
                }
            }
        }

        public void Write([Localizable(false)]string message, LogLevel logLevel)
        {
            if(logLevel >= Level)
            {
                Client.TrackEvent("LogEntry", new Dictionary<string, string>() {
                    { "message",  message },
                    {"level", logLevel.ToString() }
                });
            }
        }
    }
}
