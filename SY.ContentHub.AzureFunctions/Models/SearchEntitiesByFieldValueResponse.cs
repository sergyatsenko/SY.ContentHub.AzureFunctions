//#r "Newtonsoft.Json"

using System.Collections.Generic;

namespace SY.ContentHub.AzureFunctions.Models
{
	public class SearchEntitiesByFieldValueResponse
	{
		public List<FoundEntity> Entities { get; set; } = new List<FoundEntity>();
	}
}
