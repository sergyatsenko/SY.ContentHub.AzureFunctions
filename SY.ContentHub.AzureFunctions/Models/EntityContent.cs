//#r "Newtonsoft.Json"

using System.Collections.Generic;

namespace SY.ContentHub.AzureFunctions.Models
{
	public class EntityInfo
	{
		public EntityDataResponse Entity {get; set;}
		public Dictionary<string, List<dynamic>> Relations { get; set; }
	}
}
