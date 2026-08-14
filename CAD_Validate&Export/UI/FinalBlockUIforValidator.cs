using System;
using System.Collections.Generic;
using NXOpen;
using NXOpen.BlockStyler;

namespace CADValidator
{
    public class FinalBlockUIforValidator
    {
        // --- Class Members ---
        private static Session theSession = null;
        private static UI theUI = null;
        private string theDlxFileName;
        private NXOpen.BlockStyler.BlockDialog theDialog;

        // --- UI Blocks ---
        private NXOpen.BlockStyler.TabControl tabMain;
        private NXOpen.BlockStyler.Group fileBrowserGroup;
        private NXOpen.BlockStyler.FileSelection nativeFileBrowser0;
        private NXOpen.BlockStyler.Group folderBrowserGroup;
        private NXOpen.BlockStyler.FolderSelection nativeFolderBrowser0;
        private NXOpen.BlockStyler.Enumeration enum0;

        // --- Validation Rules ---
        private NXOpen.BlockStyler.Group group1;
        private NXOpen.BlockStyler.Toggle chkNaming;
        private NXOpen.BlockStyler.Toggle chkAttributes;
        private NXOpen.BlockStyler.Toggle chkLifecycle;
        private NXOpen.BlockStyler.Toggle chkDrawingFields;
        private NXOpen.BlockStyler.Toggle chkBOM;
        private NXOpen.BlockStyler.Toggle chkExportReady;

        // --- Results & Actions ---
        private NXOpen.BlockStyler.Separator separator0;
        private NXOpen.BlockStyler.Button btnValidate;
        private NXOpen.BlockStyler.Group group2;
        private NXOpen.BlockStyler.Button btnExport;
        private NXOpen.BlockStyler.Button btnGenerateReport;

        // ====================================================================
        // --- IN-MEMORY DATA BANKS & EXPORT QUEUE ---
        // ====================================================================
        private List<string> activeFilePaths = new List<string>();
        private List<CADValidator.Models.ValidationResult> lastValidationResults = new List<CADValidator.Models.ValidationResult>();
        private List<CADValidator.Core.ExportCandidate> approvedExportFiles = new List<CADValidator.Core.ExportCandidate>();

        public bool RequestExport { get; set; } = false;
        public string ExportFolder { get; set; } = "";
        public List<CADValidator.Core.ExportCandidate> ExportCandidates { get; set; } = new List<CADValidator.Core.ExportCandidate>();

