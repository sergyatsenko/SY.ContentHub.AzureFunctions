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

            Uri endpoint = new Uri(Utils.GetHeaderValue(req.Headers, "ContentHubUrl")); // "https://xc403.stylelabsdemo.com/");

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

            var relations = new Dictionary<string, List<dynamic>>();
            foreach (var entityRelation in entityRelations)
            {
                if (!string.IsNullOrEmpty(entityRelation))
                {
                    var relation = entity.GetRelation(entityRelation);
                    var relatedIds = relation?.GetIds();
                    if (relatedIds.Count > 0)
                    {
                        var relationData = new List<dynamic>();
                        foreach (var relatedId in relatedIds)
                        {
                            IEntity relatedEntity = await client.Entities.GetAsync(relatedId, EntityLoadConfiguration.Full);
                            if (relatedEntity != null)
                            {
                                var entityData = new
                                { 
                                    Properties = ExtractEntityData(relatedEntity),
                                    Renditions = entity.Renditions
                                };
                                relationData.Add(entityData);
                                //relationData.Add(ExtractEntityData(relatedEntity));
                            }
                        }
                        if (relationData.Count > 0)
                        {
                            relations.Add(entityRelation, relationData);
                        }
                    }
                }
            }

            //var entityJson = JsonConvert.SerializeObject(entity);
            //log.Info($"entityJson: {entityJson}");
            //var relationsJson = JsonConvert.SerializeObject(relations);
            //log.Info($"relationsJson: {relationsJson}");

            var result = new
            {
                Properties = ExtractEntityData(entity),
                Renditions = entity.Renditions,
                Relations = relations
            };
            var resultJson = JsonConvert.SerializeObject(result);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                //Content = new StringContent($"{{\"entity\":{entityJson},\"relations\":{relationsJson} }}", Encoding.UTF8, "application/json")
                Content = new StringContent(resultJson, Encoding.UTF8, "application/json")
            };

            //var value = entity.GetPropertyValue<string>("ProductName");
            //log.LogInformation($"Entity product name: {value}");
            //foreach (var rel in entity.Relations)
            //{
            //    log.LogInformation($"Relation name: {rel.Name}, Relation definition type: {rel.DefinitionType}");
            //}
            //IRelation relation = entity.GetRelation("PCMProductFamilyToProduct");
            //var relatedIds = relation?.GetIds();
            //string relatedValues = "";
            //if (relatedIds != null)
            //{
            //    foreach (var relatedId in relatedIds)
            //    {
            //        log.LogInformation($"PCMProductFamilyToProduct related ID: {relatedId}");
            //        IEntity relatedEntity = await client.Entities.GetAsync(relatedId);
            //        if (relatedEntity != null)
            //        {
            //            var relatedValue = relatedEntity.GetPropertyValue<string>("ProductFamilyName");
            //            if (relatedValue != null)
            //            {
            //                relatedValues += relatedValue;
            //            }
            //        }
            //    }
            //}
            //else
            //{
            //    log.LogInformation("no PCMProductFamilyToProduct relations found");
            //}

            //relation = entity.GetRelation("PCMProductToMasterAsset");
            //relatedIds = relation?.GetIds();

            //if (relatedIds != null)
            //{
            //    foreach (var relatedId in relatedIds)
            //    {
            //        log.LogInformation($"PCMProductToMasterAsset related ID: {relatedId}");
            //        IEntity relatedEntity = await client.Entities.GetAsync(relatedId);
            //        if (relatedEntity != null)
            //        {
            //            var relatedValue = relatedEntity.GetPropertyValue<string>("ProductFamilyName");
            //            if (relatedValue != null)
            //            {
            //                relatedValues += relatedValue;
            //            }
            //        }
            //    }
            //}
            //else
            //{
            //    log.LogInformation("no PCMProductFamilyToProduct relations found");
            //}
            //string name = req.Query["name"];

            //string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            //dynamic data = JsonConvert.DeserializeObject(requestBody);
            //name = name ?? data?.name;

            //string responseMessage = string.IsNullOrEmpty(name)
            //    ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
            //    : $"Hello, {name}. This HTTP triggered function executed successfully.";

            //return new OkObjectResult(string.Format("productName: {0}, related values: {1}", value, relatedValues));

        }

        private static Dictionary<string, object> ExtractEntityData(IEntity entity)
        {
            var e = new Dictionary<string, object>();
            e.Add("Id", entity.Id);
            e.Add("Identifier", entity.Identifier);
            e.Add("DefinitionName", entity.DefinitionName);
            e.Add("CreatedBy", entity.CreatedBy);
            e.Add("CreatedOn", entity.CreatedOn);
            e.Add("IsDirty", entity.IsDirty);
            e.Add("IsNew", entity.IsNew);
            e.Add("IsRootTaxonomyItem", entity.IsRootTaxonomyItem);
            e.Add("IsPathRoot", entity.IsPathRoot);
            e.Add("IsSystemOwned", entity.IsSystemOwned);
            e.Add("Version", entity.Version);
            e.Add("Cultures", entity.Cultures);
            foreach (var property in entity.Properties)
            {
                //var propertyValue = entity.GetProperty<ICultureInsensitiveProperty>(property.Name)?.GetValue();
                try
                {
                    var propertyValue = entity.GetPropertyValue(property.Name);
                    e.Add(property.Name, propertyValue);
                }
                catch (Exception ex) when (ex.Message == "Culture is required for culture sensitive properties.")
                {
                    var propertyValue = entity.GetPropertyValue(property.Name, CultureInfo.GetCultureInfo("en-US"));
                    e.Add(property.Name, propertyValue);
                }
               
            }
            
            return e;
        }

    }
}
