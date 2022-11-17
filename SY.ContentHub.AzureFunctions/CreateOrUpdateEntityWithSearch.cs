//#r "Newtonsoft.Json"

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using SY.ContentHub.AzureFunctions.Models;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace SY.ContentHub.AzureFunctions
{
	public static class CreateOrUpdateEntityWithSearch
	{
		class EntitySearch
		{
			public SearchEntityRequestBase entitySearchField;
		}
		class RequestObject
		{
			public string baseUrl;
			public EntitySearch entitySearch;
			public dynamic entityData;
			public void Validate()
			{
				if (string.IsNullOrEmpty(baseUrl)) throw new ArgumentException("baseUrl");
				if (entitySearch == null) throw new ArgumentException("entitySearchField is required and cannot be empty");
				if (entitySearch.entitySearchField == null) throw new ArgumentException("entitySearchField is required and cannot be emoty");
				entitySearch.entitySearchField.Validate();
			}
		}

		[FunctionName("CreateOrUpdateEntityWithSearch")]
		public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req, TraceWriter log)
		{
			log.Info("CreateOrUpdateEntityWithSearch invoked.");
			var content = req.Content;
			string requestBody = content.ReadAsStringAsync().Result;
			log.Info($"Request body: {requestBody}");
			var requestObject = JsonConvert.DeserializeObject<RequestObject>(requestBody);

			try
			{
				SearchResultsResponse searchResponseObject = null;

				using (var httpClient = new HttpClient())
				{
					httpClient.DefaultRequestHeaders.Add("X-Auth-Token", Utils.GetHeaderValue(req.Headers, "X-Auth-Token"));

					//Find parent entity
					var getEntityRequest = $"/api/entities/query?query=Definition.Name=='{requestObject.entitySearch.entitySearchField.definitionName}' AND {requestObject.entitySearch.entitySearchField.fieldType}('{requestObject.entitySearch.entitySearchField.fieldName}')=='{requestObject.entitySearch.entitySearchField.fieldValue}'&take=1&members=id";
					var entityRequestUri = new Uri(new Uri(requestObject.baseUrl), getEntityRequest);
					log.Info($"entity search requiest url: {entityRequestUri.AbsoluteUri}");
					var entityDataString = JsonConvert.SerializeObject(requestObject.entityData);
					log.Info($"entity update/create request: {entityDataString}");

					var entityResponse = await Utils.GetContentAsync(httpClient, entityRequestUri, log);
					if (entityResponse.IsSuccessStatusCode)
					{
						searchResponseObject = JsonConvert.DeserializeObject<SearchResultsResponse>(entityResponse.ResponseBody);
						if (searchResponseObject != null && searchResponseObject.items.Any())
						{
							var entityId = searchResponseObject.items[0].id;
							log.Info($"Found entity to update. Entity ID: {entityId}");
							var entityUpdatePutUrl = new Uri(new Uri(requestObject.baseUrl), $"/api/entities/{entityId}");
							ResponseData entityUpdateResponse = await Utils.PutContentAsync(httpClient, entityUpdatePutUrl, entityDataString, log);
							if (entityUpdateResponse.IsSuccessStatusCode)
							{
								return new HttpResponseMessage(entityUpdateResponse.StatusCode)
								{
									Content = new StringContent($"Successfully updated entity: {entityUpdateResponse.ResponseBody}")
								};
							}
							else
							{
								return new HttpResponseMessage(entityUpdateResponse.StatusCode)
								{
									Content = new StringContent($"Error updating entity: {entityUpdateResponse.ResponseBody}")
								};
							}
						}
					}
					else
					{
						log.Info($"Not found entity to update with given request: {entityRequestUri.AbsoluteUri}");
					}

					var entityCreatePostUrl = new Uri(new Uri(requestObject.baseUrl), $"/api/entities");
					ResponseData entityCreateResponse = await Utils.PostContentAsync(httpClient, entityCreatePostUrl, entityDataString, log);
					if (entityCreateResponse.IsSuccessStatusCode)
					{
						return new HttpResponseMessage(entityCreateResponse.StatusCode)
						{
							Content = new StringContent($"Successfully created entity: {entityCreateResponse.ResponseBody}")
						};
					}
					else
					{
						return new HttpResponseMessage(entityCreateResponse.StatusCode)
						{
							Content = new StringContent($"Error creating entity: {entityCreateResponse.ResponseBody}")
						};
					}
				}
			}
			catch (ArgumentException argEx)
			{
				log.Info($"Invalid request body or missing required parameters. Error message: {argEx.Message}");
				return new HttpResponseMessage(HttpStatusCode.BadRequest)
				{
					Content = new StringContent($"Invalid request body or missing required parameters. Error message: {argEx.Message}")
				};
			}
			catch (Exception ex)
			{
				log.Info($"'Error message':{ex.Message}");
				return new HttpResponseMessage(HttpStatusCode.InternalServerError)
				{
					Content = new StringContent($"'Error message':{ex.Message}")
				};
			}
		}
	}
}