        public FinalBlockUIforValidator()
        {
            try
            {
                theSession = Session.GetSession();
                theUI = UI.GetUI();

                string dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string dllDirectory = System.IO.Path.GetDirectoryName(dllPath);

                theDlxFileName = System.IO.Path.Combine(dllDirectory, "FinalBlockUIforValidator.dlx");
                theDialog = theUI.CreateDialog(theDlxFileName);

                theDialog.AddApplyHandler(new NXOpen.BlockStyler.BlockDialog.Apply(apply_cb));
                theDialog.AddOkHandler(new NXOpen.BlockStyler.BlockDialog.Ok(ok_cb));
                theDialog.AddUpdateHandler(new NXOpen.BlockStyler.BlockDialog.Update(update_cb));
                theDialog.AddInitializeHandler(new NXOpen.BlockStyler.BlockDialog.Initialize(initialize_cb));

                // ADDED THIS BACK: Triggers right as the dialog appears on screen
                theDialog.AddDialogShownHandler(new NXOpen.BlockStyler.BlockDialog.DialogShown(dialogShown_cb));
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static void Main()
        {
            FinalBlockUIforValidator theFinalBlockUIforValidator = null;
            try
            {
                theFinalBlockUIforValidator = new FinalBlockUIforValidator();
                theFinalBlockUIforValidator.Launch();

                if (theFinalBlockUIforValidator.RequestExport && theFinalBlockUIforValidator.ExportCandidates.Count > 0)
                {
                    CADValidator.Core.ExportEngine.RunSmartExport(theFinalBlockUIforValidator.ExportCandidates, theFinalBlockUIforValidator.ExportFolder);
                }
            }
            catch (Exception ex)
            {
                theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
            }
            finally
            {
                if (theFinalBlockUIforValidator != null)
                {
                    theFinalBlockUIforValidator.Dispose();
                    theFinalBlockUIforValidator = null;
                }
            }
        }

        public static int GetUnloadOption(string arg)
        {
            return System.Convert.ToInt32(Session.LibraryUnloadOption.Immediately);
        }

        public static void UnloadLibrary(string arg) { }

        public NXOpen.BlockStyler.BlockDialog.DialogResponse Launch()
        {
            NXOpen.BlockStyler.BlockDialog.DialogResponse dialogResponse = NXOpen.BlockStyler.BlockDialog.DialogResponse.Invalid;
            try { dialogResponse = theDialog.Launch(); }
            catch (Exception ex) { theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString()); }
            return dialogResponse;
        }

        public void Dispose()
        {
            if (theDialog != null) { theDialog.Dispose(); theDialog = null; }
        }

        public void initialize_cb()
        {
            try
            {
                tabMain = (NXOpen.BlockStyler.TabControl)theDialog.TopBlock.FindBlock("tabMain");
                fileBrowserGroup = (NXOpen.BlockStyler.Group)theDialog.TopBlock.FindBlock("fileBrowser");
                nativeFileBrowser0 = (NXOpen.BlockStyler.FileSelection)theDialog.TopBlock.FindBlock("nativeFileBrowser0");
                folderBrowserGroup = (NXOpen.BlockStyler.Group)theDialog.TopBlock.FindBlock("folderBrowser");
                nativeFolderBrowser0 = (NXOpen.BlockStyler.FolderSelection)theDialog.TopBlock.FindBlock("nativeFolderBrowser0");
                enum0 = (NXOpen.BlockStyler.Enumeration)theDialog.TopBlock.FindBlock("enum0");

                group1 = (NXOpen.BlockStyler.Group)theDialog.TopBlock.FindBlock("group1");
                chkNaming = (NXOpen.BlockStyler.Toggle)theDialog.TopBlock.FindBlock("chkNaming");
                chkAttributes = (NXOpen.BlockStyler.Toggle)theDialog.TopBlock.FindBlock("chkAttributes");
                chkLifecycle = (NXOpen.BlockStyler.Toggle)theDialog.TopBlock.FindBlock("chkLifecycle");
                chkDrawingFields = (NXOpen.BlockStyler.Toggle)theDialog.TopBlock.FindBlock("chkDrawingFields");
                chkBOM = (NXOpen.BlockStyler.Toggle)theDialog.TopBlock.FindBlock("chkBOM");
                chkExportReady = (NXOpen.BlockStyler.Toggle)theDialog.TopBlock.FindBlock("chkExportReady");

                btnValidate = (NXOpen.BlockStyler.Button)theDialog.TopBlock.FindBlock("btnValidate");
                separator0 = (NXOpen.BlockStyler.Separator)theDialog.TopBlock.FindBlock("separator0");
                group2 = (NXOpen.BlockStyler.Group)theDialog.TopBlock.FindBlock("group2");
                btnExport = (NXOpen.BlockStyler.Button)theDialog.TopBlock.FindBlock("btnExport");
                btnGenerateReport = (NXOpen.BlockStyler.Button)theDialog.TopBlock.FindBlock("btnGenerateReport");
            }
            catch (Exception ex)
            {
                theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
            }
        }

        // ====================================================================
        // --- THE UI MEMORY WIPE (Runs exactly when dialog opens)
        // ====================================================================
        public void dialogShown_cb()
        {
            try
            {
                // 1. Wipe the file browser path clean
                NXOpen.BlockStyler.PropertyList fileProps = nativeFileBrowser0.GetProperties();
                fileProps.SetString("Path", "");
                fileProps.Dispose();

                // 2. Wipe the folder browser path clean
                NXOpen.BlockStyler.PropertyList folderProps = nativeFolderBrowser0.GetProperties();
                folderProps.SetString("Path", "");
                folderProps.Dispose();

                // 3. Reset all checkboxes to FALSE (Unchecked). 
                // If you want them checked by default, change 'false' to 'true'.
                ResetToggle(chkNaming, false);
                ResetToggle(chkAttributes, false);
                ResetToggle(chkLifecycle, false);
                ResetToggle(chkDrawingFields, false);
                ResetToggle(chkBOM, false);
                ResetToggle(chkExportReady, false);

                // 4. Clear all internal memory lists so it acts as a fresh session
                activeFilePaths.Clear();
                lastValidationResults.Clear();
                approvedExportFiles.Clear();
                RequestExport = false;
                ExportFolder = "";
                ExportCandidates.Clear();
            }
            catch (Exception ex)
            {
                NXOpen.UI.GetUI().NXMessageBox.Show("Memory Wipe Error", NXOpen.NXMessageBox.DialogType.Error, ex.Message);
            }
        }

        // Helper method to keep checkbox resetting clean
        private void ResetToggle(NXOpen.BlockStyler.Toggle toggle, bool state)
        {
            NXOpen.BlockStyler.PropertyList props = toggle.GetProperties();
            props.SetLogical("Value", state);
            props.Dispose();
        }

        public int apply_cb() { return 0; }

        public int update_cb(NXOpen.BlockStyler.UIBlock block)
        {
            try
            {
                if (block == btnValidate) RunValidation();
                else if (block == btnExport) ExecuteSmartExport();
                else if (block == btnGenerateReport) GenerateHtmlReport();
            }
            catch (Exception ex) { NXOpen.UI.GetUI().NXMessageBox.Show("Update Error", NXOpen.NXMessageBox.DialogType.Error, ex.Message); }
            return 0;
        }

        public int ok_cb()
        {
            int errorCode = 0;
            try { errorCode = apply_cb(); }
            catch (Exception ex) { errorCode = 1; theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString()); }
            return errorCode;
        }

