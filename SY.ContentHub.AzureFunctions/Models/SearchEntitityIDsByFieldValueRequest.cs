//#r "Newtonsoft.Json"

using System;

namespace SY.ContentHub.AzureFunctions.Models
{
	public class SearchEntitityIDsByFieldValueRequest
	{
		public EntitySearch entitySearch { get; set; }
		public void Validate()
		{
			if (entitySearch?.entitySearchField == null) throw new ArgumentException("entitySearchField");
		}
	}
}
