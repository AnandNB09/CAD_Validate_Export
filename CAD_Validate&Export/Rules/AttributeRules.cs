using CADValidator.Models;
using NXOpen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CAD_Validate_Export.Rules
{
    public class AttributeRules
    {
        public static List<ValidationResult> RunAttributeChecks(
          Part workPart,
          string fileName,
          string fileType)
        {
            List<ValidationResult> validationResultList = new List<ValidationResult>();
            string[] strArray = (string[])null;
            switch (fileType)
            {
                case "MOD":
                    strArray = new string[7]
                    {
          "PART_NUMBER",
          "DESCRIPTION",
          "REVISION",
          "DOC_TYPE",
          "LIFECYCLE_STATUS",
          "OWNER_DEPT",
          "MATERIAL"
                    };
                    break;
                case "ASM":
                    strArray = new string[6]
                    {
          "PART_NUMBER",
          "REVISION",
          "ASSEMBLY_TYPE",
          "BOM_REQUIRED",
          "HAS_DRAWING",
          "DRAWING_LOCATION"
                    };
                    validationResultList.Add(AttributeRules.CheckIntegerAttribute(workPart, fileName, fileType, "TOTAL_COMPONENTS"));
                    break;
                case "DRW":
                    strArray = new string[2]
                    {
          "PART_NUMBER",
          "REVISION"
                    };
                    break;
            }
            if (strArray != null)
            {
                foreach (string attributeName in strArray)
                    validationResultList.Add(AttributeRules.CheckStringAttribute(workPart, fileName, fileType, attributeName));
            }
            return validationResultList;
        }

        private static ValidationResult CheckStringAttribute(
          Part part,
          string fileName,
          string fileType,
          string attributeName)
        {
            try
            {
                if (!((NXObject)part).HasUserAttribute(attributeName, (NXObject.AttributeType)5, -1))
                    return new ValidationResult(fileName, fileType, "Attributes", ValidationStatus.Fail, $"Missing required attribute: '{attributeName}'");
                string stringAttribute = ((NXObject)part).GetStringAttribute(attributeName);
                return string.IsNullOrWhiteSpace(stringAttribute) ? new ValidationResult(fileName, fileType, "Attributes", ValidationStatus.Fail, $"'{attributeName}' exists but is empty") : new ValidationResult(fileName, fileType, "Attributes", ValidationStatus.Pass, $"Found '{attributeName}': {stringAttribute}");
            }
            catch (Exception ex)
            {
                return new ValidationResult(fileName, fileType, "Attributes", ValidationStatus.Fail, $"NX Exception on '{attributeName}': {ex.Message}");
            }
        }

        private static ValidationResult CheckIntegerAttribute(
          Part part,
          string fileName,
          string fileType,
          string attributeName)
        {
            try
            {
                if (!((NXObject)part).HasUserAttribute(attributeName, (NXObject.AttributeType)3, -1))
                    return new ValidationResult(fileName, fileType, "Attributes", ValidationStatus.Fail, $"Missing required integer attribute: '{attributeName}'");
                int integerAttribute = ((NXObject)part).GetIntegerAttribute(attributeName);
                return new ValidationResult(fileName, fileType, "Attributes", ValidationStatus.Pass, $"Found '{attributeName}': {integerAttribute}");
            }
            catch (Exception ex)
            {
                return new ValidationResult(fileName, fileType, "Attributes", ValidationStatus.Fail, $"NX Exception on '{attributeName}': {ex.Message}");
            }
        }
    }
}
