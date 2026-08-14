using System;
using System.Collections.Generic;
using NXOpen;
using CADValidator.Models;

namespace CADValidator.Rules
{
    public class BOMRules
    {
        public static List<ValidationResult> RunBOMChecks(Part workPart, string fileName, string fileType, NXOpen.UF.UFSession ufSession)
        {
            List<ValidationResult> results = new List<ValidationResult>();

            if (fileType == "MOD")
            {
                results.Add(new ValidationResult(fileName, fileType, "BOM Check", ValidationStatus.Info, "Piece Parts do not require a BOM"));
                return results;
            }

            try
            {
                NXOpen.Assemblies.Component rootComp = workPart.ComponentAssembly.RootComponent;

                if (rootComp != null && rootComp.GetChildren().Length > 0)
                {
                    results.Add(new ValidationResult(fileName, fileType, "BOM Check", ValidationStatus.Pass, $"Found {rootComp.GetChildren().Length} child component(s)."));
                }
                else if (fileType == "ASM")
                {
                    results.Add(new ValidationResult(fileName, fileType, "BOM Check", ValidationStatus.Fail, "Assembly file contains zero components."));
                }
            }
            catch (Exception ex)
            {
                results.Add(new ValidationResult(fileName, fileType, "BOM Check", ValidationStatus.Fail, $"Error checking BOM: {ex.Message}"));
            }

            return results;
        }
    }
}