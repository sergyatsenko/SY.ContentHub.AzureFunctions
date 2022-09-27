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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace SY.ContentHub.AzureFunctions
{
	public static class UpsertRelation
	{
		class Client
		{
			public string clientId;
			public string clientSecret;
			public string userName;
			public string password;
		}

		class EntitySearch
		{
			public SearchEntityRequestBase parentEntitySearchField;
			public SearchEntityRequestBase childEntitySearchField;
		}

		class EntityData
		{
			public string relationFieldName;
		}

		public class Child
		{
			public string href { get; set; }
		}

		public class Root
		{
			public List<Child> children { get; set; }
		}


		class RequestObject
		{
			public string baseUrl;
			public Client client;
			public EntitySearch entitySearch;
			public EntityData entityData;
			public bool keepExistingRelations = false;
			public bool deleted = false;
			public bool continueOnEmptySearchFields = false;
			public bool continueOnNoFoundEntities = false;
			public void Validate()
			{
				if (string.IsNullOrEmpty(baseUrl)) throw new ArgumentException("baseUrl");
				if (entityData == null) throw new ArgumentException("entityData is required and cannot be empty.");
				if (string.IsNullOrEmpty(entityData.relationFieldName)) throw new ArgumentException("entityData.relationFieldName");
				if (entitySearch?.parentEntitySearchField == null) throw new ArgumentException("parentEntitySearchField");
				if (entitySearch?.childEntitySearchField == null) throw new ArgumentException("childEntitySearchField");
				entitySearch.parentEntitySearchField.Validate(continueOnEmptySearchFields);
				entitySearch.childEntitySearchField.Validate(continueOnEmptySearchFields);
			}
		}

		[FunctionName("UpsertRelation")]
		public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req, TraceWriter log)
		{
			log.Info("UpsertRelation invoked.");
			var content = req.Content;
			string requestBody = content.ReadAsStringAsync().Result;
			log.Info($"Request body: {requestBody}");
			var requestObject = JsonConvert.DeserializeObject<RequestObject>(requestBody);

			try
			{
				requestObject.Validate();

				if (requestObject.continueOnEmptySearchFields
					&& (
					string.IsNullOrEmpty(requestObject.entitySearch.parentEntitySearchField.fieldValue)
					|| string.IsNullOrEmpty(requestObject.entitySearch.childEntitySearchField.fieldValue)))
				{
					var message = $"Skipping relation creation as continueOnEmptySearchFields is set to true.  Parent entity search value: {requestObject.entitySearch.parentEntitySearchField.fieldValue}, Child entity search value: {requestObject.entitySearch.childEntitySearchField.fieldValue}";
					log?.Info(message, MethodBase.GetCurrentMethod().DeclaringType.Name);
					return new HttpResponseMessage(HttpStatusCode.OK)
					{
						Content = new StringContent(message)
					};
				}

				Uri endpoint = new Uri(requestObject.baseUrl);
				OAuthPasswordGrant oauth = new OAuthPasswordGrant
				{
					ClientId = requestObject.client.clientId,
					ClientSecret = requestObject.client.clientSecret,
					UserName = requestObject.client.userName,
					Password = requestObject.client.password
				};

				IWebMClient client = MClientFactory.CreateMClient(endpoint, oauth);

				IEntity parentEntity = await Utils.SearchSingleEntity(client,
					requestObject.entitySearch.parentEntitySearchField.fieldName,
					requestObject.entitySearch.parentEntitySearchField.fieldValue,
					requestObject.entitySearch.parentEntitySearchField.definitionName,
					EntityLoadConfiguration.Full, log);

				IEntity childEntity = await Utils.SearchSingleEntity(client,
					requestObject.entitySearch.childEntitySearchField.fieldName,
					requestObject.entitySearch.childEntitySearchField.fieldValue,
					requestObject.entitySearch.childEntitySearchField.definitionName,
					EntityLoadConfiguration.Minimal, log);

				if (parentEntity != null && parentEntity.Id != null && parentEntity.Id.HasValue
					&& childEntity != null && childEntity.Id != null && childEntity.Id.HasValue)
				{
					string message = "";
					var relation = parentEntity.GetRelation(requestObject.entityData.relationFieldName);
					if (relation != null)
					{
						var ids = relation.GetIds().ToList();

						if (!ids.Contains(childEntity.Id.Value))
						{
							if (requestObject.deleted)
							{
								message = $"Skipping relation add because it is marked deleted. Relation Name: {requestObject.entityData.relationFieldName}, Parent Entity ID: {parentEntity.Id}, Child Entity ID: {childEntity.Id}";
							}
							else
							{
								ids.Add(childEntity.Id.Value);
								relation.SetIds(ids);
								long addedResult = await client.Entities.SaveAsync(parentEntity);
								message = $"Successfully added relation. Relation Name: {requestObject.entityData.relationFieldName},Parent Entity ID: {parentEntity.Id}, Child Entity ID: {childEntity.Id}";
							}

						}
						else
						{
							if (requestObject.deleted)
							{
								ids.Remove(childEntity.Id.Value);
								relation.SetIds(ids);
								long deletedResultId = await client.Entities.SaveAsync(parentEntity);
								message = $"Successfully deleted relation because it is marked deleted. Relation Name: {requestObject.entityData.relationFieldName},Parent Entity ID: {parentEntity.Id}, Child Entity ID: {childEntity.Id}";
							}
							else
							{
								message = $"Relation already exists - skipping. Relation Name: {requestObject.entityData.relationFieldName}, Parent Entity ID: {parentEntity.Id}, Child Entity ID: {childEntity.Id}";
							}
						}

						log?.Info(message, MethodBase.GetCurrentMethod().DeclaringType.Name);
						return new HttpResponseMessage(HttpStatusCode.OK)
						{
							Content = new StringContent(message)
						};
					}
					else
					{
						message = $"Relation not found. Parent Entity ID: {parentEntity.Id}, Relation Field Name: {requestObject.entityData.relationFieldName}";
					}

					log?.Info(message, MethodBase.GetCurrentMethod().DeclaringType.Name);
					return new HttpResponseMessage(HttpStatusCode.NotFound)
					{
						Content = new StringContent(message)
					};
				}
				else

				{
					if (requestObject.continueOnNoFoundEntities)
					{
						var message = $"Skipping relation creation as continueOnNoFoundEntities is set to true.  Parent entity search value: {requestObject.entitySearch.parentEntitySearchField.fieldValue}, Child entity search value: {requestObject.entitySearch.childEntitySearchField.fieldValue}";
						log?.Info(message, MethodBase.GetCurrentMethod().DeclaringType.Name);
						return new HttpResponseMessage(HttpStatusCode.OK)
						{
							Content = new StringContent(message)
						};
					}
					else
					{
						var message = $"One or both ends of the relation are not found. Parent Entity ID Found: {parentEntity?.Id}, Child Entity ID: {childEntity?.Id}";
						log?.Error(message, null, MethodBase.GetCurrentMethod().DeclaringType.Name);
						return new HttpResponseMessage(HttpStatusCode.NotFound)
						{
							Content = new StringContent(message)
						};
					}
				}
			}
			catch (ArgumentException argEx)
			{
				var message = $"Invalid request body or missing required parameters. Error message: {argEx.Message}";
				log?.Info(message, MethodBase.GetCurrentMethod().DeclaringType.Name);
				return new HttpResponseMessage(HttpStatusCode.BadRequest)
				{
					Content = new StringContent(message)
				};
			}
			catch (Exception ex)
			{
				var message = $"'Error ':{ex.Message}";
				return new HttpResponseMessage(HttpStatusCode.InternalServerError)
				{
					Content = new StringContent(message)
				};
			}
		}
	}
}
