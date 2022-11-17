//#r "Newtonsoft.Json"

using Newtonsoft.Json.Linq;
using System;

namespace SY.ContentHub.AzureFunctions.Models
{
		class UpsertEntityRequest
		{
			public EntitySearch entitySearch;
			//public List<EntityProperty> properties;
			public JObject properties;
			//public dynamic entityData;
			public void Validate()
			{
				if (entitySearch == null) throw new ArgumentException("entitySearchField is required and cannot be empty");
				if (entitySearch.parentEntitySearchField == null) throw new ArgumentException("entitySearchField is required and cannot be emoty");
				entitySearch.parentEntitySearchField.Validate();
			}
	}
}

