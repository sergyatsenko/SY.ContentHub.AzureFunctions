//#r "Newtonsoft.Json"

using System;
using System.Collections.Generic;

namespace SY.ContentHub.AzureFunctions.Models
{
	public class SearchEntitiesByFieldValueRequest
	{
		public EntitySearch entitySearch { get; set; }
		public List<string> relations { get; set; }
		public void Validate()
		{
			if (entitySearch?.entitySearchField == null) throw new ArgumentException("entitySearchField");
		}
	}
}
