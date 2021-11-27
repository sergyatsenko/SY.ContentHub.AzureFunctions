using System;
using System.Collections.Generic;
using System.Net.Http.Headers;

namespace SY.ContentHub.AzureFunctions
{
    public static class Utils
    {
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
    }
}
