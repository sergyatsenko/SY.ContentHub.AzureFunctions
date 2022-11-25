//#r "Newtonsoft.Json"

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Stylelabs.M.Base.Querying.Linq;
using Stylelabs.M.Framework.Essentials.LoadConfigurations;
using Stylelabs.M.Framework.Essentials.LoadOptions;
using Stylelabs.M.Sdk.Contracts.Base;
using Stylelabs.M.Sdk.WebClient;
using Stylelabs.M.Sdk.WebClient.Authentication;
using SY.ContentHub.AzureFunctions.Models;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using static Stylelabs.M.Sdk.Errors;

namespace SY.ContentHub.AzureFunctions
{
	/// <summary>
	/// Update an existing or create a new relation between two specified entities in Content Hub
	/// </summary>
	public static partial class UpsertRelation
	{
		[FunctionName("UpsertRelation")]
		public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req, TraceWriter log)
		{
			//Read and parse request payload
			log.Info("UpsertRelation invoked.");
			var content = req.Content;
			string requestBody = content.ReadAsStringAsync().Result;
			log.Info($"Request body: {requestBody}");
			var requestObject = JsonConvert.DeserializeObject<UpsertRelationRequest>(requestBody);

			try
			{
				requestObject.Validate();

				if (requestObject.continueOnEmptySearchFields
					&& (
					string.IsNullOrEmpty(requestObject.entitySearch.parentEntitySearchField.fieldValue)
					|| string.IsNullOrEmpty(requestObject.entitySearch.childEntitySearchField.fieldValue)))
				{
					var message = $"Skipping relation creation as continueOnEmptySearchFields is set to true. Relation Name: {requestObject.entityData.relationFieldName}, Parent entity search value: {requestObject.entitySearch.parentEntitySearchField.fieldValue}, Child entity search value: {requestObject.entitySearch.childEntitySearchField.fieldValue}";
					log?.Info(message, MethodBase.GetCurrentMethod().DeclaringType.Name);
					return new HttpResponseMessage(HttpStatusCode.OK)
					{
						Content = new StringContent(message)
					};
				}

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


				IEntity parentEntity = await Utils.SearchSingleEntity(client,
											(entities =>
											from e in entities
											where e.Property(requestObject.entitySearch.parentEntitySearchField.fieldName) == requestObject.entitySearch.parentEntitySearchField.fieldValue
											where e.DefinitionName == requestObject.entitySearch.parentEntitySearchField.definitionName
											select e),
											EntityLoadConfiguration.Full, log);

				IEntity childEntity = await Utils.SearchSingleEntity(client,
											(entities =>
											from e in entities
											where e.Property(requestObject.entitySearch.childEntitySearchField.fieldName) == requestObject.entitySearch.childEntitySearchField.fieldValue
											where e.DefinitionName == requestObject.entitySearch.childEntitySearchField.definitionName
											select e),
											EntityLoadConfiguration.Full, log);

				////Query for single Entity that matches the search criteria for parent entity for the relation
				//IEntity parentEntity = await Utils.SearchSingleEntity(client,
				//	requestObject.entitySearch.parentEntitySearchField.fieldName,
				//	requestObject.entitySearch.parentEntitySearchField.fieldValue,
				//	requestObject.entitySearch.parentEntitySearchField.definitionName,
				//	EntityLoadConfiguration.Full, log);

				////Query for single Entity that matches the search criteria for child entity for the relation
				//IEntity childEntity = await Utils.SearchSingleEntity(client,
				//	requestObject.entitySearch.childEntitySearchField.fieldName,
				//	requestObject.entitySearch.childEntitySearchField.fieldValue,
				//	requestObject.entitySearch.childEntitySearchField.definitionName,
				//	EntityLoadConfiguration.Minimal, log);

				if (parentEntity != null && parentEntity.Id != null && parentEntity.Id.HasValue
					&& childEntity != null && childEntity.Id != null && childEntity.Id.HasValue)
				{
					log?.Info($"Parent Entity ID: {parentEntity.Id}, Child Entity ID: {childEntity.Id}");
					IRelation relation = null;
					string message = "";

					//Get a hold of Relation field in Parent entity
					relation = parentEntity.GetRelation(requestObject.entityData.relationFieldName, RelationRole.Parent);
					log?.Info($"Parent Relation from parent: {relation}");
					if (relation == null)
					{
						relation = parentEntity.GetRelation(requestObject.entityData.relationFieldName, RelationRole.Child);
						log?.Info($"Child Relation from child: {relation}");
					}

					try
					{
						//Update, create or delete the relation between entities, based on request values
						if (relation != null)
						{
							var ids = relation.GetIds().ToList();
							//If relation already exist then update/add or delete when "deleted" field is set to true in the request
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
							//If relation don't exist then add it, unless the "deleted" field is set to true in the request
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
							relation = parentEntity.GetRelation(requestObject.entityData.relationFieldName, RelationRole.Child);
							log?.Info($"Child Relation: {relation}");

							message = $"Relation not found. Parent Entity ID: {parentEntity.Id}, Relation Field Name: {requestObject.entityData.relationFieldName}";
						}
					}
					catch (Exception ex)
					{
						log?.Error($"Exception: Type: {ex.GetType()}, Message: {ex.Message}", ex, MethodBase.GetCurrentMethod().DeclaringType.Name);
						throw;
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
