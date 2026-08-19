using System;
using System.Collections.Generic;
using NXOpen;
using CADValidator.Models;

namespace CADValidator.Rules
{
    public static class AttributeRules
    {
        public static List<ValidationResult> RunAttributeChecks(
            Part part,
            string fileName,
            string fileType,
            string parsedPartNum,
            string parsedRev,
            string parsedDocType)
        {
            List<ValidationResult> results =
                new List<ValidationResult>();

            if (part == null)
            {
                results.Add(
                    new ValidationResult(
                        fileName,
                        fileType,
                        "Attributes",
                        ValidationStatus.Fail,
                        "NX part reference is null."));

                return results;
            }

            bool isEmbedded = false;

            // ==============================================================
            // DRAWING LOCATION
            // ==============================================================

            if (part.HasUserAttribute(
                    "DRAWING_LOCATION",
                    NXObject.AttributeType.String,
                    -1))
            {
                string location =
                    part.GetStringUserAttribute(
                        "DRAWING_LOCATION",
                        -1);

                if (!string.IsNullOrWhiteSpace(location) &&
                    string.Equals(
                        location.Trim(),
                        "EMBEDDED",
                        StringComparison.OrdinalIgnoreCase))
                {
                    isEmbedded = true;
                }
            }

            // ==============================================================
            // REQUIRED ATTRIBUTE DEFINITIONS
            // ==============================================================

            string[] modList =
            {
                "DESCRIPTION",
                "DOC_TYPE",
                "HAS_DRAWING",
                "OWNER_DEPT",
                "PART_NUMBER",
                "REVISION",
                "MATERIAL"
            };

            string[] asmList =
            {
                "TOTAL_COMPONENTS",
                "ASSEMBLY_TYPE",
                "BOM_REQUIRED",
                "DESCRIPTION",
                "DOC_TYPE",
                "DRAWING_LOCATION",
                "HAS_DRAWING",
                "OWNER_DEPT",
                "PART_NUMBER",
                "REVISION"
            };

            string[] drwList =
            {
                "APPROVED_BY",
                "CHECKED_BY",
                "DESCRIPTION",
                "DOC_TYPE",
                "DRAWING_NUMBER",
                "DRAWING_TITLE",
                "DRAWN_BY",
                "FIRST_ISSUE_DATE",
                "GENERAL_TOL_NOTE",
                "MAIN_SCALE",
                "OWNER_DEPT",
                "PART_NUMBER",
                "PROJECTION_TYPE",
                "REVISION",
                "SHEET_COUNT"
            };

            HashSet<string> requiredAttributes =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            switch (fileType)
            {
                case "MOD":
                    requiredAttributes.UnionWith(modList);
                    break;

                case "ASM":
                    requiredAttributes.UnionWith(asmList);
                    break;

                case "DRW":
                    requiredAttributes.UnionWith(drwList);
                    break;
            }

            // An embedded drawing requires drawing metadata
            // even when the source document is a MOD or ASM.
            if (isEmbedded &&
                (fileType == "MOD" || fileType == "ASM"))
            {
                requiredAttributes.UnionWith(drwList);
            }

            // ==============================================================
            // ATTRIBUTE VALIDATION
            // ==============================================================

            foreach (string requiredAttribute in requiredAttributes)
            {
                try
                {
                    ValidateAttribute(
                        part,
                        fileName,
                        fileType,
                        requiredAttribute,
                        parsedPartNum,
                        parsedRev,
                        parsedDocType,
                        results);
                }
                catch (Exception ex)
                {
                    results.Add(
                        new ValidationResult(
                            fileName,
                            fileType,
                            "Attributes",
                            ValidationStatus.Fail,
                            $"Error checking '{requiredAttribute}': {ex.Message}"));
                }
            }

            return results;
        }

