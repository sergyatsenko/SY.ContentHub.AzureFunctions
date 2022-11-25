//#r "Newtonsoft.Json"

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Stylelabs.M.Base.Querying.Linq;
using Stylelabs.M.Sdk.WebClient;
using Stylelabs.M.Sdk.WebClient.Authentication;
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
	/// Get a list of IDs for all entities matching the specified search criteria.
	/// </summary>
	public static partial class SearchEntitityIDsByFieldValue
	{
		[FunctionName("SearchEntitityIDsByFieldValue")]
		public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req, TraceWriter log)
		{
			//Read and parse request payload
			log.Info("SearchEntitityIDsByFieldValue invoked.", MethodBase.GetCurrentMethod().DeclaringType.Name);
			var content = req.Content;
			string requestBody = content.ReadAsStringAsync().Result;
			log?.Info($"Request body: {requestBody}", MethodBase.GetCurrentMethod().DeclaringType.Name);
			var requestObject = JsonConvert.DeserializeObject<SearchEntitityIDsByFieldValueRequest>(requestBody);

			try
			{
				//Initialize CH Web SDK client
				var clientInfo = Utils.ExtractClientInfo(req.Headers);
				Uri endpoint = new Uri(clientInfo.baseUrl);
				OAuthPasswordGrant oauth = new OAuthPasswordGrant
				{
					ClientId = clientInfo.clientId,
					ClientSecret = clientInfo.clientSecret,
					UserName = clientInfo.userName,
					Password = clientInfo.password
				};

				IWebMClient client = MClientFactory.CreateMClient(endpoint, oauth);
				
				//Search for Entities matching the requested field value
				IList<long> entityIDs;
				if (string.IsNullOrEmpty(requestObject.entitySearch.entitySearchField.fieldValue) || string.IsNullOrEmpty(requestObject.entitySearch.entitySearchField.fieldName))
				{
					//Search for IDs of all entities matching given definition
					entityIDs = await Utils.SearcEntityIDs(client,
											(entities =>
											from e in entities
											where e.DefinitionName == requestObject.entitySearch.entitySearchField.definitionName
											select e),
											log);
				}
				else
				{
					//Search for IDs of all entities matching given definition AND field value
					entityIDs = await Utils.SearcEntityIDs(client,
											(entities =>
											from e in entities
											where e.Property(requestObject.entitySearch.entitySearchField.fieldName) == requestObject.entitySearch.entitySearchField.fieldValue
											where e.DefinitionName == requestObject.entitySearch.entitySearchField.definitionName
											select e),
											log);
				}
				
				if (entityIDs == null || entityIDs.Count == 0)
				{
					return req.CreateResponse(HttpStatusCode.NotFound, $"No entities found for field {requestObject.entitySearch.entitySearchField.fieldName} with value {requestObject.entitySearch.entitySearchField.fieldValue}");
				}

				var resultJson = JsonConvert.SerializeObject(entityIDs);
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

