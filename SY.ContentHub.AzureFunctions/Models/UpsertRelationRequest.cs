//#r "Newtonsoft.Json"

using System;

namespace SY.ContentHub.AzureFunctions.Models
{
	public class UpsertRelationRequest
	{
		public EntitySearch entitySearch;
		public EntityData entityData;
		public bool keepExistingRelations = false;
		public bool deleted = false;
		public bool continueOnEmptySearchFields = false;
		public bool continueOnNoFoundEntities = false;
		public void Validate()
		{
			if (entityData == null) throw new ArgumentException("entityData is required and cannot be empty.");
			if (string.IsNullOrEmpty(entityData.relationFieldName)) throw new ArgumentException("entityData.relationFieldName");
			if (entitySearch?.parentEntitySearchField == null) throw new ArgumentException("parentEntitySearchField");
			if (entitySearch?.childEntitySearchField == null) throw new ArgumentException("childEntitySearchField");
			entitySearch.parentEntitySearchField.Validate(continueOnEmptySearchFields);
			entitySearch.childEntitySearchField.Validate(continueOnEmptySearchFields);
		}
	}
}
