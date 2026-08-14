using System;
using System.Collections.Generic;
using System.Linq;
using NXOpen;
using NXOpen.UF;
using CADValidator.Models;
using CADValidator.Rules; // Added to access your separate rule files

namespace CADValidator.Core
{
    public class ValidatorEngine
    {
        private Session theSession;
        private UFSession theUfSession;

        private bool doNamingCheck;
        private bool doAttributeCheck;
        private bool doLifecycleCheck;
        private bool doDrawingCheck;
        private bool doBomCheck;

        public ValidatorEngine(bool naming, bool attributes, bool lifecycle, bool drawing, bool bom)
        {
            theSession = Session.GetSession();
            theUfSession = UFSession.GetUFSession();

            doNamingCheck = naming;
            doAttributeCheck = attributes;
            doLifecycleCheck = lifecycle;
            doDrawingCheck = drawing;
            doBomCheck = bom;
        }

        public List<ValidationResult> ProcessFiles(List<string> filePaths)
        {
            List<ValidationResult> masterResults = new List<ValidationResult>();

            foreach (string path in filePaths)
            {
                BasePart loadedPart = null;
                PartLoadStatus loadStatus = null;

                try
                {
                    loadedPart = theSession.Parts.OpenBase(path, out loadStatus);

                    if (loadedPart != null && loadedPart is Part workPart)
                    {
                        string fileName = workPart.Leaf.ToUpper();
                        bool hasComponents = workPart.ComponentAssembly.RootComponent != null;
                        bool hasDrawingSheets = workPart.DraftingDrawingSheets.ToArray().Length > 0;
                        bool isSeparateDrawing = fileName.Contains("_DRW");
                        bool isAssembly = hasComponents && !isSeparateDrawing;

                        string fileType = isSeparateDrawing ? "DRW" : (isAssembly ? "ASM" : "MOD");

                        string parsedPartNumber = "";
                        string parsedRevision = "";
                        string parsedDocType = "";

                        // 1. HARD STOP NAMING CHECK
                        if (doNamingCheck)
                        {
                            bool namingPassed = ValidateFileName(fileName, fileType, masterResults, out parsedPartNumber, out parsedRevision, out parsedDocType);

                            if (!namingPassed) continue;
                        }
                        else
                        {
                            SilentParseFileName(fileName, out parsedPartNumber, out parsedRevision, out parsedDocType);
                        }

                        // 2. ROUTE TO ATTRIBUTES
                        if (doAttributeCheck)
                        {
                            ValidateAttributes(workPart, fileType, parsedPartNumber, parsedRevision, parsedDocType, masterResults);
                        }

                        // 3. ROUTE TO BOM CHECK (Calling your external BOMRules.cs)
                        if (doBomCheck)
                        {
                            masterResults.AddRange(CADValidator.Rules.BOMRules.RunBOMChecks(workPart, fileName, fileType, theUfSession));
                        }

                        // 4. ROUTE TO DRAWING CHECK (Calling your external DrawingRules.cs)
                        if (doDrawingCheck)
                        {
                            masterResults.AddRange(CADValidator.Rules.DrawingRules.RunDrawingChecks(workPart, fileName, fileType));
                        }
                    }
                    else
                    {
                        masterResults.Add(new ValidationResult(path, "UNKNOWN", "File Load", ValidationStatus.Fail, "Could not open file in NX."));
                    }
                }
                catch (Exception ex)
                {
                    masterResults.Add(new ValidationResult(path, "ERROR", "System", ValidationStatus.Fail, $"Crash during processing: {ex.Message}"));
                }
                finally
                {
                    if (loadedPart != null)
                    {
                        try { /* loadedPart.Close(BasePart.CloseWholeTree.False, BasePart.CloseModified.CloseModified, null); */ }
                        catch { }
                    }
                    if (loadStatus != null) loadStatus.Dispose();
                }
            }

            return masterResults;
        }

        private bool ValidateFileName(string cleanFileName, string fileType, List<ValidationResult> results, out string partNum, out string rev, out string docType)
        {
            partNum = ""; rev = ""; docType = "";

            string pattern = @"^([A-Z]{3}-\d{3})_([A-Z])_(MOD|DRW|ASM)$";
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(cleanFileName, pattern);

            if (match.Success)
            {
                partNum = match.Groups[1].Value;
                rev = match.Groups[2].Value;
                docType = match.Groups[3].Value;

                results.Add(new ValidationResult(cleanFileName, fileType, "Naming", ValidationStatus.Pass, "File name matches the strict enterprise convention."));
                return true;
            }
            else
            {
                results.Add(new ValidationResult(cleanFileName, fileType, "Naming", ValidationStatus.Fail, "Invalid format. Expected: XXX-000_X_TYPE (e.g., BVS-100_A_ASM)."));
                return false;
            }
        }

