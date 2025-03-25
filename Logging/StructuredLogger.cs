using System;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace LastFM.ReaderCore.Logging
{
    public class StructuredLogger : ILogger
    {
        private readonly string _logFilePath;
        private readonly LogLevel _minimumLevel;
        private readonly object _lock = new object();
        private readonly BlockingCollection<LogEntry> _logQueue;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _processQueueTask;
        private readonly int _maxQueueSize;
        private readonly TimeSpan _flushInterval;

        public StructuredLogger(string logFilePath = "logs/app.log", LogLevel minimumLevel = LogLevel.Information)
        {
            _logFilePath = logFilePath;
            _minimumLevel = minimumLevel;
            _maxQueueSize = 10000;
            _flushInterval = TimeSpan.FromSeconds(5);
            
            // Ensure log directory exists
            var logDir = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            _logQueue = new BlockingCollection<LogEntry>(_maxQueueSize);
            _cancellationTokenSource = new CancellationTokenSource();
            _processQueueTask = Task.Run(ProcessLogQueue, _cancellationTokenSource.Token);
        }

        public void Log(LogLevel level, string message, Exception exception = null)
        {
            if (level < _minimumLevel) return;

            var logEntry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level.ToString(),
                Message = message,
                Exception = exception != null ? new ExceptionInfo(exception) : null
            };

            try
            {
                if (!_logQueue.TryAdd(logEntry, TimeSpan.FromMilliseconds(100)))
                {
                    // If queue is full, write directly to file
                    WriteLogEntry(logEntry);
                }
            }
            catch (Exception ex)
            {
                // If queue is full or other error, write directly to file
                WriteLogEntry(logEntry);
                WriteLogEntry(new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = LogLevel.Error.ToString(),
                    Message = "Failed to queue log entry",
                    Exception = new ExceptionInfo(ex)
                });
            }
        }

        private async Task ProcessLogQueue()
        {
            var batch = new List<LogEntry>();
            var lastFlush = DateTime.UtcNow;

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    // Try to get an item from the queue
                    if (_logQueue.TryTake(out var entry, 100, _cancellationTokenSource.Token))
                    {
                        batch.Add(entry);
                    }

                    // Flush if we have enough entries or enough time has passed
                    if (batch.Count >= 100 || DateTime.UtcNow - lastFlush >= _flushInterval)
                    {
                        await FlushBatchAsync(batch);
                        batch.Clear();
                        lastFlush = DateTime.UtcNow;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Log error to file directly
                    WriteLogEntry(new LogEntry
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = LogLevel.Error.ToString(),
                        Message = "Error processing log queue",
                        Exception = new ExceptionInfo(ex)
                    });
                }
            }

            // Flush any remaining entries
            if (batch.Count > 0)
            {
                await FlushBatchAsync(batch);
            }
        }

        private async Task FlushBatchAsync(List<LogEntry> batch)
        {
            try
            {
                var logLines = batch.Select(entry => JsonConvert.SerializeObject(entry) + Environment.NewLine);
                await File.AppendAllLinesAsync(_logFilePath, logLines);
            }
            catch (Exception ex)
            {
                // If async write fails, try synchronous write
                WriteLogEntry(new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = LogLevel.Error.ToString(),
                    Message = "Failed to flush log batch asynchronously",
                    Exception = new ExceptionInfo(ex)
                });

                foreach (var entry in batch)
                {
                    WriteLogEntry(entry);
                }
            }
        }

        private void WriteLogEntry(LogEntry entry)
        {
            try
            {
                var logLine = JsonConvert.SerializeObject(entry) + Environment.NewLine;
                File.AppendAllText(_logFilePath, logLine);
            }
            catch (Exception ex)
            {
                // If all else fails, write to console
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
                Console.WriteLine($"Original log entry: {JsonConvert.SerializeObject(entry)}");
            }
        }

        public void LogDebug(string message) => Log(LogLevel.Debug, message);
        public void LogInformation(string message) => Log(LogLevel.Information, message);
        public void LogWarning(string message) => Log(LogLevel.Warning, message);
        public void LogError(string message, Exception exception = null) => Log(LogLevel.Error, message, exception);
        public void LogCritical(string message, Exception exception = null) => Log(LogLevel.Critical, message, exception);

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            try
            {
                _processQueueTask.Wait();
            }
            catch (AggregateException)
            {
                // Ignore cancellation exceptions
            }
            _cancellationTokenSource.Dispose();
            _logQueue.Dispose();
        }

        private class LogEntry
        {
            public DateTime Timestamp { get; set; }
            public string Level { get; set; }
            public string Message { get; set; }
            public ExceptionInfo Exception { get; set; }
        }

        private class ExceptionInfo
        {
            public string Type { get; set; }
            public string Message { get; set; }
            public string StackTrace { get; set; }

            public ExceptionInfo(Exception ex)
            {
                Type = ex.GetType().Name;
                Message = ex.Message;
                StackTrace = ex.StackTrace;
            }
        }
    }
} 