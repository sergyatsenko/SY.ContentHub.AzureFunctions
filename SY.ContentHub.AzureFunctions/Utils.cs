using Microsoft.Azure.WebJobs.Host;
using Stylelabs.M.Base.Querying.Linq;
using Stylelabs.M.Framework.Essentials.LoadConfigurations;
using Stylelabs.M.Sdk.Contracts.Base;
using Stylelabs.M.Sdk.Contracts.Querying;
using Stylelabs.M.Sdk.WebClient;
using Stylelabs.M.Sdk.WebClient.Authentication;
using SY.ContentHub.AzureFunctions.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Query = Stylelabs.M.Base.Querying.Query;

namespace SY.ContentHub.AzureFunctions
{
	public class ResponseData
	{
		public HttpStatusCode StatusCode { get; set; }
		public string ResponseBody { get; set; }
		public bool IsSuccessStatusCode { get; set; }

	}
	public static class Utils
	{
		public static Client ExtractClientInfo(HttpRequestHeaders headers)
		{
			return new Client
			{
				baseUrl = GetHeaderValue(headers, "baseUrl"),
				clientId = GetHeaderValue(headers, "clientId"),
				clientSecret = GetHeaderValue(headers, "clientSecret"),
				userName = GetHeaderValue(headers, "userName"),
				password = GetHeaderValue(headers, "password"),
			};
		}

		public static string GetHeaderValue(HttpRequestHeaders headers, string key, bool throwErrorWhenEmpty = true)
		{
			if (!headers.TryGetValues(key, out IEnumerable<string> values) && throwErrorWhenEmpty)
				throw new ArgumentException($"Missig required header value for {key}");

			var headerValue = string.Join(",", values);
			if (headerValue.Length == 0 && throwErrorWhenEmpty)
			{
				throw new ArgumentException($"Required header value for {key} appear to be empty.");
			}

			return headerValue;
		}

		public static async Task<ResponseData> GetContentAsync(HttpClient httpClient, Uri requestUri, TraceWriter log = null)
		{
			if (log != null) log.Info($"About to execute GET request to {requestUri}.");
			using (var response = await httpClient.GetAsync(requestUri))
			{
				return await LogAndReturnResponse(response, "GET", requestUri.AbsoluteUri, log);
			}
		}

		public static async Task<IEntity> CreateEntity(IWebMClient client, string definitionName)
		{
			return await client.EntityFactory.CreateAsync(definitionName);
		}

		/// <summary>
		/// Search for IDs of all entities matching the search query (function)
		/// </summary>
		/// <param name="client">Content Hub Web SDK client</param>
		/// <param name="queryFunction"></param>
		/// <param name="log">optional logger - ignore if no logging is needed</param>
		/// <returns>List of IDs of entities matching the search criteria</returns>
		public static async Task<IList<long>> SearchEntityIDs(IWebMClient client, Func<QueryableEntities<IQueryableEntity>, IQueryable<IQueryableEntity>> queryFunction, TraceWriter log = null)
		{
			log?.Info($"Search query function: {queryFunction}");
			//Initialize the query with given search criteria (function)
			Query query = Query.CreateQuery(queryFunction);

			try
			{
				var results = new List<long>();
				//Deal with paging when there are more than 50 results (default page size)
				IIdIterator iterator = client.Querying.CreateEntityIdIterator(query);
				while (await iterator.MoveNextAsync())
				{
					var ids = iterator.Current.Items;
					if (ids != null && ids.Any())
					{
						results.AddRange(ids);
					}
				}

				if (results != null && results.Any())
				{
					//Log and return the results
					log?.Info($"Found entities. Count: {results.Count}, IDs: {String.Join(",", results)}", "SearcEntitiesByFieldFalue");
					return results;
				}

				return null;
			}
			catch (Exception ex)
			{
				//Log and re-throw the exception
				log?.Error($"error message: {ex.Message}", ex, "SearcEntityIDs");
				throw ex;
			}

		}

		/// <summary>
		/// Search and load all entities matching the search query (function)
		/// </summary>
		/// <param name="client">Content Hub Web SDK client</param>
		/// <param name="queryFunction"></param>
		/// <param name="log">optional logger - ignore if no logging is needed</param>
		/// <returns>List of fully loaded entities matching the search criteria. Warning: be careful - this could grow huge</returns>
		public static async Task<IList<IEntity>> SearcEntities(IWebMClient client, Func<QueryableEntities<IQueryableEntity>, IQueryable<IQueryableEntity>> queryFunction, IEntityLoadConfiguration loadConfiguration, TraceWriter log = null)
		{
			log?.Info($"Search query function: {queryFunction}");
			//Initialize the query with given search criteria (function)
			Query query = Query.CreateQuery(queryFunction);
			try
			{
				var results = new List<IEntity>();
				//Deal with paging when there are more than 50 results (default page size)
				IEntityIterator iterator = client.Querying.CreateEntityIterator(query, loadConfiguration);
				while (await iterator.MoveNextAsync())
				{
					var entities = iterator.Current.Items;
					if (entities != null && entities.Any())
					{
						results.AddRange(entities);
					}
				}

				if (results != null && results.Any())
				{
					//Log and return the results
					log?.Info($"Found entities. Count: {results.Count}, IDs: {String.Join(",", results.Select(e => e.Id))}", "SearcEntitiesByFieldFalue");
					return results;
				}

				return null;
			}
			catch (Exception ex)
			{
				//Log and re-throw the exception
				log?.Error($"error message: {ex.Message}", ex, "SearcEntities");
				throw;
			}

		}
		
