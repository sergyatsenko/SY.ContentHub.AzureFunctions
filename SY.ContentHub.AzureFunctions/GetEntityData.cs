//#r "Newtonsoft.Json"

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Stylelabs.M.Framework.Essentials.LoadConfigurations;
using Stylelabs.M.Sdk.Contracts.Base;
using Stylelabs.M.Sdk.WebClient;
using Stylelabs.M.Sdk.WebClient.Authentication;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace SY.ContentHub.AzureFunctions
{
    public static class GetEntityData
    {
        [FunctionName("GetEntityData")]
        //public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processing a request...");
            var content = req.Content;
            string requestBody = content.ReadAsStringAsync().Result;
            log.Info($"Request body: {requestBody}");
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            var targetEntityIdJsonPath = Utils.GetHeaderValue(req.Headers, "TargetEntityIdJsonPath");
            var entityRelations = Utils.GetHeaderValue(req.Headers, "EntityRelations").Split("|");
            log.Info($"Json paths: targetEntityIdJsonPath-{targetEntityIdJsonPath}, entityRelations-{entityRelations}");
            long targetIdValue = (long)data.SelectToken(targetEntityIdJsonPath);
            log.Info($"targetIdValue: {targetIdValue}");

            string baseUrl = Utils.GetHeaderValue(req.Headers, "ContentHubUrl");

			Uri endpoint = new Uri(baseUrl);

            OAuthPasswordGrant oauth = new OAuthPasswordGrant
            {
                ClientId = Utils.GetHeaderValue(req.Headers, "ClientId"), //"DevIntegration"
                ClientSecret = Utils.GetHeaderValue(req.Headers, "ClientSecret"), //"DevIntegration",
                UserName = Utils.GetHeaderValue(req.Headers, "UserName"), //"Integration",
                Password = Utils.GetHeaderValue(req.Headers, "Password") //"Integration1"
            };

            // Create the Web SDK client
            IWebMClient client = MClientFactory.CreateMClient(endpoint, oauth);
            IEntity entity = await client.Entities.GetAsync(targetIdValue, EntityLoadConfiguration.Full);
            log.Info("Made request to CH...");
            if (entity == null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent($"{{\"targetEntityId\":{targetIdValue}}}")
                };
            }

            var result = new
            {
                Properties = Utils.ExtractEntityData(entity),
                Renditions = entity.Renditions,
                Relations = await Utils.GetRelatedEntities(client, entity, null, log)
			};
            var resultJson = JsonConvert.SerializeObject(result);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                //Content = new StringContent($"{{\"entity\":{entityJson},\"relations\":{relationsJson} }}", Encoding.UTF8, "application/json")
                Content = new StringContent(resultJson, Encoding.UTF8, "application/json")
            };

        }
    }
}
