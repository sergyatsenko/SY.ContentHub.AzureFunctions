//#r "Newtonsoft.Json"

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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

            var output = new Dictionary<string, object>();
            foreach (var header in req.Headers)
            {
                string key = header.Key;
                if (string.IsNullOrEmpty(key)) continue;

                string value = string.Join(",", header.Value.ToArray());
                if (string.IsNullOrEmpty(value) || value.Length < 5) continue;

                var valueStripped = value.Substring(2, value.Length - 4);

                if (value.StartsWith("[[") && value.EndsWith("]]"))
                {
                    output.Add(key, valueStripped);
                }
                else if (value.StartsWith("{{") && value.EndsWith("}}"))
                {
                    string token = data.SelectToken(valueStripped);
                    if (!string.IsNullOrEmpty(token))
                    {
                        output.Add(key, token);
                    }
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
