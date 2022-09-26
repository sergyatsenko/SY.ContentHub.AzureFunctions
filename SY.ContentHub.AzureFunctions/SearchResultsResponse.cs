using System;
using System.Collections.Generic;
using System.Text;

namespace SY.ContentHub.AzureFunctions
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
