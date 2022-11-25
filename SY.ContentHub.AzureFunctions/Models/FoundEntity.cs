//#r "Newtonsoft.Json"

using Stylelabs.M.Sdk.Contracts.Base;
using System.Collections.Generic;

namespace SY.ContentHub.AzureFunctions.Models
{
	public class FoundEntity
	{
		public IEntity Entity { get; set; }
		public Dictionary<string, List<dynamic>> Relations { get; set; }
		//= new Dictionary<string, List<dynamic>>();
		//public List<RelationIDs> RelatedEntityIDs { get; set; } = new List<RelationIDs>();
		//public IList<long> RelatedIds { get; set; }
	}
}
