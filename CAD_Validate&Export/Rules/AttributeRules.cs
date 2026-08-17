using System;
using System.Collections.Generic;
using System.Linq;
using NXOpen;
using CADValidator.Models;

namespace CADValidator.Rules
{
    public class AttributeRules
    {
        public static List<ValidationResult> RunAttributeChecks(NXOpen.Part part, string fileName, string fileType, string parsedPartNum, string parsedRev, string parsedDocType)
        {
            List<ValidationResult> results = new List<ValidationResult>();

            bool isEmbedded = false;
            if (part.HasUserAttribute("DRAWING_LOCATION", NXObject.AttributeType.String, -1))
            {
                string loc = part.GetStringUserAttribute("DRAWING_LOCATION", -1).ToUpper();
                if (loc == "EMBEDDED") isEmbedded = true;
            }

            string[] modList = { "DESCRIPTION", "DOC_TYPE", "HAS_DRAWING", "OWNER_DEPT", "PART_NUMBER", "REVISION", "MATERIAL" };
            string[] asmList = { "TOTAL_COMPONENTS", "ASSEMBLY_TYPE", "BOM_REQUIRED", "DESCRIPTION", "DOC_TYPE", "DRAWING_LOCATION", "HAS_DRAWING", "OWNER_DEPT", "PART_NUMBER", "REVISION" };
            string[] drwList = { "APPROVED_BY", "CHECKED_BY", "DESCRIPTION", "DOC_TYPE", "DRAWING_NUMBER", "DRAWING_TITLE", "DRAWN_BY", "FIRST_ISSUE_DATE", "GENERAL_TOL_NOTE", "MAIN_SCALE", "OWNER_DEPT", "PART_NUMBER", "PROJECTION_TYPE", "REVISION", "SHEET_COUNT" };

            HashSet<string> requiredAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (fileType == "MOD")
            {
                requiredAttributes.UnionWith(modList);
            }
            else if (fileType == "ASM")
            {
                requiredAttributes.UnionWith(asmList);
            }
            else if (fileType == "DRW")
            {
                requiredAttributes.UnionWith(drwList);
            }

            if (isEmbedded && (fileType == "MOD" || fileType == "ASM"))
            {
                requiredAttributes.UnionWith(drwList);
            }

            foreach (string reqAttr in requiredAttributes)
            {
                string upperReqAttr = reqAttr.ToUpper();

                try
                {
                    if (upperReqAttr == "SHEET_COUNT" || upperReqAttr == "TOTAL_COMPONENTS")
                    {
                        if (part.HasUserAttribute(upperReqAttr, NXObject.AttributeType.Integer, -1))
                        {
                            int intValue = part.GetIntegerUserAttribute(upperReqAttr, -1);
                            results.Add(new ValidationResult(fileName, fileType, "Attributes", ValidationStatus.Pass, $"Found '{upperReqAttr}': {intValue}"));
                        }
                        else
                        {
                            results.Add(new ValidationResult(fileName, fileType, "Attributes", ValidationStatus.Fail, $"Missing required integer attribute: '{upperReqAttr}'"));
                        }
                    }
                    else
                    {
                        if (part.HasUserAttribute(upperReqAttr, NXObject.AttributeType.String, -1))
                        {
                            string attrValue = part.GetStringUserAttribute(upperReqAttr, -1).Trim();
                            string upperAttrValue = attrValue.ToUpper();

                            if (string.IsNullOrWhiteSpace(attrValue))
                            {
                                results.Add(new ValidationResult(fileName, fileType, "Attributes", ValidationStatus.Fail, $"Attribute '{upperReqAttr}' is blank."));
                                continue;
                            }

                            bool failedCrossCheck = false;

                            if (upperReqAttr == "DOC_TYPE" && upperAttrValue != parsedDocType)
                            {
                                results.Add(new ValidationResult(fileName, fileType, "Attributes", ValidationStatus.Fail, $"DOC_TYPE ({upperAttrValue}) does not match file name ({parsedDocType})."));
                                failedCrossCheck = true;
                            }
                            else if (upperReqAttr == "REVISION" && upperAttrValue != parsedRev)
                            {
                                results.Add(new ValidationResult(fileName, fileType, "Attributes", ValidationStatus.Fail, $"REVISION ({upperAttrValue}) does not match file name ({parsedRev})."));
                                failedCrossCheck = true;
                            }
                            else if (upperReqAttr == "PART_NUMBER" && upperAttrValue != parsedPartNum)
                            {
                                results.Add(new ValidationResult(fileName, fileType, "Attributes", ValidationStatus.Fail, $"PART_NUMBER ({upperAttrValue}) does not match file name ({parsedPartNum})."));
                                failedCrossCheck = true;
                            }
                            else if (upperReqAttr == "DRAWING_NUMBER" && upperAttrValue != parsedPartNum)
                            {
                                results.Add(new ValidationResult(fileName, fileType, "Attributes", ValidationStatus.Fail, $"DRAWING_NUMBER ({upperAttrValue}) must match PART_NUMBER ({parsedPartNum})."));
                                failedCrossCheck = true;
                            }

                            if (!failedCrossCheck)
                            {
                                results.Add(new ValidationResult(fileName, fileType, "Attributes", ValidationStatus.Pass, $"Found '{upperReqAttr}': {attrValue}"));
                            }
                        }
                        else
                        {
                            results.Add(new ValidationResult(fileName, fileType, "Attributes", ValidationStatus.Fail, $"Missing required attribute: '{upperReqAttr}'"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new ValidationResult(fileName, fileType, "Attributes", ValidationStatus.Fail, $"Error checking '{upperReqAttr}': {ex.Message}"));
                }
            }

            return results;
        }
    }
}