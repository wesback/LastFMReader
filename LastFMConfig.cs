using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace LastFM.ReaderCore
{
    public class LastFMConfig
    {
        public static IConfiguration _config =  new ConfigurationBuilder()
        #if DEBUG
            .AddJsonFile($"appsettings.debug.json", false, true)
        #else
            .AddJsonFile("appsettings.json", false, true)
        #endif
            .Build();
        public static string getConfig(string configName)
        {
            return _config[configName];
        }

        public static List<string> getConfigArray(string configName, string configItem)
        {
            List<string> configItems = new List<string>();
            IConfigurationSection configArray = _config.GetSection(configName);
            foreach (var c in configArray.GetChildren())
            {
                configItems.Add(c[configItem].ToString());
            }

            return configItems;
        }
    }
}