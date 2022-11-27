//#r "Newtonsoft.Json"

//using Microsoft.Azure.WebJobs;
//using Stylelabs.M.Base.Querying.Linq;
//using Microsoft.Azure.WebJobs.Extensions.Http;
//using Microsoft.Azure.WebJobs.Host;
//using Newtonsoft.Json;
//using Stylelabs.M.Framework.Essentials.LoadConfigurations;
//using Stylelabs.M.Sdk.Contracts.Base;
//using Stylelabs.M.Sdk.WebClient;
//using Stylelabs.M.Sdk.WebClient.Authentication;
//using SY.ContentHub.AzureFunctions.Models;
//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using System.Net;
//using System.Net.Http;
//using System.Reflection;
//using System.Threading.Tasks;
//using static Stylelabs.M.Sdk.Errors;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Stylelabs.M.Base.Querying.Linq;
using Stylelabs.M.Framework.Essentials.LoadConfigurations;
using Stylelabs.M.Sdk.Contracts.Base;
using Stylelabs.M.Sdk.WebClient;
using Stylelabs.M.Sdk.WebClient.Authentication;
using SY.ContentHub.AzureFunctions.Models;
using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace SY.ContentHub.AzureFunctions
{
	/// <summary>
	/// Update an existing or create a new Entity in Content Hub
	/// </summary>
	public static partial class UpsertEntity
	{
		[FunctionName("UpsertEntity")]
		public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req, TraceWriter log)
		{
			//Read and parse request payload
			log.Info("UpsertEntity invoked.", MethodBase.GetCurrentMethod().DeclaringType.Name);
			var content = req.Content;
			string requestBody = content.ReadAsStringAsync().Result;
			log?.Info($"Request body: {requestBody}", MethodBase.GetCurrentMethod().DeclaringType.Name);
			var requestObject = JsonConvert.DeserializeObject<UpsertEntityRequest>(requestBody);

			try
			{
				//Initialize CH Web SDK client
				IWebMClient client = Utils.InitClient(req);

				IEntity entity;
				
				if (string.IsNullOrEmpty(requestObject.entitySearch.entitySearchField.fieldValue) || string.IsNullOrEmpty(requestObject.entitySearch.entitySearchField.fieldName))
				{
					entity = await Utils.SearchSingleEntity(client,
											(entities =>
											from e in entities
											where e.DefinitionName == requestObject.entitySearch.entitySearchField.definitionName
											select e),
											EntityLoadConfiguration.Full, log);
				}
				else
				{
					entity = await Utils.SearchSingleEntity(client,
											(entities =>
											from e in entities
											where e.Property(requestObject.entitySearch.entitySearchField.fieldName) == requestObject.entitySearch.entitySearchField.fieldValue
											where e.DefinitionName == requestObject.entitySearch.entitySearchField.definitionName
											select e),
											EntityLoadConfiguration.Full, log);
				}
				//Query for single Entity that matches the search criteria
				//IEntity entity = await Utils.SearchSingleEntity(client,
				//	requestObject.entitySearch.entitySearchField.fieldName,
				//	requestObject.entitySearch.entitySearchField.fieldValue,
				//	requestObject.entitySearch.entitySearchField.definitionName,
				//	EntityLoadConfiguration.Full, log);

				var isNewEntity = false;
				if (entity == null)
				{
					entity = await Utils.CreateEntity(client, requestObject.entitySearch.entitySearchField.definitionName);
					isNewEntity = true;
				}

				//Update property valeus with data from the request payload
				foreach (var property in requestObject.properties)
				{
					var key = property.Key;
					log?.Info($"Property Key: {key}, Value: {property.Value?.ToString()}", MethodBase.GetCurrentMethod().DeclaringType.Name);
					IProperty entityProperty = entity.GetProperty(key);
					if (entityProperty == null)
					{
						log?.Info($"Entity Property not found. Name: {property.Key}", MethodBase.GetCurrentMethod().DeclaringType.Name);
						return new HttpResponseMessage(HttpStatusCode.BadRequest)
						{
							Content = new StringContent($"Entity Property not found. Name: {property.Key}")
						};
					}
					else
					{
						var type = entityProperty.DataType;
						log?.Info($"Entity Property Name: {entityProperty.Name}, DefinitionType: {entityProperty.DefinitionType} Type: {entityProperty.DataType.Name}", MethodBase.GetCurrentMethod().DeclaringType.Name);

						if (property.Value != null && !string.IsNullOrEmpty(property.Value.ToString()))
						{
							var value = property.Value.ToObject(entityProperty.DataType);
							try
							{
								entity.SetPropertyValue(entityProperty.Name, value);
							}
							catch (Exception ex) when (ex.Message == "Culture is required for culture sensitive properties.")
							{
								CultureInfo defaultCulture = await client.Cultures.GetDefaultCultureAsync();
								entity.SetPropertyValue(entityProperty.Name, defaultCulture, value);
							}
						}
					}
				}

				//Save Entity changes back into Content Hub
				long id = await client.Entities.SaveAsync(entity);
				var changeKind = isNewEntity ? "created" : "updated";
				log?.Info($"Successfully {changeKind} entity. ID: {id}", MethodBase.GetCurrentMethod().DeclaringType.Name);
				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent($"Successfully {changeKind} entity. ID: {id}")
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

