//#r "Newtonsoft.Json"

using System.Collections.Generic;

namespace SY.ContentHub.AzureFunctions.Models
{
	public class Root
	{
		public List<Child> children { get; set; }
	}
}
