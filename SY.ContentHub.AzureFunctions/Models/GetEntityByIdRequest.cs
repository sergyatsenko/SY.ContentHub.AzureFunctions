//#r "Newtonsoft.Json"

using System;
using System.Collections.Generic;

namespace SY.ContentHub.AzureFunctions.Models
{
	public class GetEntityByIdRequest : GetEntityRequest
	{
		public long entityId;
		public bool? includePublicLinks;
		public List<string> relations;
		public void Validate()
		{
			if (entityId <= 0) throw new ArgumentException("entityId");
		}
	}
}
