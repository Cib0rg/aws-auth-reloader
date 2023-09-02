using AwsAuthSync;
using NLog;
using System.Timers;


internal class Program
{
    private static System.Timers.Timer timer = new();
    private static int reconsillationInterval = Settings.RefreshInterval;

    static Logger logger = LogManager.GetCurrentClassLogger();

    private static async Task MainLoop()
    {
        var k8sClient = new K8SClient();

        AWSFetcher fetcher = null;

        // Error flag
        bool skipNextSteps = false;

        try
        {
            fetcher = new AWSFetcher(Settings.AWSRegion);
        }
        catch (ArgumentNullException ex)
        {
            logger.Error("Some of mandatory params are missing?\n{0}", ex.ToString());
            if (Settings.AWSRegion == null) logger.Error("AWS Region cannot be empty");
            skipNextSteps = true;
        }

        #region Fetching from AWS
        if (!skipNextSteps)
        {
            try
            {
                await fetcher.FetchUserData();
            }
            catch (Exception ex)
            {
                logger.Error("Cannot fetch user data: {0}", ex.ToString());
                skipNextSteps = true;
            }
        }
        else
        {
            logger.Warn("Skipping AWS fetch in this cycle because of previous errors");
        }
        #endregion

        #region Working with fetched data
        if (!skipNextSteps)
        {
            // Getting Data from K8S
            var k8sUsers = await k8sClient.GetCurrentAWSAuthUsers();

            // Getting things together
            // K8S userlist is "source of truth" that we will change according to AWS and protection settings

            // Of course we are operating with ARNs - this is our one and only ID for comparsion

            // Users that exist in K8S and not in K8S (minus protected items of course)
            var protectedUsers = Settings.ProtectedEntities;
            var usersToDelete = k8sUsers.Where(x => !fetcher.AWSUsers.Any(l => x.Arn == l.Arn)).Where(x => !protectedUsers.Any(l => x.Arn == l));

            // Users that exist in AWS and not in K8S
            var usersToAdd = fetcher.AWSUsers.Where(x => !k8sUsers.Any(l => x.Arn == l.Arn));

            // Users that have changes in their groups
            var awsGroupsUsers = from ku in k8sUsers
                                 from au in fetcher.AWSUsers
                                 where (ku.Groups.Count > 0 && au.Groups.Count > 0 && au.Arn == ku.Arn && ku.Groups != au.Groups)
                                 select au.Arn;

            var k8sGroupsUsers = from ku in k8sUsers
                                 from au in fetcher.AWSUsers
                                 where (ku.Groups.Count > 0 && au.Groups.Count > 0 && au.Arn == ku.Arn && ku.Groups != au.Groups)
                                 select ku.Arn;

            // Dotnet crutches
            var awsGroupsUsersList = awsGroupsUsers.ToList();
            var k8sGroupsUsersList = k8sGroupsUsers.ToList();

            List<string> usersWithDifferentGroups = new();
            usersWithDifferentGroups.AddRange(awsGroupsUsersList);
            usersWithDifferentGroups.AddRange(k8sGroupsUsersList);

            // And finally we have ARNs of entities with different groups in K8S and AWS (only entities that already exist)
            var usersWithDifferentGroupsFiltered = usersWithDifferentGroups.Distinct().ToList();

            // And we have protected entities - we should avoid any changes to their properties
            foreach (var user in protectedUsers)
            {
                if (usersWithDifferentGroupsFiltered.Contains(user))
                {
                    usersWithDifferentGroupsFiltered.Remove(user);
                    logger.Debug("Removing user {0} from GroupDiff - he have a protection from above", user);
                }
            }

            // Debug output
            if (Settings.LogLevel == LogLevel.Debug)
            {
                foreach (var u in usersToAdd)
                {
                    logger.Debug("We have {0} to add", u.Arn);
                }

                foreach (var u in usersToDelete)
                {
                    logger.Debug("We have {0} to delete", u.Arn);
                }

                foreach (var user in usersWithDifferentGroupsFiltered)
                {
                    logger.Debug("Users with different group: {0}", user);
                }
            }

            // Putting all together
            // First: creating a clone of K8SUserList
            List<AWSUser> finalUserList = new();
            finalUserList.AddRange(k8sUsers);

            // Then we need to delete from K8S users that was marked for deletion (usersToDelete)
            foreach (var user in usersToDelete)
            {
                var delUser = finalUserList.Find(x => x.Arn == user.Arn);
                finalUserList.Remove(delUser);
            }

            // Now we need to add users that not exists
            finalUserList.AddRange(usersToAdd);

            // And we need to modify user groups too
            // Remember: Source of truth are in AWS, so we can simply replace current groups with list from AWS
            foreach (var user in usersWithDifferentGroupsFiltered)
            {
                // relatively simple, is'nt?
                finalUserList.Find(x => x.Arn == user).Groups = fetcher.AWSUsers.Find(x => x.Arn == user).Groups;
            }

            // Now we should do some magic - we need to form configmap with this entities
            // Outsourcing this to k8s client. Srsly, it's it job
            var cm = k8sClient.CreateConfigMap(finalUserList);

            // And applying configmap (will do nothing if in dryrun mode)
            k8sClient.ApplyConfigMap(cm);
        }
        else
        {
            logger.Warn("Skipping K8S part in this cycle because of previous errors");
        }

        #endregion
    }
    static async Task Main(string[] args)
    {
        // Configuring logging
        LogManager.Setup().LoadConfiguration(builder => {
            builder.ForLogger().FilterMinLevel(Settings.LogLevel).WriteToConsole();
        });

        await MainLoop();

        timer.AutoReset = true;
        timer.Interval = reconsillationInterval * 1000; // Converting this to seconds
        timer.Elapsed += timerElapsed;

        timer.Start();

        while (true)
        {
            // Doing nothing just to prevent exit from main loop
        }
    }

    static async void timerElapsed(object sender, ElapsedEventArgs e)
    {
        logger.Info("Doing one more run in main loop");
        await MainLoop();

        logger.Info("Falling asleep");
    }
}