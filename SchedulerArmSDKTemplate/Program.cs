namespace SchedulerArmSDKTemplate
{
    using Microsoft.Azure;
    using Microsoft.Azure.Common.Authentication.Models;
    using Microsoft.Azure.Management.Scheduler;
    using Microsoft.Azure.Management.Scheduler.Models;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Microsoft.Rest;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Threading.Tasks;

    class Program
    {
        private static AzureEnvironment _environment;

        static void Main(string[] args)
        {
            // Set Environment - Choose between Azure public cloud, china cloud and US govt. cloud
            _environment = AzureEnvironment.PublicEnvironments[EnvironmentName.AzureCloud];

            // Get the credentials
            var tokenCloudCreds = GetCredsFromServicePrincipal();
            var tokenCreds = new TokenCredentials(tokenCloudCreds.Token);

            // Use credentials to create Scheduler managment client.
            SchedulerManagementClient schedulerManagementClient = new SchedulerManagementClient(_environment.GetEndpointAsUri(AzureEnvironment.Endpoint.ResourceManager), tokenCreds)
            {
                SubscriptionId = ConfigurationManager.AppSettings["AzureSubscriptionId"]
            };

            CreateJobCollectionAndJobs(schedulerManagementClient);
        }

        private static TokenCloudCredentials GetCredsFromServicePrincipal()
        {
            var subscriptionId = ConfigurationManager.AppSettings["AzureSubscriptionId"];
            var tenantId = ConfigurationManager.AppSettings["AzureTenantId"];
            var clientId = ConfigurationManager.AppSettings["AzureClientId"];
            var clientSecret = ConfigurationManager.AppSettings["AzureClientSecret"];

            // Quick check to make sure we're not running with the default app.config
            if (subscriptionId[0] == '[')
            {
                throw new Exception("You need to enter your appSettings in app.config to run this sample");
            }

            var authority = String.Format("{0}{1}", _environment.Endpoints[AzureEnvironment.Endpoint.ActiveDirectory], tenantId);
            var authContext = new AuthenticationContext(authority);
            var credential = new ClientCredential(clientId, clientSecret);
            var authResult = authContext.AcquireToken(_environment.Endpoints[AzureEnvironment.Endpoint.ActiveDirectoryServiceEndpointResourceId], credential);

            return new TokenCloudCredentials(subscriptionId, authResult.AccessToken);
        }

        private static JobCollectionDefinition BuildJobCollecionDefinition(string jobCollectionName, string location)
        {
            return new JobCollectionDefinition()
            {
                Name = jobCollectionName,
                Location = location,
                Properties = new JobCollectionProperties()
                {
                    Sku = new Sku()
                    {
                        Name = SkuDefinition.Standard,
                    },
                    State = JobCollectionState.Enabled,
                    Quota = new JobCollectionQuota()
                    {
                        MaxRecurrence = new JobMaxRecurrence()
                        {
                            Frequency = RecurrenceFrequency.Minute,
                            Interval = 1,
                        },
                        MaxJobCount = 5
                    }
                }
            };
        }

        private static void CreateJobCollectionAndJobs(SchedulerManagementClient schedulerManagementClient)
        {
            var resourceGroup = ConfigurationManager.AppSettings["AzureResourceGroup"];
            var location = ConfigurationManager.AppSettings["AzureLocation"];
            var jobCollectionNamePrefix = "jc_";
            var jobCollectionName = string.Format("{0}{1}", jobCollectionNamePrefix, Guid.NewGuid().ToString());

            schedulerManagementClient.JobCollections.CreateOrUpdate(
                resourceGroupName: resourceGroup,
                jobCollectionName: jobCollectionName,
                jobCollection: BuildJobCollecionDefinition(jobCollectionName, location));

            CreateOrUpdateJob(schedulerManagementClient, resourceGroup, jobCollectionName, Guid.NewGuid().ToString(), RecurrenceFrequency.Week);
            CreateOrUpdateJob(schedulerManagementClient, resourceGroup, jobCollectionName, Guid.NewGuid().ToString(), RecurrenceFrequency.Hour);
        }

        private static void CreateOrUpdateJob(
            SchedulerManagementClient schedulerManagementClient,
            string resourceGroupName,
            string jobCollectionName,
            string jobName,
            RecurrenceFrequency recurrenceRequency)
        {
            var headers = new Dictionary<string, string>();

            var andomHour = new Random();
            var randomMinute = new Random();
            var randomSecond = new Random();

            schedulerManagementClient.Jobs.CreateOrUpdate(
                resourceGroupName,
                jobCollectionName,
                jobName,
                new JobDefinition()
                {
                    Properties = new JobProperties()
                    {
                        StartTime = DateTime.UtcNow,
                        Action = new JobAction()
                        {
                            Type = JobActionType.Http,
                            Request = new HttpRequest()
                            {
                                Uri = ConfigurationManager.AppSettings["HttpActionUrl"],
                                Method = "GET",
                            },
                            RetryPolicy = new RetryPolicy()
                            {
                                RetryType = RetryType.None,
                            }
                        },
                        Recurrence = new JobRecurrence()
                        {
                            Frequency = recurrenceRequency,
                            Interval = 1,
                            Count = 10000,
                        },
                        State = JobState.Enabled,
                    }
                });
        }
    }
}