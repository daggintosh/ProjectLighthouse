using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using Kettu;
using LBPUnion.ProjectLighthouse.Logging;

namespace LBPUnion.ProjectLighthouse.Types.Settings
{
    [Serializable]
    public class ServerSettings
    {
        static ServerSettings()
        {
            if (ServerStatics.IsUnitTesting) return; // Unit testing, we don't want to read configurations here since the tests will provide their own

            if (File.Exists(ConfigFileName))
            {
                string configFile = File.ReadAllText(ConfigFileName);

                Instance = JsonSerializer.Deserialize<ServerSettings>(configFile) ?? throw new ArgumentNullException(nameof(ConfigFileName));

                if (Instance.ConfigVersion >= CurrentConfigVersion) return;

                Logger.Log($"Upgrading config file from version {Instance.ConfigVersion} to version {CurrentConfigVersion}", LoggerLevelConfig.Instance);
                Instance.ConfigVersion = CurrentConfigVersion;
                configFile = JsonSerializer.Serialize
                (
                    Instance,
                    typeof(ServerSettings),
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                    }
                );

                File.WriteAllText(ConfigFileName, configFile);
            }
            else
            {
                string configFile = JsonSerializer.Serialize
                (
                    new ServerSettings(),
                    typeof(ServerSettings),
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                    }
                );

                File.WriteAllText(ConfigFileName, configFile);

                Logger.Log
                (
                    "The configuration file was not found. " +
                    "A blank configuration file has been created for you at " +
                    $"{Path.Combine(Environment.CurrentDirectory, ConfigFileName)}",
                    LoggerLevelConfig.Instance
                );

                Environment.Exit(1);
            }
        }

        #region Meta

        [NotNull]
        public static ServerSettings Instance;

        public const int CurrentConfigVersion = 2;

        [JsonPropertyName("ConfigVersionDoNotModifyOrYouWillBeSlapped")]
        public int ConfigVersion { get; set; } = CurrentConfigVersion;

        public const string ConfigFileName = "lighthouse.config.json";

        #endregion Meta

        public string InfluxOrg { get; set; } = "";
        public string InfluxBucket { get; set; } = "";
        public string InfluxToken { get; set; } = "";

        public string EulaText { get; set; } = "";

        public string DbConnectionString { get; set; } = "server=127.0.0.1;uid=root;pwd=lighthouse;database=lighthouse";
    }
}