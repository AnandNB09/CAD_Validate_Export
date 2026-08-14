using System;
using System.Collections.Generic;
using NXOpen;
using CADValidator.Models;

namespace CADValidator.Rules
{
    public class DrawingRules
    {
        public static List<ValidationResult> RunDrawingChecks(Part workPart, string fileName, string fileType)
        {
            List<ValidationResult> results = new List<ValidationResult>();

            try
            {
                NXOpen.Drawings.DrawingSheet[] sheets = workPart.DraftingDrawingSheets.ToArray();

                if (fileType == "DRW")
                {
                    if (sheets.Length > 0)
                    {
                        results.Add(new ValidationResult(fileName, fileType, "Drawing Check", ValidationStatus.Pass, $"Found {sheets.Length} drafting sheet(s)."));
                    }
                    else
                    {
                        results.Add(new ValidationResult(fileName, fileType, "Drawing Check", ValidationStatus.Fail, "No drafting sheets found in DRW file."));
                    }
                }
                else
                {
                    if (sheets.Length > 0)
                    {
                        results.Add(new ValidationResult(fileName, fileType, "Drawing Check", ValidationStatus.Info, $"Embedded drawing found with {sheets.Length} sheet(s)."));
                    }
                    else
                    {
                        results.Add(new ValidationResult(fileName, fileType, "Drawing Check", ValidationStatus.Info, "No embedded drawing sheets found."));
                    }
                }
            }
            catch (Exception ex)
            {
                results.Add(new ValidationResult(fileName, fileType, "Drawing Check", ValidationStatus.Fail, $"Error scanning for drawings: {ex.Message}"));
            }

            return results;
        }
    }
}