//#r "Newtonsoft.Json"

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using SY.ContentHub.AzureFunctions.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace SY.ContentHub.AzureFunctions
{
	public static partial class CreateOrUpdateRelationWithSearch
	{
		class RequestObject
		{
			public string baseUrl;
			public EntitySearch entitySearch;
			public EntityData entityData;
			public bool keepExistingRelations = false;
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

		[FunctionName("CreateOrUpdateRelationWithSearch")]
		public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequestMessage req, TraceWriter log)
		{
			log.Info("CreateOrUpdateRelationWithSearch invoked.");
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
					return new HttpResponseMessage(HttpStatusCode.OK)
					{
						Content = new StringContent($"Skipping relation creation as continueOnEmptySearchFields is set to true.  Parent entity search value: {requestObject.entitySearch.parentEntitySearchField.fieldValue}, Child entity search value: {requestObject.entitySearch.childEntitySearchField.fieldValue}")
					};
				}

				SearchResultsResponse parentResponseObject = null;
				SearchResultsResponse childrenResponseObject = null;

				using (var httpClient = new HttpClient())
				{
					httpClient.DefaultRequestHeaders.Add("X-Auth-Token", Utils.GetHeaderValue(req.Headers, "X-Auth-Token"));

					//Find parent entity
					var getParentRequest = $"/api/entities/query?query=Definition.Name=='{requestObject.entitySearch.parentEntitySearchField.definitionName}' AND {requestObject.entitySearch.parentEntitySearchField.fieldType}('{requestObject.entitySearch.parentEntitySearchField.fieldName}')=='{requestObject.entitySearch.parentEntitySearchField.fieldValue}'&take=1&members=id";
					var parentRequestUri = new Uri(new Uri(requestObject.baseUrl), getParentRequest);
					log.Info($"Parent entity search requiest url: {parentRequestUri.AbsoluteUri}");
					var getParentEntityResponse = await Utils.GetContentAsync(httpClient, parentRequestUri, log);
					if (getParentEntityResponse.IsSuccessStatusCode)
					{
						parentResponseObject = JsonConvert.DeserializeObject<SearchResultsResponse>(getParentEntityResponse.ResponseBody);
					}
					else
					{
						return new HttpResponseMessage(getParentEntityResponse.StatusCode)
						{
							Content = new StringContent($"Error searching for parent entity: {getParentEntityResponse.ResponseBody}")
						};
					}

					//Find child entity
					var getChildrenRequest = $"/api/entities/query?query=Definition.Name=='{requestObject.entitySearch.childEntitySearchField.definitionName}' AND {requestObject.entitySearch.childEntitySearchField.fieldType}('{requestObject.entitySearch.childEntitySearchField.fieldName}')=='{requestObject.entitySearch.childEntitySearchField.fieldValue}'&members=id";
					var childRequestUri = new Uri(new Uri(requestObject.baseUrl), getChildrenRequest);
					log.Info($"Child entity search requiest url: {childRequestUri.AbsoluteUri}");
					var getChildEntityResponse = await Utils.GetContentAsync(httpClient, childRequestUri, log);
					if (getChildEntityResponse.IsSuccessStatusCode)
					{
						childrenResponseObject = JsonConvert.DeserializeObject<SearchResultsResponse>(getChildEntityResponse.ResponseBody);
					}
					else
					{
						return new HttpResponseMessage(getChildEntityResponse.StatusCode)
						{
							Content = new StringContent($"Error searching for child entity: {getChildEntityResponse.ResponseBody}")
						};
					}

					//create relation
					if (parentResponseObject != null && parentResponseObject.items.Any()
						&& childrenResponseObject != null && childrenResponseObject.items.Any())
					{
						List<int> relationIds = childrenResponseObject.items.Select(i => i.id).ToList();
						var parentId = parentResponseObject.items[0].id;
						string childNodes = string.Empty;
						if (requestObject.keepExistingRelations)
						{
							var getChildNodesRelation = $"/api/entities/{parentId}/relations/{requestObject.entityData.relationFieldName}";
							var getChildNodesRelationUri = new Uri(new Uri(requestObject.baseUrl), getChildNodesRelation);
							log.Info($"Get existing relation child nodes requiest url: {getChildNodesRelationUri.AbsoluteUri}");

							ResponseData getChildNodesRelationResponse = await Utils.GetContentAsync(httpClient, getChildNodesRelationUri, log);
							if (getChildNodesRelationResponse.IsSuccessStatusCode)
							{
								log.Info($"Get existing relation child nodes: {getChildNodesRelationResponse.ResponseBody}");
								dynamic getChildNodesRelationResponseObject = JsonConvert.DeserializeObject(getChildNodesRelationResponse.ResponseBody);
								log.Info($"Get existing relation child nodes: children!= null: {getChildNodesRelationResponseObject?.children != null}");
								if (getChildNodesRelationResponseObject?.children != null && getChildNodesRelationResponseObject?.children.Count > 0)
								{
									log.Info($"Get existing relation child nodes: children.Coun: {getChildNodesRelationResponseObject?.children.Count > 0}");
									foreach (var item in getChildNodesRelationResponseObject.children)
									{
										string href = item.GetValue("href").ToString();
										log.Info($"href: {href}");
										if (!string.IsNullOrEmpty(href) && href.Contains("/"))
										{
											var idString = href.Split('/').Last();
											int id;
											if (int.TryParse(idString, out id))
											{
												if (!relationIds.Contains(id))
												{
													log.Info($"Adding relation ID {id} in href {href}");
													relationIds.Add(id);
												}
												else
												{
													log.Info($"Found already existing and skipping relation ID {id} in href {href}");
												}

											}
										}
									}
								}
							}
							else
							{
								log.Info($"Not found any existing relation child nodes. Response Code: {getChildNodesRelationResponse.StatusCode}. RequestUri: {getChildNodesRelationUri.AbsoluteUri}, Response: {getChildNodesRelationResponse.ResponseBody}");
							}
						}

						childNodes = string.Join(",", relationIds.Select(i => string.Format("{{ \"href\": \"{0}/api/entities/{1}\" }}", requestObject.baseUrl, i)));
						log.Info($"resulting childNodes: {childNodes}");
						var createRelationRequestBody = @$"{{
                                                                ""id"": {parentId},
                                                                ""relations"": {{
                                                                    ""{requestObject.entityData.relationFieldName}"": {{
                                                                        ""children"": [
                                                                            {childNodes}
                                                                        ],
                                                                        ""inherits_security"": true,
                                                                        ""self"": {{
                                                                            ""href"": ""{requestObject.baseUrl}/api/entities/{parentId}/relations/{requestObject.entityData.relationFieldName}""
                                                                        }}
                                                                    }}
                                                                }},
                                                                ""entitydefinition"": {{
                                                                    ""href"": ""{requestObject.baseUrl}/api/entitydefinitions/{requestObject.entitySearch.parentEntitySearchField.definitionName}""
                                                                }}
                                                             }}";

						var relationUpdateRequestUrl = new Uri(new Uri(requestObject.baseUrl), "api/entities");

						ResponseData relationUpdateResponse = await Utils.PostContentAsync(httpClient, relationUpdateRequestUrl, createRelationRequestBody, log);
						if (relationUpdateResponse.IsSuccessStatusCode)
						{
							//log.Info($"Successfully created relation: {relationUpdateResponse}");
							return new HttpResponseMessage(relationUpdateResponse.StatusCode)
							{
								Content = new StringContent($"Successfully created relation. Status Code: {relationUpdateResponse.StatusCode}, Response: {relationUpdateResponse.ResponseBody}")
							};
						}
						else
						{
							return new HttpResponseMessage(relationUpdateResponse.StatusCode)
							{
								Content = new StringContent($"Error creating relation: {relationUpdateResponse.ResponseBody}")
							};
						}
					}
					else
					{
						if (requestObject.continueOnNoFoundEntities)
						{
							return new HttpResponseMessage(HttpStatusCode.OK)
							{
								Content = new StringContent($"Skipping relation creation as continueOnNoFoundEntities is set to true.  Parent entity search value: {requestObject.entitySearch.parentEntitySearchField.fieldValue}, Child entity search value: {requestObject.entitySearch.childEntitySearchField.fieldValue}")
							};
						}
						else
						{
							log.Info($"One or both ends of the relation are not found.");
							return new HttpResponseMessage(HttpStatusCode.NotFound)
							{
								Content = new StringContent("One or both ends of the relation are not found.") // $"'enityDefinitionName':{entityDefinitionName}, 'searchFieldName':{searchFieldName}, 'searchFieldValue':{searchFieldValue}")
							};
						}
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
