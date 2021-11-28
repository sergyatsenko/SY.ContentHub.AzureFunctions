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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SY.ContentHub.AzureFunctions
{
    public static class RenderTemplate
    {
        [FunctionName("RenderTemplate")]
        //public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req, ILogger log)
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req, TraceWriter log)
        {
            log.Info("C# HTTP trigger function processing a request...");

            var templateString = Utils.GetHeaderValue(req.Headers, "Template");
            log.Info($"templateString: {templateString}");

            var content = req.Content;
            string requestBody = content.ReadAsStringAsync().Result;
            log.Info($"requestBody: {requestBody}");
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            MatchCollection jsonPathMatches = Regex.Matches(templateString, @"{{(.*?)}}", RegexOptions.IgnoreCase);
            foreach (Match jsonPathMatch in jsonPathMatches)
            {
                if (jsonPathMatch.Value.Length < 5) continue;
                var jsonPath = jsonPathMatch.Value.Substring(2, jsonPathMatch.Value.Length - 4);
                string value = data.SelectToken(jsonPath);
                if (!string.IsNullOrEmpty(value))
                {
                    templateString = templateString.Replace(jsonPathMatch.Value, value);
                }
                log.Info($"value match: {value}");
                log.Info($"templateString: {templateString}");
            }

            MatchCollection valueMatches = Regex.Matches(templateString, @"\[\[(.*?)\]\]", RegexOptions.IgnoreCase);
            foreach (Match valueMatch in valueMatches)
            {
                if (valueMatch.Value.Length < 5) continue;
                var value = valueMatch.Value.Substring(2, valueMatch.Value.Length - 4);
                templateString = templateString.Replace(valueMatch.Value, value);
                log.Info($"templateString: {templateString}");
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(templateString, Encoding.UTF8, "application/json")
            };
        }
    }
}