		/// <summary>
		/// Search for the ID of the first entity matching the search query (function)
		/// </summary>
		/// <param name="client">Content Hub Web SDK client</param>
		/// <param name="queryFunction"></param>
		/// <param name="log">optional logger - ignore if no logging is needed</param>
		/// <returns>An ID of first entity matching the search criteria</returns>
		public static async Task<IEntity> SearchSingleEntity(IWebMClient client, Func<QueryableEntities<IQueryableEntity>, IQueryable<IQueryableEntity>> queryFunction, IEntityLoadConfiguration loadConfiguration, TraceWriter log = null)
		{
			Query query = Query.CreateQuery(queryFunction);
			try
			{
				//Search for the first match for a given query
				var result = (await client.Querying.SingleAsync(query, loadConfiguration));

				if (result != null)
				{
					//Log and return the results
					log?.Info($"Found entitity. ID: {result.Id}", "SearchSingleEntity");
					return result;
				}

				return null;
			}
			catch (Exception ex)
			{
				//Log and re-throw the exception
				log?.Error($"error message: {ex.Message}", ex, "SearchSingleEntity");
				throw ex;
			}
		}

		/// <summary>
		/// Get Content Hub Entity by ID
		/// </summary>
		/// <param name="client"></param>
		/// <param name="entityId">ID of the Entity</param>
		/// <param name="loadConfiguration">Entity load configuration, contolling how much data to load from CH.</param>
		/// <param name="log">optional logger</param>
		/// <returns>Entity or null if not found.</returns>
		public static async Task<IEntity> GetEntity(IWebMClient client, long entityId, IEntityLoadConfiguration loadConfiguration, TraceWriter log = null)
		{
			try
			{
				//Read Entitty and load its fields and relations as per specified LoadConfiguration
				var entity = await client.Entities.GetAsync(entityId, EntityLoadConfiguration.Full);

				if (entity != null)
				{
					//Log and return the results
					log?.Info($"Found entitity. ID: {entity.Id}", "GetEntity");
					return entity;
				}

				return null;
			}
			catch (Exception ex)
			{
				//Log and re-throw the exception
				log?.Error($"error message: {ex.Message}", ex, "GetEntity");
				throw ex;
			}
		}


		public static async Task<ResponseData> PostContentAsync(HttpClient httpClient, Uri requestUri, string content, TraceWriter log = null)
		{
			if (log != null) log.Info($"About to execute POST request to {requestUri}. Content: {content}.");
			var encodedJsonContent = new StringContent(content, Encoding.UTF8, "application/json");
			using (var response = await httpClient.PostAsync(requestUri, encodedJsonContent))
			{
				return await LogAndReturnResponse(response, "POST", requestUri.AbsoluteUri, log);
			}
		}

		public static async Task<ResponseData> PutContentAsync(HttpClient httpClient, Uri requestUri, string content, TraceWriter log = null)
		{
			if (log != null) log.Info($"About to execute PUT request to {requestUri}. Content: {content}.");
			var encodedJsonContent = new StringContent(content, Encoding.UTF8, "application/json");
			using (var response = await httpClient.PutAsync(requestUri, encodedJsonContent))
			{
				return await LogAndReturnResponse(response, "PUT", requestUri.AbsoluteUri, log);
			}
		}

		private async static Task<ResponseData> LogAndReturnResponse(HttpResponseMessage response, string verb, string requestUri, TraceWriter log = null)
		{
			string responseBody = await response.Content.ReadAsStringAsync();
			if (response.IsSuccessStatusCode)
			{
				if (log != null)
				{
					log.Info($"Success sending {verb} request to {requestUri}. Status Code: {response.StatusCode}, Response Body: {responseBody}");
				}
				return new ResponseData { StatusCode = response.StatusCode, IsSuccessStatusCode = response.IsSuccessStatusCode, ResponseBody = responseBody };
			}
			else
			{
				if (log != null)
				{
					log.Info($"Error sending {verb} request to {requestUri}. Status Code: {response.StatusCode}, Response Body: {responseBody}");
				}
				return new ResponseData { StatusCode = response.StatusCode, IsSuccessStatusCode = response.IsSuccessStatusCode, ResponseBody = responseBody };
			}
		}

