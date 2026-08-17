using System;
using System.Collections.Generic;
using System.Linq;
using NXOpen;
using NXOpen.UF;
using CADValidator.Models;
using CADValidator.Rules;

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

                        // ==========================================================
                        // 1. ESTABLISH INTENT (Derive expected type from filename)
                        // ==========================================================
                        string fileType = "MOD"; // Default fallback
                        if (fileName.Contains("_DRW")) fileType = "DRW";
                        else if (fileName.Contains("_ASM")) fileType = "ASM";
                        else if (fileName.Contains("_MOD")) fileType = "MOD";

                        // ==========================================================
                        // 2. STRUCTURAL CONSISTENCY CHECK (Intent vs. Reality)
                        // ==========================================================
                        bool hasRoot = workPart.ComponentAssembly.RootComponent != null;
                        bool hasChildren = hasRoot && workPart.ComponentAssembly.RootComponent.GetChildren().Length > 0;

                        // Catch the scenario where a MOD illegally contains assembly children
                        if (fileType == "MOD" && hasChildren)
                        {
                            masterResults.Add(new ValidationResult(fileName, fileType, "Structure Check", ValidationStatus.Fail, "Structural mismatch: File is named as a Part (_MOD) but contains an assembly structure."));
                        }

                        // NOTE: Mismatches for empty _ASM and empty _DRW files are automatically 
                        // caught by your existing BOMRules and DrawingRules engines!

                        string parsedPartNumber = "";
                        string parsedRevision = "";
                        string parsedDocType = "";

                        // 3. HARD STOP NAMING CHECK
                        if (doNamingCheck)
                        {
                            bool namingPassed = ValidateFileName(fileName, fileType, masterResults, out parsedPartNumber, out parsedRevision, out parsedDocType);

                            if (!namingPassed) continue;
                        }
                        else
                        {
                            SilentParseFileName(fileName, out parsedPartNumber, out parsedRevision, out parsedDocType);
                        }

                        // 4. ROUTE TO ATTRIBUTES
                        if (doAttributeCheck)
                        {
                            masterResults.AddRange(CADValidator.Rules.AttributeRules.RunAttributeChecks(workPart, fileName, fileType, parsedPartNumber, parsedRevision, parsedDocType));
                        }

                        // 5. ROUTE TO LIFECYCLE CHECK
                        if (doLifecycleCheck)
                        {
                            ValidateLifecycle(workPart, fileName, fileType, masterResults);
                        }

                        // 6. ROUTE TO BOM CHECK
                        if (doBomCheck)
                        {
                            masterResults.AddRange(CADValidator.Rules.BOMRules.RunBOMChecks(workPart, fileName, fileType, theUfSession));
                        }

                        // 7. ROUTE TO DRAWING CHECK
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
                        try { loadedPart.Close(BasePart.CloseWholeTree.False, BasePart.CloseModified.CloseModified, null); }
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

        private void ValidateLifecycle(NXOpen.Part part, string fileName, string fileType, List<ValidationResult> results)
        {
            try
            {
                if (part.HasUserAttribute("LIFECYCLE_STATUS", NXObject.AttributeType.String, -1))
                {
                    string status = part.GetStringUserAttribute("LIFECYCLE_STATUS", -1).Trim().ToUpper();

                    string[] validStatuses = { "RELEASED", "APPROVED", "IN_WORK" };

                    if (string.IsNullOrWhiteSpace(status))
                    {
                        results.Add(new ValidationResult(fileName, fileType, "Lifecycle", ValidationStatus.Fail, "LIFECYCLE_STATUS attribute is blank."));
                    }
                    else if (validStatuses.Contains(status))
                    {
                        results.Add(new ValidationResult(fileName, fileType, "Lifecycle", ValidationStatus.Pass, $"Valid lifecycle state: {status}"));
                    }
                    else
                    {
                        results.Add(new ValidationResult(fileName, fileType, "Lifecycle", ValidationStatus.Fail, $"State '{status}' is not approved for export."));
                    }
                }
                else
                {
                    results.Add(new ValidationResult(fileName, fileType, "Lifecycle", ValidationStatus.Fail, "Missing required attribute: 'LIFECYCLE_STATUS'"));
                }
            }
            catch (Exception ex)
            {
                results.Add(new ValidationResult(fileName, fileType, "Lifecycle", ValidationStatus.Fail, $"Error checking Lifecycle: {ex.Message}"));
            }
        }
    }
}