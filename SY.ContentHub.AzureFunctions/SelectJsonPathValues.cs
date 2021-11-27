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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace SY.ContentHub.AzureFunctions
{
    public static class SelectJsonPathValues
    {
        [FunctionName("SelectJsonPathValues")]
        //public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processing a request...");

            var content = req.Content;
            string requestBody = content.ReadAsStringAsync().Result;
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            Dictionary<string, string> output = new Dictionary<string, string>();
            foreach (var header in req.Headers)
            {
                string key = header.Key;

                if (!key.StartsWith("_")) continue;
                string value = string.Join(",", header.Value.ToArray());
                if (key.Equals("_datasource", StringComparison.OrdinalIgnoreCase))
                {
                    output.Add("_datasource", value);
                    continue;
                }
                log.Info(key);
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) continue;
                string token = data.SelectToken(value);
                if (!string.IsNullOrEmpty(token))
                {
                    output.Add(key.Substring(1, key.Length - 1), token);
                }
            }

            var resultJson = JsonConvert.SerializeObject(output);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(resultJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
