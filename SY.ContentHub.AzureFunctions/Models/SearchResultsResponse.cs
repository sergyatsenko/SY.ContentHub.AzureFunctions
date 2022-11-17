using System.Collections.Generic;

namespace SY.ContentHub.AzureFunctions.Models
{
	class Item
	{
		public int id { get; set; }
	}
	class SearchResultsResponse
	{
		public List<Item> items { get; set; }
	}
}