		public static async Task<Dictionary<string, List<dynamic>>> GetRelatedEntities(IWebMClient client, IEntity entity, List<string> relationFields, bool resolveSolrNames, TraceWriter log = null)
		{
			var relations = new Dictionary<string, List<dynamic>>();
			if (relationFields != null && relationFields.Any())
			{
				log.Info("Loading relations...", MethodBase.GetCurrentMethod().DeclaringType.Name);
				foreach (var relationName in relationFields)
				{
					log.Info($"Retrieving '{relationName}' relation...", MethodBase.GetCurrentMethod().DeclaringType.Name);
					var relation = entity.GetRelation(relationName);
					var relatedIds = relation?.GetIds();
					if (relatedIds.Count > 0)
					{
						var relationData = new List<dynamic>();
						foreach (var relatedId in relatedIds)
						{
							IEntity relatedEntity = await client.Entities.GetAsync(relatedId, EntityLoadConfiguration.Full);
							if (relatedEntity != null)
							{
								var entityData = new
								{
									Properties = resolveSolrNames ? ExtractEntityData(relatedEntity, solrFieldNameResolver) : ExtractEntityData(relatedEntity, defaultFieldNameResolver),
									Renditions = entity.Renditions
								};
								relationData.Add(entityData);
								//relationData.Add(ExtractEntityData(relatedEntity));
							}
						}
						if (relationData.Count > 0)
						{
							relations.Add(relationName, relationData);
						}
					}
				}
			}

			return relations;
		}

		public static async Task<List<IEntity>> GetRelatedEntities(IWebMClient client, IEntity entity, string entityRelation, IEntityLoadConfiguration loadConfiguration, TraceWriter log)
		{
			if (!string.IsNullOrEmpty(entityRelation))
			{
				var relatedEntities = new List<IEntity>();

				var relation = entity.GetRelation(entityRelation);
				log.Info($"Retrieved regular relation: {relation}", MethodBase.GetCurrentMethod().DeclaringType.Name);
				var relatedIds = relation?.GetIds();
				log.Info($"Relation IDs count: {relatedIds?.Count}", MethodBase.GetCurrentMethod().DeclaringType.Name);

				if (relatedIds == null || relatedIds?.Count < 1)
				{
					relation = entity.GetRelation(entityRelation, Stylelabs.M.Framework.Essentials.LoadOptions.RelationRole.Parent);
					log.Info($"Retrieved parent relation: {relation}", MethodBase.GetCurrentMethod().DeclaringType.Name);
					relatedIds = relation?.GetIds();
					log.Info($"Parent Relation IDs count: {relatedIds?.Count}", MethodBase.GetCurrentMethod().DeclaringType.Name);
				}

				if (relatedIds == null || relatedIds?.Count < 1)
				{
					relation = entity.GetRelation(entityRelation, Stylelabs.M.Framework.Essentials.LoadOptions.RelationRole.Child);
					log.Info($"Retrieved child relation: {relation}", MethodBase.GetCurrentMethod().DeclaringType.Name);
					relatedIds = relation?.GetIds();
					log.Info($"Child Relation IDs count: {relatedIds?.Count}", MethodBase.GetCurrentMethod().DeclaringType.Name);
				}

				if (relatedIds?.Count > 0)
				{
					foreach (var relatedId in relatedIds)
					{
						var relatedEntitity = await client.Entities.GetAsync(relatedId, EntityLoadConfiguration.Full);
						if (relatedEntitity != null)
						{
							relatedEntities.Add(relatedEntitity);
						}
					}
					return relatedEntities;
				}
			}

			return null;
		}

		public static IList<long> GetRelationIDs(IWebMClient client, IEntity entity, string entityRelation, IEntityLoadConfiguration loadConfiguration, TraceWriter log)
		{
			if (!string.IsNullOrEmpty(entityRelation))
			{
				var relatedEntities = new List<IEntity>();

				var relation = entity.GetRelation(entityRelation);
				log.Info($"Retrieved regular relation: {relation}", MethodBase.GetCurrentMethod().DeclaringType.Name);
				var relatedIds = relation?.GetIds();
				log.Info($"Relation IDs count: {relatedIds?.Count}", MethodBase.GetCurrentMethod().DeclaringType.Name);

				if (relatedIds == null || relatedIds?.Count < 1)
				{
					relation = entity.GetRelation(entityRelation, Stylelabs.M.Framework.Essentials.LoadOptions.RelationRole.Parent);
					log.Info($"Retrieved parent relation: {relation}", MethodBase.GetCurrentMethod().DeclaringType.Name);
					relatedIds = relation?.GetIds();
					log.Info($"Parent Relation IDs count: {relatedIds?.Count}", MethodBase.GetCurrentMethod().DeclaringType.Name);
				}

				if (relatedIds == null || relatedIds?.Count < 1)
				{
					relation = entity.GetRelation(entityRelation, Stylelabs.M.Framework.Essentials.LoadOptions.RelationRole.Child);
					log.Info($"Retrieved child relation: {relation}", MethodBase.GetCurrentMethod().DeclaringType.Name);
					relatedIds = relation?.GetIds();
					log.Info($"Child Relation IDs count: {relatedIds?.Count}", MethodBase.GetCurrentMethod().DeclaringType.Name);
				}

				return relatedIds;

			}

			return null;
		}

