//#r "Newtonsoft.Json"

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Stylelabs.M.Framework.Essentials.LoadConfigurations;
using Stylelabs.M.Sdk.Contracts.Base;
using Stylelabs.M.Sdk.Models.Base;
using Stylelabs.M.Sdk.WebClient;
using Stylelabs.M.Sdk.WebClient.Authentication;
using SY.ContentHub.AzureFunctions.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SY.ContentHub.AzureFunctions
{
	/// <summary>
	/// Find Entities by single field value.
	/// Optionally include related Entities using specified relations.
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

				var entity = await client.Entities.GetAsync(requestObject.entityId, EntityLoadConfiguration.Full);
				if (entity == null)
				{
					return req.CreateResponse(HttpStatusCode.NotFound, $"Entity with Id {requestObject.entityId} not found.");
				}

				var relations = await Utils.GetRelatedEntities(client, entity, requestObject.relations, log);

				var result = new EntityInfo
				{
					Entity = Utils.ExtractEntityData(entity),
					Relations = relations
				};

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