        public PropertyList GetBlockProperties(string blockID)
        {
            PropertyList plist = null;
            try { plist = theDialog.GetBlockProperties(blockID); }
            catch (Exception ex) { theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString()); }
            return plist;
        }

        private void RunValidation()
        {
            List<string> filesToProcess = new List<string>();

            try
            {
                NXOpen.BlockStyler.PropertyList tabProps = tabMain.GetProperties();
                int activeTab = tabProps.GetInteger("ActivePage");
                tabProps.Dispose();

                // --- NEW LOGIC: Read the Radio Button Group ---
                // 0 = Parts, 1 = Assemblies, 2 = Drawings, 3 = Full
                NXOpen.BlockStyler.PropertyList enumProps = enum0.GetProperties();
                int filterMode = enumProps.GetEnum("Value");
                enumProps.Dispose();

                if (activeTab == 0)
                {
                    // Single File Mode: We usually just process the file the user explicitly picked
                    NXOpen.BlockStyler.PropertyList fileProps = nativeFileBrowser0.GetProperties();
                    string singleFilePath = fileProps.GetString("Path");
                    fileProps.Dispose();

                    if (!string.IsNullOrEmpty(singleFilePath))
                    {
                        filesToProcess.Add(singleFilePath);
                    }
                }
                else if (activeTab == 1)
                {
                    // Batch Folder Mode: Scan and Filter
                    NXOpen.BlockStyler.PropertyList folderProps = nativeFolderBrowser0.GetProperties();
                    string folderPath = folderProps.GetString("Path");
                    folderProps.Dispose();

                    if (!string.IsNullOrEmpty(folderPath))
                    {
                        try
                        {
                            string[] scannedFiles = System.IO.Directory.GetFiles(folderPath, "*.prt", System.IO.SearchOption.TopDirectoryOnly);

                            foreach (string file in scannedFiles)
                            {
                                string upperName = System.IO.Path.GetFileName(file).ToUpper();

                                // Filter Logic based on strict enterprise naming rules
                                if (filterMode == 0 && !upperName.Contains("_MOD")) continue; // Skip if it's not a Part
                                if (filterMode == 1 && !upperName.Contains("_ASM")) continue; // Skip if it's not an Assembly
                                if (filterMode == 2 && !upperName.Contains("_DRW")) continue; // Skip if it's not a Drawing

                                // If filterMode == 3 (Full validation), it skips the checks above and adds everything

                                filesToProcess.Add(file);
                            }
                        }
                        catch (Exception ex)
                        {
                            NXOpen.UI.GetUI().NXMessageBox.Show("Read Error", NXOpen.NXMessageBox.DialogType.Error, "Could not read folder: " + ex.Message);
                            return;
                        }
                    }
                }

                if (filesToProcess.Count == 0)
                {
                    NXOpen.UI.GetUI().NXMessageBox.Show("Warning", NXOpen.NXMessageBox.DialogType.Warning, "No files found matching the selected filter criteria.");
                    return;
                }

                NXOpen.BlockStyler.PropertyList propNaming = chkNaming.GetProperties();
                bool doNaming = propNaming.GetLogical("Value");
                propNaming.Dispose();

                NXOpen.BlockStyler.PropertyList propAttr = chkAttributes.GetProperties();
                bool doAttr = propAttr.GetLogical("Value");
                propAttr.Dispose();

                NXOpen.BlockStyler.PropertyList propLife = chkLifecycle.GetProperties();
                bool doLifecycle = propLife.GetLogical("Value");
                propLife.Dispose();

                NXOpen.BlockStyler.PropertyList propDraw = chkDrawingFields.GetProperties();
                bool doDraw = propDraw.GetLogical("Value");
                propDraw.Dispose();

                NXOpen.BlockStyler.PropertyList propBom = chkBOM.GetProperties();
                bool doBom = propBom.GetLogical("Value");
                propBom.Dispose();

                activeFilePaths = filesToProcess;

                CADValidator.Core.ValidatorEngine engine = new CADValidator.Core.ValidatorEngine(
                    doNaming, doAttr, doLifecycle, doDraw, doBom
                );

                lastValidationResults = engine.ProcessFiles(filesToProcess);
                ProcessAndShowResults(lastValidationResults);
            }
            catch (Exception ex)
            {
                NXOpen.UI.GetUI().NXMessageBox.Show("Error", NXOpen.NXMessageBox.DialogType.Error, ex.Message);
            }
        }

