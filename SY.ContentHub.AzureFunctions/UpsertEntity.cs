//#r "Newtonsoft.Json"

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stylelabs.M.Framework.Essentials.LoadConfigurations;
using Stylelabs.M.Sdk.Contracts.Base;
using Stylelabs.M.Sdk.WebClient;
using Stylelabs.M.Sdk.WebClient.Authentication;
using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace SY.ContentHub.AzureFunctions
{
	public static class UpsertEntity
	{
		class EntitySearch
		{
			public SearchEntityRequestBase entitySearchField;
		}

		class Client
		{
			public string clientId;
			public string clientSecret;
			public string userName;
			public string password;
		}

		class EntityProperty
		{
			public string name;
			public string type;
			public object value;
		}

		class RequestObject
		{
			public string baseUrl;
			public Client client;
			public EntitySearch entitySearch;
			//public List<EntityProperty> properties;
			public JObject properties;
			//public dynamic entityData;
			public void Validate()
			{
				if (string.IsNullOrEmpty(baseUrl)) throw new ArgumentException("baseUrl");
				if (entitySearch == null) throw new ArgumentException("entitySearchField is required and cannot be empty");
				if (entitySearch.entitySearchField == null) throw new ArgumentException("entitySearchField is required and cannot be emoty");
				entitySearch.entitySearchField.Validate();
			}
		}

		[FunctionName("UpsertEntity")]
		public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req, TraceWriter log)
		{
			log.Info("UpsertEntity invoked.", MethodBase.GetCurrentMethod().DeclaringType.Name);
			var content = req.Content;
			string requestBody = content.ReadAsStringAsync().Result;
			log?.Info($"Request body: {requestBody}", MethodBase.GetCurrentMethod().DeclaringType.Name);
			var requestObject = JsonConvert.DeserializeObject<RequestObject>(requestBody);
			
			try
			{
				Uri endpoint = new Uri(requestObject.baseUrl);
				//SearchResultsResponse searchResponseObject = null;
				OAuthPasswordGrant oauth = new OAuthPasswordGrant
				{
					ClientId = requestObject.client.clientId,
					ClientSecret = requestObject.client.clientSecret,
					UserName = requestObject.client.userName,
					Password = requestObject.client.password
				};

				IWebMClient client = MClientFactory.CreateMClient(endpoint, oauth);

				IEntity entity = await Utils.SearchSingleEntity(client, 
					requestObject.entitySearch.entitySearchField.fieldName, 
					requestObject.entitySearch.entitySearchField.fieldValue, 
					requestObject.entitySearch.entitySearchField.definitionName, 
					EntityLoadConfiguration.Full, log);

				var isNewEntity = false;
				if (entity == null)
				{
					entity = await Utils.CreateEntity(client, requestObject.entitySearch.entitySearchField.definitionName);
					isNewEntity = true;
				}

				foreach (var property in requestObject.properties)
				{
					var key = property.Key;
					//var value = property.Value.ToString();
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

