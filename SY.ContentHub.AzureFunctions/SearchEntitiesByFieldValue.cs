//#r "Newtonsoft.Json"

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Stylelabs.M.Base.Querying.Linq;
using Stylelabs.M.Framework.Essentials.LoadConfigurations;
using Stylelabs.M.Sdk.Contracts.Base;
using Stylelabs.M.Sdk.WebClient;
using SY.ContentHub.AzureFunctions.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SY.ContentHub.AzureFunctions
{
	/// <summary>
	/// Find Entities by field value and definition name
	/// Optionally include related Entities using specified relations.
	/// </summary>
	public static partial class SearchEntitiesByFieldValue
	{
		[FunctionName("SearchEntitiesByFieldValue")]
		public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req, TraceWriter log)
		{
			//Read and parse request payload
			log.Info("SearchEntitiesByFieldValue invoked.", MethodBase.GetCurrentMethod().DeclaringType.Name);
			var content = req.Content;
			string requestBody = content.ReadAsStringAsync().Result;
			log?.Info($"Request body: {requestBody}", MethodBase.GetCurrentMethod().DeclaringType.Name);
			var requestObject = JsonConvert.DeserializeObject<SearchEntitiesByFieldValueRequest>(requestBody);

			try
			{
				//Initialize CH Web SDK client
				IWebMClient client = Utils.InitClient(req);

				//Search for Entities matching the requested field value
				IList<IEntity> entities;

				if (string.IsNullOrEmpty(requestObject.entitySearch.entitySearchField.fieldValue) || string.IsNullOrEmpty(requestObject.entitySearch.entitySearchField.fieldName))
				{
					entities = await Utils.SearcEntities(client,
											(entities =>
											from e in entities
											where e.DefinitionName == requestObject.entitySearch.entitySearchField.definitionName
											select e),
											EntityLoadConfiguration.Full, log);
				}
				else
				{
					entities = await Utils.SearcEntities(client,
											(entities =>
											from e in entities
											where e.Property(requestObject.entitySearch.entitySearchField.fieldName) == requestObject.entitySearch.entitySearchField.fieldValue
											where e.DefinitionName == requestObject.entitySearch.entitySearchField.definitionName
											select e),
											EntityLoadConfiguration.Full, log);
				}


				if (entities == null || entities.Count == 0)
				{
					return req.CreateResponse(HttpStatusCode.NotFound, $"No entities found for field {requestObject.entitySearch.entitySearchField.fieldName} with value {requestObject.entitySearch.entitySearchField.fieldValue}");
				}


				var response = new List<EntityInfo>(); 
				foreach (var entityObject in entities)
				{
					var relations = await Utils.GetRelatedEntities(client, entityObject, requestObject.relations, requestObject.resolveToSolrFieldNames, log);

					var entity = new EntityInfo
					{
						Entity = Utils.ExtractEntityData(entityObject),
						Relations = relations
					};

					response.Add(entity);
				}

				var resultJson = JsonConvert.SerializeObject(response);
				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(resultJson, Encoding.UTF8, "application/json")
				};

			}
			catch (ArgumentException argEx)
			{
				var message = $"Invalid request body or missing required parameters. Error message: {argEx.Message}";
				log.Info(message, MethodBase.GetCurrentMethod().DeclaringType.Name);
				return new HttpResponseMessage(HttpStatusCode.BadRequest)
				{
					Content = new StringContent($"Invalid request body or missing required parameters. Error message: {argEx.Message}")
				};
			}
			catch (Exception ex)
			{
				var message = $"'Error message':{ex.Message}";
				log.Info(message, MethodBase.GetCurrentMethod().DeclaringType.Name);
				return new HttpResponseMessage(HttpStatusCode.InternalServerError)
				{
					Content = new StringContent($"'Error message':{ex.Message}")
				};
			}
		}
	}
}