        private void ProcessAndShowResults(List<CADValidator.Models.ValidationResult> results)
        {
            approvedExportFiles.Clear();
            HashSet<string> disqualifiedParts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> partTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<string>> failureReasons = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var res in results)
            {
                string cleanName = System.IO.Path.GetFileNameWithoutExtension(res.FileName);
                partTypes[cleanName] = res.FileType;

                if (res.Status == CADValidator.Models.ValidationStatus.Fail)
                {
                    disqualifiedParts.Add(cleanName);
                    if (!failureReasons.ContainsKey(cleanName)) failureReasons[cleanName] = new List<string>();
                    failureReasons[cleanName].Add(res.Message);
                }
            }

            int stepReadyCount = 0;
            int pdfReadyCount = 0;

            foreach (string path in activeFilePaths)
            {
                string cleanName = System.IO.Path.GetFileNameWithoutExtension(path);

                if (!disqualifiedParts.Contains(cleanName) && partTypes.ContainsKey(cleanName))
                {
                    string fileType = partTypes[cleanName];
                    CADValidator.Core.ExportCandidate candidate = new CADValidator.Core.ExportCandidate();
                    candidate.FullPath = path;
                    candidate.FileType = fileType;
                    candidate.CleanName = cleanName;

                    approvedExportFiles.Add(candidate);

                    if (fileType == "MOD" || fileType == "ASM") stepReadyCount++;
                    if (fileType == "DRW" || fileType == "ASM") pdfReadyCount++;
                }
            }

