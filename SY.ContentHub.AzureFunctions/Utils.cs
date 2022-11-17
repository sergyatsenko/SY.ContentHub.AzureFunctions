using Microsoft.Azure.WebJobs.Host;
using Stylelabs.M.Base.Querying;
using Stylelabs.M.Base.Querying.Linq;
using Stylelabs.M.Framework.Essentials.LoadConfigurations;
using Stylelabs.M.Sdk.Contracts.Base;
using Stylelabs.M.Sdk.WebClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using SY.ContentHub.AzureFunctions.Models;

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
			if(log != null ) log.Info($"About to execute GET request to {requestUri}.");
			using (var response = await httpClient.GetAsync(requestUri))
			{
				return await LogAndReturnResponse(response, "GET", requestUri.AbsoluteUri, log);
			}
		}

		public static async Task<IEntity> CreateEntity(IWebMClient client, string definitionName)
		{
			return await client.EntityFactory.CreateAsync(definitionName);
		}

		public static async Task<IEntity> SearchSingleEntity(IWebMClient client, string searchFieldName, string searchFieldValue, string entityDefinitionName, IEntityLoadConfiguration loadConfiguration, TraceWriter log = null)
		{
			

			var entityQuery = Query.CreateQuery(entities =>
						 from e in entities
						 where e.Property(searchFieldName) == searchFieldValue
						 where e.DefinitionName == entityDefinitionName
						 select e);

			try
			{
				var results = (await client.Querying.QueryAsync(entityQuery, loadConfiguration))?.Items;

				if (results != null && results.Any())
				{
					log?.Info($"Found entities. Count: {results.Count}, IDs: {String.Join(",", results.Select(e => e.Id))}", "SearchSingleEntity");
					return results.First();
				}

				return null;
			}
			catch (Exception ex)
			{
				log?.Error($"error message: {ex.Message}", ex, "SearchSingleEntity");
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

	}
}
