using System;

namespace LastFM.ReaderCore.Logging
{
    public enum LogLevel
    {
        Debug,
        Information,
        Warning,
        Error,
        Critical
    }

    public interface ILogger
    {
        void Log(LogLevel level, string message, Exception exception = null);
        void LogDebug(string message);
        void LogInformation(string message);
        void LogWarning(string message);
        void LogError(string message, Exception exception = null);
        void LogCritical(string message, Exception exception = null);
    }
} 