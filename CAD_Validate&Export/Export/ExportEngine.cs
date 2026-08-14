using NXOpen;
using System;
using System.Collections.Generic;
using System.IO;

namespace CADValidator.Core
{
    public class ExportEngine
    {
        public static void RunSmartExport(List<ExportCandidate> candidates, string exportFolder)
        {
            Session theSession = Session.GetSession();
            NXOpen.UF.UFSession theUfSession = NXOpen.UF.UFSession.GetUFSession();

            int stepCount = 0;
            int pdfCount = 0;

            Part originalDisplayPart = theSession.Parts.Display;

            // ====================================================================
            // PHASE 1: BACKGROUND STEP EXPORT (Lightning Fast, No Screen Flashing)
            // ====================================================================
            foreach (ExportCandidate candidate in candidates)
            {
                if (candidate.FileType == "MOD" || candidate.FileType == "ASM")
                {
                    try
                    {
                        StepCreator stepCreator = theSession.DexManager.CreateStepCreator();
                        stepCreator.ExportAs = StepCreator.ExportAsOption.Ap214;
                        stepCreator.SettingsFile = @"C:\Program Files\Siemens\DesigncenterNX2512\step214ug\ugstep214.def";

                        stepCreator.ExportFrom = StepCreator.ExportFromOption.ExistingPart;
                        stepCreator.InputFile = candidate.FullPath;
                        stepCreator.OutputFile = Path.Combine(exportFolder, candidate.CleanName + ".stp");

                        stepCreator.ObjectTypes.Solids = true;
                        stepCreator.LayerMask = "1-256";
                        stepCreator.FileSaveFlag = false;
                        stepCreator.ProcessHoldFlag = true;

                        stepCreator.Commit();
                        stepCreator.Destroy();
                        stepCount++;
                    }
                    catch (Exception ex)
                    {
                        UI.GetUI().NXMessageBox.Show("STEP Error", NXMessageBox.DialogType.Error, $"Failed on {candidate.CleanName}: {ex.Message}");
                    }
                }
            }

            // ====================================================================
            // PHASE 2: PDF EXPORT (Screen Switching Suppressed)
            // ====================================================================
            bool isDisplaySuppressed = false;

            try
            {
                theUfSession.Disp.SetDisplay(NXOpen.UF.UFConstants.UF_DISP_SUPPRESS_DISPLAY);
                isDisplaySuppressed = true;

                foreach (ExportCandidate candidate in candidates)
                {
                    if (candidate.FileType == "DRW" || candidate.FileType == "ASM")
                    {
                        PartLoadStatus loadStatus = null;
                        Part exportPart = null;

                        try
                        {
                            foreach (Part loadedPart in theSession.Parts)
                            {
                                if (loadedPart.Name.Equals(candidate.CleanName, StringComparison.OrdinalIgnoreCase))
                                {
                                    exportPart = loadedPart;
                                    break;
                                }
                            }

                            if (exportPart == null)
                            {
                                exportPart = (Part)theSession.Parts.OpenActiveDisplay(candidate.FullPath, DisplayPartOption.AllowAdditional, out loadStatus);
                            }
                            else
                            {
                                theSession.Parts.SetDisplay(exportPart, false, true, out loadStatus);
                            }

                            if (exportPart != null)
                            {
                                NXOpen.Drawings.DrawingSheet[] sheets = exportPart.DraftingDrawingSheets.ToArray();

                                if (sheets.Length > 0)
                                {
                                    theSession.ApplicationSwitchImmediate("UG_APP_DRAFTING");
                                    exportPart.Drafting.EnterDraftingApplication();

                                    try { exportPart.Views.WorkView.UpdateCustomSymbols(); } catch { }
                                    try { exportPart.Drafting.SetTemplateInstantiationIsComplete(true); } catch { }

                                    PrintPDFBuilder pdfBuilder = exportPart.PlotManager.CreatePrintPdfbuilder();
                                    pdfBuilder.Scale = 1.0;
                                    pdfBuilder.Size = PrintPDFBuilder.SizeOption.ScaleFactor;
                                    pdfBuilder.OutputText = PrintPDFBuilder.OutputTextOption.Polylines;
                                    pdfBuilder.Units = PrintPDFBuilder.UnitsOption.English;
                                    pdfBuilder.XDimension = 8.5;
                                    pdfBuilder.YDimension = 11.0;
                                    pdfBuilder.RasterImages = true;

                                    NXOpen.NXObject[] nxSheets = new NXOpen.NXObject[sheets.Length];
                                    for (int i = 0; i < sheets.Length; i++) { nxSheets[i] = sheets[i]; }

                                    pdfBuilder.SourceBuilder.SetSheets(nxSheets);

                                    string pdfPath = Path.Combine(exportFolder, candidate.CleanName + ".pdf");

                                    if (File.Exists(pdfPath)) File.Delete(pdfPath);

                                    pdfBuilder.Filename = pdfPath;
                                    pdfBuilder.Commit();
                                    pdfBuilder.Destroy();
                                    pdfCount++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            UI.GetUI().NXMessageBox.Show("PDF Error", NXMessageBox.DialogType.Error, $"Failed on {candidate.CleanName}: {ex.Message}");
                        }
                        finally
                        {
                            if (exportPart != null && (originalDisplayPart == null || !exportPart.Name.Equals(originalDisplayPart.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                try { exportPart.Close(BasePart.CloseWholeTree.False, BasePart.CloseModified.CloseModified, null); } catch { }
                            }
                            if (loadStatus != null) loadStatus.Dispose();
                        }
                    }
                }
            }
            finally
            {
                if (isDisplaySuppressed)
                {
                    theUfSession.Disp.SetDisplay(NXOpen.UF.UFConstants.UF_DISP_UNSUPPRESS_DISPLAY);
                }
            }

            // ====================================================================
            // 3. RESTORE USER'S SCREEN
            // ====================================================================
            if (originalDisplayPart != null)
            {
                PartLoadStatus restoreStatus = null;
                try { theSession.Parts.SetDisplay(originalDisplayPart, false, true, out restoreStatus); }
                catch { }
                finally { if (restoreStatus != null) restoreStatus.Dispose(); }
            }

            UI.GetUI().NXMessageBox.Show("Smart Export Complete", NXMessageBox.DialogType.Information,
                $"Successfully exported:\n\n{stepCount} STEP file(s)\n{pdfCount} PDF(s).\n\nClick OK to open the destination folder.");

            try { System.Diagnostics.Process.Start("explorer.exe", exportFolder); } catch { }

            // ====================================================================
            // 4. AGGRESSIVE MEMORY WIPE
            // ====================================================================
            try
            {
                if (candidates != null)
                {
                    candidates.Clear();
                    candidates = null;
                }
                exportFolder = string.Empty;

                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
                System.GC.Collect();
            }
            catch { }
        }
    }
}