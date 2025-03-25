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
            if (ex != null)
            {
                Console.WriteLine("Exception: {0} - Info: {1}", ex.Message, infoMessage);
            }
            else
            {
                Console.WriteLine("Info: {0}", infoMessage);
            }
        }
    }
}