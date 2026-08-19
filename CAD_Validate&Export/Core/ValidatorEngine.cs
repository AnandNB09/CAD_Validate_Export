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
        private readonly Session theSession;
        private readonly UFSession theUfSession;

        private readonly bool doNamingCheck;
        private readonly bool doAttributeCheck;
        private readonly bool doLifecycleCheck;
        private readonly bool doDrawingCheck;
        private readonly bool doBomCheck;

        public ValidatorEngine(
            bool naming,
            bool attributes,
            bool lifecycle,
            bool drawing,
            bool bom)
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

            if (filePaths == null || filePaths.Count == 0)
            {
                return masterResults;
            }

            foreach (string path in filePaths)
            {
                BasePart loadedPart = null;
                PartLoadStatus loadStatus = null;

                try
                {
                    loadedPart = theSession.Parts.OpenBase(
                        path,
                        out loadStatus);

                    if (loadedPart == null || !(loadedPart is Part workPart))
                    {
                        masterResults.Add(
                            new ValidationResult(
                                path,
                                "UNKNOWN",
                                "File Load",
                                ValidationStatus.Fail,
                                "Could not open file in NX."));

                        continue;
                    }

                    string fileName = workPart.Leaf.ToUpperInvariant();

                    // ==========================================================
                    // 1. ESTABLISH DOCUMENT TYPE
                    // ==========================================================

                    string fileType = DetermineFileType(fileName);

                    // ==========================================================
                    // 2. STRUCTURAL CONSISTENCY CHECK
                    // ==========================================================

                    bool hasRoot =
                        workPart.ComponentAssembly.RootComponent != null;

                    bool hasChildren =
                        hasRoot &&
                        workPart.ComponentAssembly.RootComponent
                            .GetChildren()
                            .Length > 0;

                    // A MOD file should not contain an assembly structure.
                    if (fileType == "MOD" && hasChildren)
                    {
                        masterResults.Add(
                            new ValidationResult(
                                fileName,
                                fileType,
                                "Structure Check",
                                ValidationStatus.Fail,
                                "Structural mismatch: File is named as a Part (_MOD) but contains an assembly structure."));
                    }

                    string parsedPartNumber = string.Empty;
                    string parsedRevision = string.Empty;
                    string parsedDocType = string.Empty;

                    // ==========================================================
                    // 3. NAMING VALIDATION / FILE NAME PARSING
                    // ==========================================================

                    if (doNamingCheck)
                    {
                        bool namingPassed = ValidateFileName(
                            fileName,
                            fileType,
                            masterResults,
                            out parsedPartNumber,
                            out parsedRevision,
                            out parsedDocType);

                        // Naming validation is treated as a hard stop because
                        // downstream attribute cross-checks depend on the
                        // parsed filename values.
                        if (!namingPassed)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        SilentParseFileName(
                            fileName,
                            out parsedPartNumber,
                            out parsedRevision,
                            out parsedDocType);
                    }

                    // ==========================================================
                    // 4. ATTRIBUTE VALIDATION
                    // ==========================================================

                    if (doAttributeCheck)
                    {
                        masterResults.AddRange(
                            AttributeRules.RunAttributeChecks(
                                workPart,
                                fileName,
                                fileType,
                                parsedPartNumber,
                                parsedRevision,
                                parsedDocType));
                    }

                    // ==========================================================
                    // 5. LIFECYCLE VALIDATION
                    // ==========================================================

                    if (doLifecycleCheck)
                    {
                        ValidateLifecycle(
                            workPart,
                            fileName,
                            fileType,
                            masterResults);
                    }

                    // ==========================================================
                    // 6. BOM VALIDATION
                    // ==========================================================

                    if (doBomCheck)
                    {
                        masterResults.AddRange(
                            BOMRules.RunBOMChecks(
                                workPart,
                                fileName,
                                fileType,
                                theUfSession));
                    }

                    // ==========================================================
                    // 7. DRAWING VALIDATION
                    // ==========================================================

                    if (doDrawingCheck)
                    {
                        masterResults.AddRange(
                            DrawingRules.RunDrawingChecks(
                                workPart,
                                fileName,
                                fileType));
                    }
                }
                catch (Exception ex)
                {
                    masterResults.Add(
                        new ValidationResult(
                            path,
                            "ERROR",
                            "System",
                            ValidationStatus.Fail,
                            $"Crash during processing: {ex.Message}"));
                }
                finally
                {
                    // Always close the part opened for this validation cycle.
                    if (loadedPart != null)
                    {
                        try
                        {
                            loadedPart.Close(
                                BasePart.CloseWholeTree.False,
                                BasePart.CloseModified.CloseModified,
                                null);
                        }
                        catch
                        {
                            // Do not allow cleanup failure to interrupt
                            // processing of subsequent files.
                        }
                    }

                    if (loadStatus != null)
                    {
                        try
                        {
                            loadStatus.Dispose();
                        }
                        catch
                        {
                            // Ignore cleanup errors.
                        }
                    }
                }
            }

            return masterResults;
        }

        private string DetermineFileType(string fileName)
        {
            if (fileName.Contains("_DRW"))
            {
                return "DRW";
            }

            if (fileName.Contains("_ASM"))
            {
                return "ASM";
            }

            if (fileName.Contains("_MOD"))
            {
                return "MOD";
            }

            return "MOD";
        }

        private bool ValidateFileName(
            string cleanFileName,
            string fileType,
            List<ValidationResult> results,
            out string partNum,
            out string rev,
            out string docType)
        {
            partNum = string.Empty;
            rev = string.Empty;
            docType = string.Empty;

            string pattern =
                @"^([A-Z]{3}-\d{3})_([A-Z])_(MOD|DRW|ASM)$";

            System.Text.RegularExpressions.Match match =
                System.Text.RegularExpressions.Regex.Match(
                    cleanFileName,
                    pattern);

            if (match.Success)
            {
                partNum = match.Groups[1].Value;
                rev = match.Groups[2].Value;
                docType = match.Groups[3].Value;

                results.Add(
                    new ValidationResult(
                        cleanFileName,
                        fileType,
                        "Naming",
                        ValidationStatus.Pass,
                        "File name matches the strict enterprise convention."));

                return true;
            }

            results.Add(
                new ValidationResult(
                    cleanFileName,
                    fileType,
                    "Naming",
                    ValidationStatus.Fail,
                    "Invalid format. Expected: XXX-000_X_TYPE."));

            return false;
        }

        private void SilentParseFileName(
            string cleanFileName,
            out string partNum,
            out string rev,
            out string docType)
        {
            partNum = string.Empty;
            rev = string.Empty;
            docType = string.Empty;

            string pattern =
                @"^([A-Z]{3}-\d{3})_([A-Z])_(MOD|DRW|ASM)$";

            System.Text.RegularExpressions.Match match =
                System.Text.RegularExpressions.Regex.Match(
                    cleanFileName,
                    pattern);

            if (match.Success)
            {
                partNum = match.Groups[1].Value;
                rev = match.Groups[2].Value;
                docType = match.Groups[3].Value;
            }
        }

        private void ValidateLifecycle(
            Part part,
            string fileName,
            string fileType,
            List<ValidationResult> results)
        {
            const string lifecycleAttribute = "LIFECYCLE_STATUS";

            try
            {
                if (!part.HasUserAttribute(
                        lifecycleAttribute,
                        NXObject.AttributeType.String,
                        -1))
                {
                    results.Add(
                        new ValidationResult(
                            fileName,
                            fileType,
                            "Lifecycle",
                            ValidationStatus.Fail,
                            $"Missing required attribute: '{lifecycleAttribute}'"));

                    return;
                }

                string status =
                    part.GetStringUserAttribute(
                        lifecycleAttribute,
                        -1)
                    .Trim()
                    .ToUpperInvariant();

                if (string.IsNullOrWhiteSpace(status))
                {
                    results.Add(
                        new ValidationResult(
                            fileName,
                            fileType,
                            "Lifecycle",
                            ValidationStatus.Fail,
                            $"{lifecycleAttribute} attribute is blank."));

                    return;
                }

                // These are the lifecycle states currently accepted
                // by this application.
                string[] validStatuses =
                {
                    "RELEASED",
                    "APPROVED",
                    "IN_WORK"
                };

                if (validStatuses.Contains(
                        status,
                        StringComparer.OrdinalIgnoreCase))
                {
                    results.Add(
                        new ValidationResult(
                            fileName,
                            fileType,
                            "Lifecycle",
                            ValidationStatus.Pass,
                            $"Valid lifecycle state: {status}"));
                }
                else
                {
                    results.Add(
                        new ValidationResult(
                            fileName,
                            fileType,
                            "Lifecycle",
                            ValidationStatus.Fail,
                            $"State '{status}' is not approved for export."));
                }
            }
            catch (Exception ex)
            {
                results.Add(
                    new ValidationResult(
                        fileName,
                        fileType,
                        "Lifecycle",
                        ValidationStatus.Fail,
                        $"Error checking Lifecycle: {ex.Message}"));
            }
        }
    }
}