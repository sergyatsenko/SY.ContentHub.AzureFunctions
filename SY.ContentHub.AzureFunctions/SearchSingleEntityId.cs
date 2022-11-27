//#r "Newtonsoft.Json"

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Stylelabs.M.Base.Querying;
using Stylelabs.M.Base.Querying.Linq;
using Stylelabs.M.Sdk.WebClient;
using Stylelabs.M.Sdk.WebClient.Authentication;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace SY.ContentHub.AzureFunctions
{
    public static class SearchSingleEntityId
    {
        [FunctionName("SearchSingleEntityId")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processing a request...");
            var content = req.Content;
            string requestBody = content.ReadAsStringAsync().Result;
            log.Info($"Request body: {requestBody}");
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            var entityDefinitionName = Utils.GetHeaderValue(req.Headers, "entityDefinitionName");
            var searchFieldName = Utils.GetHeaderValue(req.Headers, "searchFieldName");
            var searchFieldValue = Utils.GetHeaderValue(req.Headers, "searchFieldValue");

			IWebMClient client = Utils.InitClient(req);

			var entityQuery = Query.CreateQuery(entities =>
                         from e in entities
                         where e.Property(searchFieldName) == searchFieldValue
                         where e.DefinitionName == entityDefinitionName
                         select e);

            try
            {
                var results = (await client.Querying.QueryAsync(entityQuery))?.Items;
                log.Info($"results: {results}, count: {results?.Count}");
                if (results != null && results.Any())
                {
                    var id = results.First().Id;
                    if (id != null && id > 0)
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(id.ToString())
                        };
                    };
                }
            }
            catch (Exception ex)
            {
                log.Info($"'Error message':{ex.Message}");
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent($"'Error message':{ex.Message}")
                };
            }

            log.Info($"not found");
            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("404: Not found entity.") // $"'enityDefinitionName':{entityDefinitionName}, 'searchFieldName':{searchFieldName}, 'searchFieldValue':{searchFieldValue}")
            };
        }
    }
}
