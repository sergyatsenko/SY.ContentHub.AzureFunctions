//#r "Newtonsoft.Json"

namespace SY.ContentHub.AzureFunctions
{
	public static partial class UpsertEntity
	{
		class EntityProperty
		{
			public string name;
			public string type;
			public object value;
		}
	}
}

