//#r "Newtonsoft.Json"

using Stylelabs.M.Sdk.Contracts.Base;
using System.Collections.Generic;

namespace SY.ContentHub.AzureFunctions.Models
{
	public class RelationIDs
	{ 
		public string RelationName { get; set; }
		public IList<long> IDs { get; set; }
	}
}