		public static string defaultFieldNameResolver(string fieldName, Type type)
		{
			return fieldName;
		}

		public static string solrFieldNameResolver(string fieldName, Type type)
		{
			switch (type)
			{
				case Type t when t == typeof(string) 
							|| t == typeof(Guid)
							|| t == typeof(short)
							|| t == typeof(char):
					return $"{fieldName}_s";
				case Type t when t == typeof(int) || t == typeof(long):
					return $"{fieldName}_tl";
				case Type t when t == typeof(float) || t == typeof(decimal):
					return $"{fieldName}_tf";
				case Type t when t == typeof(double):
					return $"{fieldName}_td";
				case Type t when t == typeof(bool):
					return $"{fieldName}_b";
				case Type t when t == typeof(DateTime):
					return $"{fieldName}_dtm";
				default:
					return $"{fieldName}_s";
			}
		}

		/// <summary>
		/// Read entity properties into name-value pairs
		/// </summary>
		/// <param name="entity">Source Entity</param>
		/// <returns>name-value pairs representing all entity properties</returns>
		public static Dictionary<string, object> ExtractEntityData(IEntity entity, Func<string, Type, string> nameResolver = null)
		{
			if (nameResolver == null) nameResolver = defaultFieldNameResolver;
			
			var e = new Dictionary<string, object>
			{
				{ nameResolver("Id", typeof(long)), entity.Id },
				{ nameResolver("Identifier", typeof(string)), entity.Identifier },
				{ nameResolver("DefinitionName", typeof(string)), entity.DefinitionName },
				{ nameResolver("CreatedBy", typeof(long)), entity.CreatedBy },
				{ nameResolver("CreatedOn", typeof(DateTime)), entity.CreatedOn },
				{ nameResolver("IsDirty", typeof(bool)), entity.IsDirty },
				{ nameResolver("IsNew", typeof(bool)), entity.IsNew },
				{ nameResolver("IsRootTaxonomyItem", typeof(bool)), entity.IsRootTaxonomyItem },
				{ nameResolver("IsPathRoot", typeof(bool)), entity.IsPathRoot },
				{ nameResolver("IsSystemOwned", typeof(bool)), entity.IsSystemOwned },
				{ nameResolver("Version", typeof(string)), entity.Version }
				//{ nameResolver("Cultures", typeof(long)), entity.Cultures }
			};

			var relativeUrl = entity.GetPropertyValue<string>("RelativeUrl");
			var versionHash = entity.GetPropertyValue<string>("VersionHash");

			// Construct public link Urls if Entity happends to be an Asset and have public links set on it
			if (!string.IsNullOrEmpty(relativeUrl) && !string.IsNullOrEmpty(versionHash))
			{
				var publicLink = $"api/public/content/{relativeUrl}?v={versionHash}";
				e.Add(nameResolver("PublicLink", typeof(string)), publicLink);
			}

			foreach (var property in entity.Properties)
			{
				try
				{
					var propertyValue = entity.GetPropertyValue(property.Name);
					e.Add(nameResolver(property.Name, property.DataType), propertyValue);
				}
				catch (Exception ex) when (ex.Message == "Culture is required for culture sensitive properties.")
				{
					var propertyValue = entity.GetPropertyValue(property.Name, CultureInfo.GetCultureInfo("en-US"));
					e.Add(nameResolver(property.Name, property.DataType), propertyValue);
				}
			}

			return e;
		}

		/// <summary>
		/// Initialize WebClient with credentials provided in a given request headers.
		/// </summary>
		/// <param name="request"></param>
		/// <returns>WebClient</returns>
		public static IWebMClient InitClient(HttpRequestMessage request)
		{
			var clientInfo = Utils.ExtractClientInfo(request.Headers);
			Uri endpoint = new Uri(clientInfo.baseUrl);
			OAuthPasswordGrant oauth = new OAuthPasswordGrant
			{
				ClientId = clientInfo.clientId,
				ClientSecret = clientInfo.clientSecret,
				UserName = clientInfo.userName,
				Password = clientInfo.password
			};

			IWebMClient client = MClientFactory.CreateMClient(endpoint, oauth);
			return client;
		}
		
	}
}
