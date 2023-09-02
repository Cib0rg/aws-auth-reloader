using Amazon;
using NLog;

namespace AwsAuthSync
{
    internal static class Settings
    {
        private static readonly string basePrefix = "cluster:";

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static LogLevel LogLevel { 
            get 
            {
                string logLevel;
                try
                {
#pragma warning disable CS8602 // Разыменование вероятной пустой ссылки.
                    logLevel = Environment.GetEnvironmentVariable("LOG_LEVEL").ToLowerInvariant();
#pragma warning restore CS8602 // Разыменование вероятной пустой ссылки.
                }
                catch  (NullReferenceException)
                {
                    logLevel = "info";
                }

                LogLevel resultLevel = logLevel switch
                {
                    "off" => LogLevel.Off,
                    "trace" => LogLevel.Trace,
                    "debug" => LogLevel.Debug,
                    "info" => LogLevel.Info,
                    "warn" => LogLevel.Warn,
                    "error" => LogLevel.Error,
                    "fatal" => LogLevel.Fatal,
                    _ => LogLevel.Info,
                };
                return resultLevel;
            } 
        }

        public static RegionEndpoint AWSRegion
        {
            get
            {
                string? value = Environment.GetEnvironmentVariable("AWS_REGION");
                if (String.IsNullOrEmpty(value)) { throw new ArgumentNullException("AWS_REGION"); }
                return RegionEndpoint.GetBySystemName(value);
            }
        }

        /// <summary>
        /// Prefix that allow to find cluster-related tags. Can be overrided with env variable. Default is "cluster:"
        /// </summary>
        public static string TagPrefix
        {
            get
            {
                try
                {
                    string? result = Environment.GetEnvironmentVariable("TAG_PREFIX");
                    logger.Debug("TAG_PREFIX env variable are set to {0}", result);
                    if (!string.IsNullOrEmpty(result)) 
                    {
                        logger.Debug("Using {0} as TAG_PREFIX", result);
                        return result; 
                    }
                }
                catch (Exception ex)
                {
                    logger.Warn("There was an exception when I tried to get TAG_PREFIX value: {0}", ex.ToString());
                }
                logger.Debug("TAG_PREFIX was empty, using {0} as TAG_PREFIX", basePrefix);
                return basePrefix;
            }
        }

        /// <summary>
        /// Dry-run switch (output only, w/o commits). Default if false;
        /// </summary>
        public static bool DryRunMode
        {
            get
            {
                try
                {
                    if (Environment.GetEnvironmentVariable("DRY_RUN").ToLowerInvariant() == "false")
                    {
                        logger.Info("Shall continue in fully operational mode");
                        return false;
                    }
                }
                catch
                {
                    logger.Debug("Shall continue in dry-run mode");
                    return true;
                }
                logger.Debug("Shall continue in dry-run mode");
                return true;
            }
        }

        public static int RefreshInterval
        {
            get
            {
                if (int.TryParse(Environment.GetEnvironmentVariable("REFRESH_INTERVAL"), out int refreshInterval))
                {
                    return refreshInterval;
                }
                else
                    return 900; //seconds
            }
        }

        /// <summary>
        /// Cluster name to filter results fetched from AWS. Mandatory variable!
        /// </summary>
        public static string ClusterName
        {
            get
            {
                try
                {
                    string? result = Environment.GetEnvironmentVariable("CLUSTER_NAME");
                    logger.Debug("CLUSTER_NAME env variable are set to {0}", result);
                    return result;
                }
                catch (Exception ex)
                {
                    logger.Warn("There was an exception when I tried to get CLUSTER_NAME value: {0}", ex.ToString());
                    logger.Error("Please set CLUSTER_NAME variable");
                    throw new ArgumentNullException(null, "CLUSTER_NAME variable is empty");
                }
            }
        }

        /// <summary>
        /// Fail-safe protocol!
        /// Here we can declare coma-separated entities that will be protected from deletion.
        /// Can be used if we are not 200% sure that we doing all right - at least VIP entities will stay in place.
        /// </summary>
        public static List<string> ProtectedEntities
        {
            get
            {
                using (var sr = new StreamReader("protected.txt"))
                {
                    List<string> entities = new();
                    var result = sr.ReadToEnd();
                    if (!string.IsNullOrEmpty(result))
                    {
                        entities.AddRange(result.Split(Environment.NewLine ));
                        string logEntry = string.Empty;
                        foreach (var entity in entities)
                        {
                            logEntry += entity + Environment.NewLine;
                        }
                        logger.Debug("Here is a list of protected entities:\n{0}", logEntry);
                    }
                    else
                    {
                        logger.Debug("No entities to protect");
                    }
                    return entities;
                }
            }
        }
    }
}
