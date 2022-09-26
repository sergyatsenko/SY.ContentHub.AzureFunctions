using System;
using System.Collections.Generic;
using System.Text;

namespace SY.ContentHub.AzureFunctions
{
	class SearchEntityRequestBase
	{
		public string definitionName;
		public string fieldName;
		public string fieldType;
		public string fieldValue;
		public bool isStringValue = true;
		public void Validate(bool ignoreMissingFieldValue = false)
		{
			if (string.IsNullOrEmpty(definitionName)) throw new ArgumentException("definitionName");
			if (string.IsNullOrEmpty(fieldName)) throw new ArgumentException("fieldName");
			if (string.IsNullOrEmpty(fieldType)) throw new ArgumentException("fieldType");
			if (!ignoreMissingFieldValue && string.IsNullOrEmpty(fieldValue)) throw new ArgumentException("fieldValue");
		}
	}
}
