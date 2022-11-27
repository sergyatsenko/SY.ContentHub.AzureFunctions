//#r "Newtonsoft.Json"

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Stylelabs.M.Framework.Essentials.LoadConfigurations;
using Stylelabs.M.Sdk.WebClient;
using SY.ContentHub.AzureFunctions.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SY.ContentHub.AzureFunctions
{
	/// <summary>
	/// Get Entity by ID and then if found, load entities from specified relations 
	/// </summary>
	public static partial class GetEntityById
	{
		[FunctionName("GetEntityById")]
		public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req, TraceWriter log)
		{
			//Read and parse request payload
			log.Info("GetEntityById invoked.", MethodBase.GetCurrentMethod().DeclaringType.Name);
			var content = req.Content;
			string requestBody = content.ReadAsStringAsync().Result;
			log?.Info($"Request body: {requestBody}", MethodBase.GetCurrentMethod().DeclaringType.Name);
			var requestObject = JsonConvert.DeserializeObject<GetEntityByIdRequest>(requestBody);

			try
			{
				//Initialize WebClient 
				IWebMClient client = Utils.InitClient(req);
				//Get Entity by ID and load all its properties and relations
				var entity = await Utils.GetEntity(client, requestObject.entityId, EntityLoadConfiguration.Full, log);

				if (entity == null)
				{
					return req.CreateResponse(HttpStatusCode.NotFound, $"Entity with Id {requestObject.entityId} not found.");
				}

				//Load all related entities, linked to specified relation fields
				var relations = await Utils.GetRelatedEntities(client, entity, requestObject.relations, requestObject.resolveToSolrFieldNames, log);

				//Create response object
				var result = new EntityInfo
				{
					//Read entity properties into name-value pairs
					Entity = requestObject.resolveToSolrFieldNames ? Utils.ExtractEntityData(entity, Utils.solrFieldNameResolver) : Utils.ExtractEntityData(entity, Utils.defaultFieldNameResolver),
					Relations = relations
				};

				//Serialize response object into JSON and return that as HTTP response
				var resultJson = JsonConvert.SerializeObject(result);
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