        private void SilentParseFileName(string cleanFileName, out string partNum, out string rev, out string docType)
        {
            partNum = ""; rev = ""; docType = "";
            string pattern = @"^([A-Z]{3}-\d{3})_([A-Z])_(MOD|DRW|ASM)$";
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(cleanFileName, pattern);
            if (match.Success)
            {
                partNum = match.Groups[1].Value;
                rev = match.Groups[2].Value;
                docType = match.Groups[3].Value;
            }
        }

        private void ValidateAttributes(NXOpen.Part part, string fileType, string parsedPartNum, string parsedRev, string parsedDocType, List<ValidationResult> results)
        {
            bool isEmbedded = false;
            if (part.HasUserAttribute("DRAWING_LOCATION", NXObject.AttributeType.String, -1))
            {
                string loc = part.GetStringUserAttribute("DRAWING_LOCATION", -1).ToUpper();
                if (loc == "EMBEDDED") isEmbedded = true;
            }

            string[] modList = { "DESCRIPTION", "DOC_TYPE", "HAS_DRAWING", "LIFECYCLE_STATUS", "OWNER_DEPT", "PART_NUMBER", "REVISION", "MATERIAL" };
            string[] asmList = { "TOTAL_COMPONENTS", "ASSEMBLY_TYPE", "BOM_REQUIRED", "DESCRIPTION", "DOC_TYPE", "DRAWING_LOCATION", "HAS_DRAWING", "LIFECYCLE_STATUS", "OWNER_DEPT", "PART_NUMBER", "REVISION" };
            string[] drwList = { "APPROVED_BY", "CHECKED_BY", "DESCRIPTION", "DOC_TYPE", "DRAWING_NUMBER", "DRAWING_TITLE", "DRAWN_BY", "FIRST_ISSUE_DATE", "GENERAL_TOL_NOTE", "LIFECYCLE_STATUS", "MAIN_SCALE", "OWNER_DEPT", "PART_NUMBER", "PROJECTION_TYPE", "REVISION", "SHEET_COUNT" };

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
                            results.Add(new ValidationResult(part.Name, fileType, "Attributes", ValidationStatus.Pass, $"Found '{upperReqAttr}': {intValue}"));
                        }
                        else
                        {
                            results.Add(new ValidationResult(part.Name, fileType, "Attributes", ValidationStatus.Fail, $"Missing required integer attribute: '{upperReqAttr}'"));
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
                                results.Add(new ValidationResult(part.Name, fileType, "Attributes", ValidationStatus.Fail, $"Attribute '{upperReqAttr}' is blank."));
                                continue;
                            }

                            bool failedCrossCheck = false;

                            if (upperReqAttr == "DOC_TYPE" && upperAttrValue != parsedDocType)
                            {
                                results.Add(new ValidationResult(part.Name, fileType, "Attributes", ValidationStatus.Fail, $"DOC_TYPE ({upperAttrValue}) does not match file name ({parsedDocType})."));
                                failedCrossCheck = true;
                            }
                            else if (upperReqAttr == "REVISION" && upperAttrValue != parsedRev)
                            {
                                results.Add(new ValidationResult(part.Name, fileType, "Attributes", ValidationStatus.Fail, $"REVISION ({upperAttrValue}) does not match file name ({parsedRev})."));
                                failedCrossCheck = true;
                            }
                            else if (upperReqAttr == "PART_NUMBER" && upperAttrValue != parsedPartNum)
                            {
                                results.Add(new ValidationResult(part.Name, fileType, "Attributes", ValidationStatus.Fail, $"PART_NUMBER ({upperAttrValue}) does not match file name ({parsedPartNum})."));
                                failedCrossCheck = true;
                            }
                            else if (upperReqAttr == "DRAWING_NUMBER" && upperAttrValue != parsedPartNum)
                            {
                                results.Add(new ValidationResult(part.Name, fileType, "Attributes", ValidationStatus.Fail, $"DRAWING_NUMBER ({upperAttrValue}) must match PART_NUMBER ({parsedPartNum})."));
                                failedCrossCheck = true;
                            }

                            if (!failedCrossCheck)
                            {
                                results.Add(new ValidationResult(part.Name, fileType, "Attributes", ValidationStatus.Pass, $"Found '{upperReqAttr}': {attrValue}"));
                            }
                        }
                        else
                        {
                            results.Add(new ValidationResult(part.Name, fileType, "Attributes", ValidationStatus.Fail, $"Missing required attribute: '{upperReqAttr}'"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new ValidationResult(part.Name, fileType, "Attributes", ValidationStatus.Fail, $"Error checking '{upperReqAttr}': {ex.Message}"));
                }
            }
        }
    }
}