using System;

namespace LastFM.ReaderCore
{

    public interface IErrorLogger
    {
        void LogError(Exception ex, string infoMessage);
    }

    public class ErrorLogger : IErrorLogger
    {
        public void LogError(Exception ex, string infoMessage)
        {
            Console.WriteLine ("Exception: {0} - Info: {1}", ex.Message.ToString(), infoMessage);
        }
    }
}