            System.Text.StringBuilder popupMsg = new System.Text.StringBuilder();
            popupMsg.AppendLine($"--- VALIDATION COMPLETE ---\n");
            popupMsg.AppendLine($"> {stepReadyCount} files passed and are ready to export as STEP.");
            popupMsg.AppendLine($"> {pdfReadyCount} files passed and are ready to export as PDF.\n");

            if (disqualifiedParts.Count > 0)
            {
                popupMsg.AppendLine("============================");
                popupMsg.AppendLine("       FAILED FILES         ");
                popupMsg.AppendLine("============================");
                foreach (var failedFile in failureReasons)
                {
                    popupMsg.AppendLine($"\nFile: {failedFile.Key}");
                    foreach (string reason in failedFile.Value) popupMsg.AppendLine($"  - Fail: {reason}");
                }
            }
            NXOpen.UI.GetUI().NXMessageBox.Show("Validation Summary", NXOpen.NXMessageBox.DialogType.Information, popupMsg.ToString());
        }

        private void ExecuteSmartExport()
        {
            try
            {
                if (approvedExportFiles.Count == 0)
                {
                    NXOpen.UI.GetUI().NXMessageBox.Show("Export", NXOpen.NXMessageBox.DialogType.Information, "No files passed validation. Please run validation first.");
                    return;
                }

                using (System.Windows.Forms.FolderBrowserDialog folderDialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    folderDialog.Description = "Select Destination Folder for Smart Export";
                    using (System.Windows.Forms.Form dummyForm = new System.Windows.Forms.Form() { TopMost = true })
                    {
                        if (folderDialog.ShowDialog(dummyForm) == System.Windows.Forms.DialogResult.OK)
                        {
                            ExportFolder = folderDialog.SelectedPath;
                            ExportCandidates = new List<CADValidator.Core.ExportCandidate>(approvedExportFiles);
                            RequestExport = true;

                            NXOpen.UI.GetUI().NXMessageBox.Show("Export Queued", NXOpen.NXMessageBox.DialogType.Information,
                                "Export sequence queued successfully!\n\nPlease click the green 'OK' button at the bottom of the main dialog to safely close the tool and begin processing the files.");
                        }
                    }
                }
            }
            catch (Exception ex) { NXOpen.UI.GetUI().NXMessageBox.Show("Error", NXOpen.NXMessageBox.DialogType.Error, ex.Message); }
        }

        private void GenerateHtmlReport()
        {
            try
            {
                if (lastValidationResults == null || lastValidationResults.Count == 0)
                {
                    NXOpen.UI.GetUI().NXMessageBox.Show("Report", NXOpen.NXMessageBox.DialogType.Information, "No results to export. Please run validation first.");
                    return;
                }

                using (System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog())
                {
                    saveFileDialog.Filter = "HTML Document (*.html)|*.html|All files (*.*)|*.*";
                    saveFileDialog.Title = "Save Validation Report";
                    saveFileDialog.FileName = "CAD_Validation_Report_" + System.DateTime.Now.ToString("yyyyMMdd_HHmm") + ".html";

                    if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        System.Text.StringBuilder html = new System.Text.StringBuilder();

                        html.AppendLine("<!DOCTYPE html>");
                        html.AppendLine("<html lang='en'>");
                        html.AppendLine("<head>");
                        html.AppendLine("<meta charset='UTF-8'>");
                        html.AppendLine("<title>CAD Validation Report</title>");
                        html.AppendLine("<style>");
                        html.AppendLine("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f7f6; color: #333; margin: 40px; }");
                        html.AppendLine("h1 { color: #005a9e; border-bottom: 2px solid #005a9e; padding-bottom: 10px; }");
                        html.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; background-color: #fff; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }");
                        html.AppendLine("th, td { padding: 12px 15px; text-align: left; border-bottom: 1px solid #ddd; }");
                        html.AppendLine("th { background-color: #005a9e; color: #ffffff; font-weight: bold; text-transform: uppercase; font-size: 14px; }");
                        html.AppendLine(".row-pass { border-left: 5px solid #28a745; }");
                        html.AppendLine(".row-fail { border-left: 5px solid #dc3545; background-color: #fdf3f4; }");
                        html.AppendLine(".row-warn { border-left: 5px solid #ffc107; background-color: #fffbf0; }");
                        html.AppendLine(".row-info { border-left: 5px solid #17a2b8; }");
                        html.AppendLine(".status-badge { padding: 4px 8px; border-radius: 4px; font-weight: bold; color: white; display: inline-block; text-align: center; width: 60px; }");
                        html.AppendLine(".badge-pass { background-color: #28a745; }");
                        html.AppendLine(".badge-fail { background-color: #dc3545; }");
                        html.AppendLine(".badge-warn { background-color: #ffc107; color: #333; }");
                        html.AppendLine(".badge-info { background-color: #17a2b8; }");
                        html.AppendLine("</style>");
                        html.AppendLine("</head>");
                        html.AppendLine("<body>");

                        html.AppendLine("<h1>CAD Validation Report</h1>");
                        html.AppendLine($"<p><strong>Generated On:</strong> {System.DateTime.Now.ToString("MMMM dd, yyyy HH:mm")}</p>");

                        html.AppendLine("<table>");
                        html.AppendLine("<thead>");
                        html.AppendLine("<tr><th>File Name</th><th>Type</th><th>Rule</th><th>Status</th><th>Message</th></tr>");
                        html.AppendLine("</thead>");
                        html.AppendLine("<tbody>");

                        string previousFileName = "";

                        foreach (CADValidator.Models.ValidationResult res in lastValidationResults)
                        {
                            if (previousFileName != "" && previousFileName != res.FileName)
                            {
                                html.AppendLine("<tr style='background-color: #ebf0f2;'><td colspan='5' style='padding: 2px;'></td></tr>");
                            }

                            string rowClass = "row-info";
                            string badgeClass = "badge-info";
                            string statusText = res.Status.ToString().ToUpper();

                            if (res.Status == CADValidator.Models.ValidationStatus.Pass) { rowClass = "row-pass"; badgeClass = "badge-pass"; }
                            else if (res.Status == CADValidator.Models.ValidationStatus.Fail) { rowClass = "row-fail"; badgeClass = "badge-fail"; }
                            else if (res.Status == CADValidator.Models.ValidationStatus.Warning) { rowClass = "row-warn"; badgeClass = "badge-warn"; }

                            html.AppendLine($"<tr class='{rowClass}'>");
                            html.AppendLine($"<td><strong>{System.Net.WebUtility.HtmlEncode(res.FileName)}</strong></td>");
                            html.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(res.FileType)}</td>");
                            html.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(res.RuleName)}</td>");
                            html.AppendLine($"<td><span class='status-badge {badgeClass}'>{statusText}</span></td>");
                            html.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(res.Message)}</td>");
                            html.AppendLine("</tr>");

                            previousFileName = res.FileName;
                        }

                        html.AppendLine("</tbody>");
                        html.AppendLine("</table>");
                        html.AppendLine("</body>");
                        html.AppendLine("</html>");

                        System.IO.File.WriteAllText(saveFileDialog.FileName, html.ToString());
                        System.Diagnostics.Process.Start(saveFileDialog.FileName);
                    }
                }
            }
            catch (Exception ex) { NXOpen.UI.GetUI().NXMessageBox.Show("Report Error", NXOpen.NXMessageBox.DialogType.Error, "Failed to generate HTML report: " + ex.Message); }
        }
    }
}