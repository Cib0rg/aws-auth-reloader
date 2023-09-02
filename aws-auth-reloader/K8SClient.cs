using k8s.Models;
using KubeOps.KubernetesClient;
using NLog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AwsAuthSync
{
    internal class K8SClient
    {
        #region Variables
        // Properties that should not be changed
        private readonly string cmNs = "kube-system";

        private readonly string cmName = "aws-auth";

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly KubernetesClient _client = new();
        #endregion

        #region Methods

        public async Task<List<AWSUser>> GetCurrentAWSAuthUsers()
        {
            logger.Debug("Fetching current users from K8S");
            var cm = await _client.Get<V1ConfigMap>(cmName, cmNs);

            var deserializer = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();

            var k8sRoleList = deserializer.Deserialize<List<MapRole>>(cm.Data["mapRoles"]);

            logger.Debug("Converting fetched users to internal entities");

            List<AWSUser> genericRoleList = new();

            foreach (var role in k8sRoleList)
            {
                logger.Debug("Converting {0} aka {1}", role.Username, role.Rolearn);
                AWSUser user = new()
                {
                    Arn = role.Rolearn,
                    Name = role.Username,
                    Groups = role.Groups
                };
                genericRoleList.Add(user);
            }

            return genericRoleList;
        }

        public V1ConfigMap CreateConfigMap(List<AWSUser> users)
        {
            V1ObjectMeta cmMetadata = new()
            {
                Name = cmName,
                NamespaceProperty = cmNs,
            };

            List<MapRole> mapRoles = new();
            foreach (var user in users)
            {
                MapRole role = new MapRole
                {
                    Rolearn = user.Arn,
                    Username = user.Name,
                    Groups = user.Groups
                };

                mapRoles.Add(role);
            }
            // Converting 
            Dictionary<string, string> groups = new();

            string roles = string.Empty;

            foreach (var role in mapRoles)
            {
                roles += string.Format("- {0}\n", role.ToString());
            }

            groups["mapRoles"] = roles;

            logger.Debug("ConfigMap data will look like this:\n{0}", roles);
            V1ConfigMap result = new()
            {
                Metadata = cmMetadata,
                Data = groups
            };

            return result;
        }

        public async void ApplyConfigMap(V1ConfigMap cm)
        {
            if (Settings.DryRunMode)
            {
                logger.Info("DryRun mode - changes will not be applied");
                return;
            }
            else
            {
                try
                {
                    logger.Debug("Updating configmap {0} in namespace {1}", new object[] { cm.Metadata.Name, cm.Metadata.Namespace() });
                    await _client.Update(cm);
                }
                catch (Exception ex)
                {
                    logger.Error("Some exception happens: {0}", ex.ToString());
                }
            }
        }

        #endregion
    }
}
