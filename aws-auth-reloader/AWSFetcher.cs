using Amazon.IdentityManagement.Model;
using Amazon.IdentityManagement;
using Amazon.Runtime;
using Amazon;
using Amazon.Runtime.CredentialManagement;
using System.Text;

namespace AwsAuthSync
{
    internal class AWSFetcher
    {
        public RegionEndpoint RegionEndpoint { get; set; }

        private List<AWSUser> appliedUserList = new();

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public List<AWSUser> AWSUsers { get => appliedUserList; }

        // Constructor for role-based auth
        public AWSFetcher(RegionEndpoint region)
        {
            RegionEndpoint = region;
        }

        public async Task FetchUserData() 
        {
            logger.Debug("Starting to fetch users from AWS");

            IAmazonIdentityManagementService service = new AmazonIdentityManagementServiceClient(FallbackCredentialsFactory.GetCredentials(), RegionEndpoint);

            var listUsersPaginator = service.Paginators.ListUsers(new ListUsersRequest());

            var users = new List<User>();

            await foreach (var response in listUsersPaginator.Responses)
            {
                users.AddRange(response.Users);
            }

            List<AWSUser> retrievedUsers = new();

            foreach (var user in users)
            {
                var awsUser = new AWSUser()
                {
                    Name = user.UserName,
                    Arn = user.Arn
                };

                var tagRequest = new ListUserTagsRequest
                {
                    UserName = user.UserName
                };

                logger.Debug("Requesting tags for {0}", tagRequest.UserName);

                var listTagsPaginator = service.Paginators.ListUserTags(tagRequest);
                await foreach (var response in listTagsPaginator.Responses)
                {
                    awsUser.Tags = response.Tags;
                }
                retrievedUsers.Add(awsUser);
            }

            // Same for roles
            var listRolesPaginator = service.Paginators.ListRoles(new ListRolesRequest());

            var roles = new List<Role>();

            await foreach (var response in listRolesPaginator.Responses)
            {
                roles.AddRange(response.Roles);
            }

            foreach (var role in roles)
            {
                var awsUser = new AWSUser()
                {
                    Name = role.RoleName,
                    Arn = role.Arn
                };

                var tagRequest = new ListRoleTagsRequest
                {
                    RoleName = role.RoleName,
                };

                logger.Debug("Requesting tags for {0}", tagRequest.RoleName);

                var listTagsPaginator = service.Paginators.ListRoleTags(tagRequest);
                await foreach (var response in listTagsPaginator.Responses)
                {
                    awsUser.Tags = response.Tags;
                }
                retrievedUsers.Add(awsUser);
            }


            // Applying filter by tags
            logger.Info("There is {0} users before filters",retrievedUsers.Count);
            var tagFilter = new StringBuilder().Append(Settings.TagPrefix).Append(Settings.ClusterName).ToString();
            var filteredUsers = from user in retrievedUsers
                                from tags in user.Tags
                                where user.Tags.Count > 0
                                where (tags.Key == tagFilter)
                                select user;

            appliedUserList = filteredUsers.ToList();
            logger.Info("After filter there is {0} users that have {1} in tags", new object[] { appliedUserList.Count, tagFilter });

            // Transforming tags to internal groups entity
            foreach (var user in appliedUserList)
            {
                var tagWithGroups = user.Tags.Where<Tag>(x => x.Key == tagFilter).First() as Tag;
                user.Groups = tagWithGroups.Value.Split(' ').ToList();
                logger.Debug("User {0} have this groups: {1}", new object[] { user.Arn, String.Join(",", user.Groups) });
            }
        }
    }
}
