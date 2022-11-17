//#r "Newtonsoft.Json"

namespace SY.ContentHub.AzureFunctions.Models
{
	public class EntitySearch
	{
		public SearchEntityRequestBase entitySearchField;
		public SearchEntityRequestBase parentEntitySearchField;
		public SearchEntityRequestBase childEntitySearchField;
	}
}
