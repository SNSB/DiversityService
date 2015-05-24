namespace DiversityService
{
    using Splat;
    using System;
    using System.Configuration;
    using System.ComponentModel;
    using Microsoft.ApplicationInsights;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Reflection;
    using System.Diagnostics.Contracts;
    using Microsoft.ApplicationInsights.DataContracts;

    static class InsightsLogging
    {
        private const string INSIGHTS_API_KEY = "InsightsKey";

        public static TelemetryClient Client;
        
        public static void ConfigureLogging()
        {
            // Only register once
            var logger = Locator.Current.GetService<ILogManager>();
            if (logger is InsightsLogManager)
            {
                return;
            }

            var key = ConfigurationManager.AppSettings.Get(INSIGHTS_API_KEY);
            if(!string.IsNullOrWhiteSpace(key))
            {
                try
                {
                    Client = new TelemetryClient();
                    Client.InstrumentationKey = key;
                    Locator.CurrentMutable.RegisterConstant(new InsightsLogManager(), typeof(ILogManager));
                }
                catch(Exception)
                {
                    // No Logging
                }
            }
        }
    }

    class InsightsLogManager : ILogManager
    {
        IFullLogger nullLogger = new WrappingFullLogger(new NullLogger(), typeof(InsightsLogManager));

        public IFullLogger GetLogger(Type type)
        {
            if (InsightsLogging.Client == null)
            {
                return nullLogger;
            }

            return new InsightsLogger(InsightsLogging.Client, type);
        }
    }

    /// <summary>
    /// Mostly copied from Splat/WrappingFullLogger
    /// </summary>
    class InsightsLogger : IFullLogger
    {
        TelemetryClient _client;

        readonly string prefix;
        readonly MethodInfo stringFormat;

        public InsightsLogger(TelemetryClient client, Type callingType)
        {
            _client = client;
            prefix = String.Format(CultureInfo.InvariantCulture, "{0}: ", callingType.Name);

            stringFormat = typeof(String).GetMethod("Format", new[] { typeof(IFormatProvider), typeof(string), typeof(object[]) });
            Contract.Requires(client != null);
            Contract.Requires(stringFormat != null);
        }

        string InvokeStringFormat(IFormatProvider formatProvider, string message, object[] args)
        {
            var sfArgs = new object[3];
            sfArgs[0] = formatProvider;
            sfArgs[1] = message;
            sfArgs[2] = args;
            return (string)stringFormat.Invoke(null, sfArgs);
        }

        public void Debug<T>(T value)
        {
            this.Write(prefix + value, LogLevel.Debug);
        }

        public void Debug<T>(IFormatProvider formatProvider, T value)
        {
            this.Write(String.Format(formatProvider, "{0}{1}", prefix, value), LogLevel.Debug);
        }

        public void DebugException(string message, Exception exception)
        {
            this.WriteException(message, exception, LogLevel.Debug);
        }

        public void Debug(IFormatProvider formatProvider, string message, params object[] args)
        {
            var result = InvokeStringFormat(formatProvider, message, args);

            this.Write(prefix + result, LogLevel.Debug);
        }


        public void Debug(string message)
        {
            this.Write(prefix + message, LogLevel.Debug);
        }

        public void Debug(string message, params object[] args)
        {
            var result = InvokeStringFormat(CultureInfo.InvariantCulture, message, args);
            this.Write(prefix + result, LogLevel.Debug);
        }

        public void Debug<TArgument>(IFormatProvider formatProvider, string message, TArgument argument)
        {
            this.Write(prefix + String.Format(formatProvider, message, argument), LogLevel.Debug);
        }

        public void Debug<TArgument>(string message, TArgument argument)
        {
            this.Write(prefix + String.Format(CultureInfo.InvariantCulture, message, argument), LogLevel.Debug);
        }

        public void Debug<TArgument1, TArgument2>(IFormatProvider formatProvider, string message, TArgument1 argument1, TArgument2 argument2)
        {
            this.Write(prefix + String.Format(formatProvider, message, argument1, argument2), LogLevel.Debug);
        }

        public void Debug<TArgument1, TArgument2>(string message, TArgument1 argument1, TArgument2 argument2)
        {
            this.Write(prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2), LogLevel.Debug);
        }

        public void Debug<TArgument1, TArgument2, TArgument3>(IFormatProvider formatProvider, string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            this.Write(prefix + String.Format(formatProvider, message, argument1, argument2, argument3), LogLevel.Debug);
        }

        public void Debug<TArgument1, TArgument2, TArgument3>(string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            this.Write(prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2, argument3), LogLevel.Debug);
        }

        public void Info<T>(T value)
        {
            this.Write(prefix + value, LogLevel.Info);
        }

        public void Info<T>(IFormatProvider formatProvider, T value)
        {
            this.Write(String.Format(formatProvider, "{0}{1}", prefix, value), LogLevel.Info);
        }

        public void InfoException(string message, Exception exception)
        {
            this.WriteException(message, exception, LogLevel.Info);
        }

        public void Info(IFormatProvider formatProvider, string message, params object[] args)
        {
            var result = InvokeStringFormat(formatProvider, message, args);
            this.Write(prefix + result, LogLevel.Info);
        }

        public void Info(string message)
        {
            this.Write(prefix + message, LogLevel.Info);
        }

        public void Info(string message, params object[] args)
        {
            var result = InvokeStringFormat(CultureInfo.InvariantCulture, message, args);
            this.Write(prefix + result, LogLevel.Info);
        }

        public void Info<TArgument>(IFormatProvider formatProvider, string message, TArgument argument)
        {
            this.Write(prefix + String.Format(formatProvider, message, argument), LogLevel.Info);
        }

        public void Info<TArgument>(string message, TArgument argument)
        {
            this.Write(prefix + String.Format(CultureInfo.InvariantCulture, message, argument), LogLevel.Info);
        }

        public void Info<TArgument1, TArgument2>(IFormatProvider formatProvider, string message, TArgument1 argument1, TArgument2 argument2)
        {
            this.Write(prefix + String.Format(formatProvider, message, argument1, argument2), LogLevel.Info);
        }

        public void Info<TArgument1, TArgument2>(string message, TArgument1 argument1, TArgument2 argument2)
        {
            this.Write(prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2), LogLevel.Info);
        }

        public void Info<TArgument1, TArgument2, TArgument3>(IFormatProvider formatProvider, string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            this.Write(prefix + String.Format(formatProvider, message, argument1, argument2, argument3), LogLevel.Info);
        }

        public void Info<TArgument1, TArgument2, TArgument3>(string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            this.Write(prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2, argument3), LogLevel.Info);
        }

        public void Warn<T>(T value)
        {
            this.Write(prefix + value, LogLevel.Warn);
        }

        public void Warn<T>(IFormatProvider formatProvider, T value)
        {
            this.Write(String.Format(formatProvider, "{0}{1}", prefix, value), LogLevel.Warn);
        }

        public void WarnException(string message, Exception exception)
        {
            this.WriteException(message, exception, LogLevel.Warn);
        }

        public void Warn(IFormatProvider formatProvider, string message, params object[] args)
        {
            var result = InvokeStringFormat(formatProvider, message, args);
            this.Write(prefix + result, LogLevel.Warn);
        }

        public void Warn(string message)
        {
            this.Write(prefix + message, LogLevel.Warn);
        }

        public void Warn(string message, params object[] args)
        {
            var result = InvokeStringFormat(CultureInfo.InvariantCulture, message, args);
            this.Write(prefix + result, LogLevel.Warn);
        }

        public void Warn<TArgument>(IFormatProvider formatProvider, string message, TArgument argument)
        {
            this.Write(prefix + String.Format(formatProvider, message, argument), LogLevel.Warn);
        }

        public void Warn<TArgument>(string message, TArgument argument)
        {
            this.Write(prefix + String.Format(CultureInfo.InvariantCulture, message, argument), LogLevel.Warn);
        }

        public void Warn<TArgument1, TArgument2>(IFormatProvider formatProvider, string message, TArgument1 argument1, TArgument2 argument2)
        {
            this.Write(prefix + String.Format(formatProvider, message, argument1, argument2), LogLevel.Warn);
        }

        public void Warn<TArgument1, TArgument2>(string message, TArgument1 argument1, TArgument2 argument2)
        {
            this.Write(prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2), LogLevel.Warn);
        }

        public void Warn<TArgument1, TArgument2, TArgument3>(IFormatProvider formatProvider, string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            this.Write(prefix + String.Format(formatProvider, message, argument1, argument2, argument3), LogLevel.Warn);
        }

        public void Warn<TArgument1, TArgument2, TArgument3>(string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            this.Write(prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2, argument3), LogLevel.Warn);
        }


        public void Error<T>(T value)
        {
            this.Write(prefix + value, LogLevel.Error);
        }

        public void Error<T>(IFormatProvider formatProvider, T value)
        {
            this.Write(String.Format(formatProvider, "{0}{1}", prefix, value), LogLevel.Error);
        }

        public void ErrorException(string message, Exception exception)
        {
            this.WriteException(message, exception, LogLevel.Error);
        }

        public void Error(IFormatProvider formatProvider, string message, params object[] args)
        {
            var result = InvokeStringFormat(formatProvider, message, args);
            this.Write(prefix + result, LogLevel.Error);
        }

        public void Error(string message)
        {
            this.Write(prefix + message, LogLevel.Error);
        }

        public void Error(string message, params object[] args)
        {
            var result = InvokeStringFormat(CultureInfo.InvariantCulture, message, args);
            this.Write(prefix + result, LogLevel.Error);
        }

        public void Error<TArgument>(IFormatProvider formatProvider, string message, TArgument argument)
        {
            this.Write(prefix + String.Format(formatProvider, message, argument), LogLevel.Error);
        }

        public void Error<TArgument>(string message, TArgument argument)
        {
            this.Write(prefix + String.Format(CultureInfo.InvariantCulture, message, argument), LogLevel.Error);
        }

        public void Error<TArgument1, TArgument2>(IFormatProvider formatProvider, string message, TArgument1 argument1, TArgument2 argument2)
        {
            this.Write(prefix + String.Format(formatProvider, message, argument1, argument2), LogLevel.Error);
        }

        public void Error<TArgument1, TArgument2>(string message, TArgument1 argument1, TArgument2 argument2)
        {
            this.Write(prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2), LogLevel.Error);
        }

        public void Error<TArgument1, TArgument2, TArgument3>(IFormatProvider formatProvider, string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            this.Write(prefix + String.Format(formatProvider, message, argument1, argument2, argument3), LogLevel.Error);
        }

        public void Error<TArgument1, TArgument2, TArgument3>(string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            this.Write(prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2, argument3), LogLevel.Error);
        }


        public void Fatal<T>(T value)
        {
            this.Write(prefix + value, LogLevel.Fatal);
        }

        public void Fatal<T>(IFormatProvider formatProvider, T value)
        {
            this.Write(String.Format(formatProvider, "{0}{1}", prefix, value), LogLevel.Fatal);
        }

        public void FatalException(string message, Exception exception)
        {
            this.WriteException(message, exception, LogLevel.Fatal);
        }

        public void Fatal(IFormatProvider formatProvider, string message, params object[] args)
        {
            var result = InvokeStringFormat(formatProvider, message, args);
            this.Write(prefix + result, LogLevel.Fatal);
        }

        public void Fatal(string message)
        {
            this.Write(prefix + message, LogLevel.Fatal);
        }

        public void Fatal(string message, params object[] args)
        {
            var result = InvokeStringFormat(CultureInfo.InvariantCulture, message, args);
            this.Write(prefix + result, LogLevel.Fatal);
        }

        public void Fatal<TArgument>(IFormatProvider formatProvider, string message, TArgument argument)
        {
            this.Write(prefix + String.Format(formatProvider, message, argument), LogLevel.Fatal);
        }

        public void Fatal<TArgument>(string message, TArgument argument)
        {
            this.Write(prefix + String.Format(CultureInfo.InvariantCulture, message, argument), LogLevel.Fatal);
        }

        public void Fatal<TArgument1, TArgument2>(IFormatProvider formatProvider, string message, TArgument1 argument1, TArgument2 argument2)
        {
            this.Write(prefix + String.Format(formatProvider, message, argument1, argument2), LogLevel.Fatal);
        }

        public void Fatal<TArgument1, TArgument2>(string message, TArgument1 argument1, TArgument2 argument2)
        {
            this.Write(prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2), LogLevel.Fatal);
        }

        public void Fatal<TArgument1, TArgument2, TArgument3>(IFormatProvider formatProvider, string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            this.Write(prefix + String.Format(formatProvider, message, argument1, argument2, argument3), LogLevel.Fatal);
        }

        public void Fatal<TArgument1, TArgument2, TArgument3>(string message, TArgument1 argument1, TArgument2 argument2, TArgument3 argument3)
        {
            this.Write(prefix + String.Format(CultureInfo.InvariantCulture, message, argument1, argument2, argument3), LogLevel.Fatal);
        }

        private static SeverityLevel ToSeverity(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    return SeverityLevel.Verbose;
                case LogLevel.Info:
                    return SeverityLevel.Information;
                case LogLevel.Warn:
                    return SeverityLevel.Warning;
                case LogLevel.Error:
                    return SeverityLevel.Error;
                case LogLevel.Fatal:
                    return SeverityLevel.Critical;
                default:
                    throw new NotImplementedException();
            }
        }

        public void Write([Localizable(false)]string message, LogLevel logLevel)
        {
            if(logLevel >= Level)
            {
                var telemetry = new TraceTelemetry(message, ToSeverity(logLevel));
                _client.TrackTrace(telemetry);
            }
        }

        public void WriteException([Localizable(false)]string message, Exception ex, LogLevel logLevel)
        {
            if(logLevel >= Level)
            {
                var telemetry = new ExceptionTelemetry(ex)
                {
                    HandledAt = ExceptionHandledAt.UserCode,
                    SeverityLevel = ToSeverity(logLevel),
                    Timestamp = DateTimeOffset.UtcNow
                };
                _client.TrackException(telemetry);
            }
        }

        public LogLevel Level { get; set; }
    }
}