        private static void ValidateAttribute(
            Part part,
            string fileName,
            string fileType,
            string attributeName,
            string parsedPartNum,
            string parsedRev,
            string parsedDocType,
            List<ValidationResult> results)
        {
            // ==============================================================
            // INTEGER ATTRIBUTES
            // ==============================================================

            if (IsIntegerAttribute(attributeName))
            {
                if (part.HasUserAttribute(
                        attributeName,
                        NXObject.AttributeType.Integer,
                        -1))
                {
                    int value1 =
                        part.GetIntegerUserAttribute(
                            attributeName,
                            -1);

                    results.Add(
                        new ValidationResult(
                            fileName,
                            fileType,
                            "Attributes",
                            ValidationStatus.Pass,
                            $"Found '{attributeName}': {value1}"));
                }
                else
                {
                    results.Add(
                        new ValidationResult(
                            fileName,
                            fileType,
                            "Attributes",
                            ValidationStatus.Fail,
                            $"Missing required integer attribute: '{attributeName}'"));
                }

                return;
            }

            // ==============================================================
            // STRING ATTRIBUTES
            // ==============================================================

            if (!part.HasUserAttribute(
                    attributeName,
                    NXObject.AttributeType.String,
                    -1))
            {
                results.Add(
                    new ValidationResult(
                        fileName,
                        fileType,
                        "Attributes",
                        ValidationStatus.Fail,
                        $"Missing required attribute: '{attributeName}'"));

                return;
            }

            string value =
                part.GetStringUserAttribute(
                    attributeName,
                    -1);

            if (string.IsNullOrWhiteSpace(value))
            {
                results.Add(
                    new ValidationResult(
                        fileName,
                        fileType,
                        "Attributes",
                        ValidationStatus.Fail,
                        $"Attribute '{attributeName}' is blank."));

                return;
            }

            value = value.Trim();

            // ==============================================================
            // CROSS-VALIDATION
            // ==============================================================

            string crossCheckFailure =
                GetCrossCheckFailureMessage(
                    attributeName,
                    value,
                    parsedPartNum,
                    parsedRev,
                    parsedDocType);

            if (!string.IsNullOrEmpty(crossCheckFailure))
            {
                results.Add(
                    new ValidationResult(
                        fileName,
                        fileType,
                        "Attributes",
                        ValidationStatus.Fail,
                        crossCheckFailure));

                return;
            }

            results.Add(
                new ValidationResult(
                    fileName,
                    fileType,
                    "Attributes",
                    ValidationStatus.Pass,
                    $"Found '{attributeName}': {value}"));
        }

        private static bool IsIntegerAttribute(string attributeName)
        {
            return string.Equals(
                       attributeName,
                       "SHEET_COUNT",
                       StringComparison.OrdinalIgnoreCase)
                   ||
                   string.Equals(
                       attributeName,
                       "TOTAL_COMPONENTS",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string GetCrossCheckFailureMessage(
            string attributeName,
            string attributeValue,
            string parsedPartNum,
            string parsedRev,
            string parsedDocType)
        {
            if (string.Equals(
                    attributeName,
                    "DOC_TYPE",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(parsedDocType) &&
                    !string.Equals(
                        attributeValue,
                        parsedDocType,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return
                        $"DOC_TYPE ({attributeValue}) does not match file name ({parsedDocType}).";
                }
            }

            if (string.Equals(
                    attributeName,
                    "REVISION",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(parsedRev) &&
                    !string.Equals(
                        attributeValue,
                        parsedRev,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return
                        $"REVISION ({attributeValue}) does not match file name ({parsedRev}).";
                }
            }

            if (string.Equals(
                    attributeName,
                    "PART_NUMBER",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(parsedPartNum) &&
                    !string.Equals(
                        attributeValue,
                        parsedPartNum,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return
                        $"PART_NUMBER ({attributeValue}) does not match file name ({parsedPartNum}).";
                }
            }

            if (string.Equals(
                    attributeName,
                    "DRAWING_NUMBER",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(parsedPartNum) &&
                    !string.Equals(
                        attributeValue,
                        parsedPartNum,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return
                        $"DRAWING_NUMBER ({attributeValue}) must match PART_NUMBER ({parsedPartNum}).";
                }
            }

            return string.Empty;
        }
    }
}