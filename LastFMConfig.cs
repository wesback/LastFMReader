using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace LastFM.ReaderCore
{
    public class LastFMConfig
    {
        public static IConfiguration _config =  new ConfigurationBuilder()
        #if DEBUG
            .AddJsonFile($"appsettings.debug.json", false, true)
        #else
            .AddJsonFile($"appsettings.json", false, true)
            .AddEnvironmentVariables()
        #endif
            .Build();
        public static string getConfig(string configName)
        {
            return _config[configName];
        }

        /*
        public static List<string> getConfigArray(string configName)
        {
            List<string> configItems = new List<string>();
            IConfigurationSection configArray = _config.GetSection(configName);
            foreach (var c in configArray.GetChildren())
            {
                configItems.Add(c.Value);
            }

            return configItems;
        }
        */
    }
}