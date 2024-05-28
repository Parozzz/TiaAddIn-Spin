﻿using Microsoft.WindowsAPICodePack.Dialogs;
using System.Globalization;
using TiaXmlReader.Utility;
using TiaXmlReader.Generation.Alarms.GenerationForm;
using TiaXmlReader.Generation.IO.GenerationForm;
using TiaXmlReader.AutoSave;
using System.Xml;
using TiaXmlReader.Generation.Configuration;
using Jint;
using TiaXmlReader.Javascript;
using Timer = System.Windows.Forms.Timer;
using TiaUtilities.Generation.Configuration.Utility;
using InfoBox;
using TiaXmlReader.Languages;
using SimaticML;
using SimaticML.Blocks;
using SimaticML.Enums;
using SimaticML.Blocks.FlagNet;
using SimaticML.Blocks.FlagNet.nPart;
using SimaticML.API;
using TiaXmlReader.Generation;

namespace TiaXmlReader
{
    public partial class MainImportExportForm : Form
    {
        private readonly ProgramSettings programSettings;
        private readonly TimedSaveHandler autoSaveHandler;
        private readonly JavascriptErrorReportThread jsErrorHandlingThread;

        public MainImportExportForm()
        {
            InitializeComponent();

            this.programSettings = ProgramSettings.Load();
            this.programSettings.Save(); //To create file if not exist!

            this.autoSaveHandler = new TimedSaveHandler(programSettings);
            this.jsErrorHandlingThread = new JavascriptErrorReportThread();

            Init();
        }

        private void Init()
        {
            LogHandler.INSTANCE.Init();
            LogHandler.INSTANCE.Start();

            jsErrorHandlingThread.Init();
            jsErrorHandlingThread.Start();

            tiaVersionComboBox.Text = "" + programSettings.lastTIAVersion;

            var ignoreLanguageBoxAtStartup = true;

            this.languageComboBox.Items.AddRange(["it-IT", "en-US"]);
            this.languageComboBox.TextChanged += (sender, args) =>
            {
                try
                {
                    var culture = CultureInfo.GetCultureInfo(this.languageComboBox.Text);
                    LocalizationVariables.LANG = programSettings.ietfLanguage = culture.IetfLanguageTag;

                    if (ignoreLanguageBoxAtStartup)
                    {
                        return;
                    }

                    //This should stay always in english. In case someone set an unkown language, this will be neautral.
                    var result = InformationBox.Show("Do you want to restart application?", "Restart to change language", buttons: InformationBoxButtons.YesNo);
                    if (result == InformationBoxResult.Yes)
                    {
                        this.programSettings.Save(); //Without save, after restart it would restore the old (Meaning it would be useless)

                        Application.Restart();
                        Environment.Exit(0);
                    }
                }
                catch (CultureNotFoundException ex)
                {
                    this.languageComboBox.SelectedItem = this.languageComboBox.Items[0];
                    Utils.ShowExceptionMessage(ex);
                }
            };
            this.languageComboBox.Text = programSettings.ietfLanguage; //Call this after so the text changed event changes the system lang.

            ignoreLanguageBoxAtStartup = false;

            #region SETTINGS_SAVE_TICK + AUTO_SAVE
            this.autoSaveTimeTextBox.TextChanged += (sender, args) =>
            {
                if (int.TryParse(autoSaveTimeTextBox.Text, out int time))
                {
                    this.autoSaveHandler.SetIntervalAndStart(time * 1000);
                }
            };
            this.autoSaveTimeTextBox.Text = "" + programSettings.TimedSaveTime; //Call this after so it start auto save

            var settingsWrapper = new AutoSaveSettingsWrapper(this.programSettings);
            settingsWrapper.Scan();

            var timer = new Timer { Interval = 1000 };
            timer.Start();
            timer.Tick += (sender, e) =>
            {
                if (!settingsWrapper.CompareSnapshot())
                {
                    this.programSettings.Save();
                }
            };
            #endregion

            Translate();
        }

        private void Translate()
        {
            this.Text = Localization.Get("MAIN_FORM");

            this.fileToolStripMenuItem.Text = Localization.Get("GENERICS_FILE");
            this.autoSaveMenuItem.Text = Localization.Get("MAIN_FORM_TOP_FILE_AUTO_SAVE");

            this.dbDuplicationMenuItem.Text = Localization.Get("MAIN_FORM_TOP_DB_DUPLICATION");
            this.generateIOMenuItem.Text = Localization.Get("MAIN_FORM_TOP_IO_GENERATION");
            this.generateAlarmsMenuItem.Text = Localization.Get("MAIN_FORM_TOP_ALARM_GENERATOR");

            this.tiaVersionLabel.Text = Localization.Get("MAIN_FORM_TIA_VERSION");
            this.languageLabel.Text = Localization.Get("MAIN_FORM_LANGUAGE");
        }

        private void TiaVersionComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (uint.TryParse(tiaVersionComboBox.Text, out var version))
            {
                SimaticMLAPI.TIA_VERSION = programSettings.lastTIAVersion = version;
            }
        }

        private void DbDuplicationMenuItem_Click(object sender, EventArgs e)
        {
            var dbDuplicationForm = new DBDuplicationForm(programSettings)
            {
                ShowInTaskbar = false
            };
            dbDuplicationForm.ShowDialog();
        }

        private void GenerateIOMenuItem_Click(object sender, EventArgs e)
        {
            new IOGenerationForm(this.jsErrorHandlingThread, this.autoSaveHandler, this.programSettings.IOSettings, this.programSettings.GridSettings).Show(this);
        }

        private void GenerateAlarmsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new AlarmGenerationForm(this.jsErrorHandlingThread, this.autoSaveHandler, this.programSettings.AlarmSettings, this.programSettings.GridSettings).Show(this);
        }

        private void ImportXMLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var fileDialog = new CommonOpenFileDialog
            {
                IsFolderPicker = false,
                EnsurePathExists = true,
                EnsureFileExists = true,
                DefaultExtension = ".xml",
                Filters = { new CommonFileDialogFilter("XML Files (*.xml)", "*.xml") }
            };

            if (fileDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                var filePath = fileDialog.FileName;
                if (filePath == null)
                {
                    return;
                }

                var xmlDocument = new XmlDocument();
                xmlDocument.Load(filePath);

                var xmlNodeConfiguration = SimaticMLAPI.ParseXML(xmlDocument);
                if (xmlNodeConfiguration == null)
                {
                    return;
                }

                xmlNodeConfiguration.UpdateID_UId(new IDGenerator());

                fileDialog = new CommonOpenFileDialog
                {
                    IsFolderPicker = false,
                    EnsurePathExists = true,
                    EnsureFileExists = false,
                    DefaultExtension = ".xml",
                    Filters = { new CommonFileDialogFilter("XML Files (*.xml)", "*.xml") }
                };

                if (fileDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    filePath = fileDialog.FileName;
                    if (filePath == null)
                    {
                        return;
                    }

                    var document = SimaticMLAPI.CreateDocument(xmlNodeConfiguration);
                    document.Save(filePath);
                }
                var _debug = "" + "";
            }
        }

        private string? JS;
        private void JSToolStripMenuItem_Click(object sender, EventArgs args)
        {
            var configForm = new ConfigForm("TEST JS")
            {
                ControlWidth = 500
            };

            var mainGroup = configForm.Init();
            mainGroup.AddJavascript().Label("Espressione").Height(300)
                  .ControlText(JS)
                  .TextChanged(str => JS = str);

            configForm.FormClosed += (s, e) =>
            {
                try
                {
                    if (JS == null)
                    {
                        return;
                    }

                    using (var engine = new Engine())
                    {
                        engine.SetValue("nome", "cacca");

                        var eval = engine.Evaluate(JS);

                        var nome = engine.GetValue("nome");
                        var _ = "";
                    }
                }
                catch (Exception ex)
                {
                    Utils.ShowExceptionMessage(ex);
                }
            };

            configForm.StartShowingAtCursor();
            configForm.Init();
            configForm.Show(this);

        }

        private void SampleXMLMenuItem_Click(object sender, EventArgs e)
        {
            var fc = new BlockFC();

            //Add basic block information, like name, number and programming language.
            fc.AttributeList.BlockName = "FC_TEST";
            fc.AttributeList.BlockNumber = 123;
            fc.AttributeList.ProgrammingLanguage = SimaticProgrammingLanguage.LADDER;

            //Add two temp variables to the specific section.

            for(int i = 0; i < 10; i++)
            {
                fc.AttributeList.TEMP.AddMember($"tContact{i}", SimaticDataType.BOOLEAN);
            }
            fc.AttributeList.TEMP.AddMember("tCoil1", SimaticDataType.BOOLEAN);

            var segment = new SimaticLADSegment();
            segment.Title[LocalizationVariables.CULTURE] = "Segment Title!";
            segment.Comment[LocalizationVariables.CULTURE] = "Segment Comment! Much information here ...";

            ContactPart[] contacts = new ContactPart[10];
            for (int i = 0; i < 10; i++)
            {
                contacts[i] = new ContactPart() { Operand = new SimaticLocalVariable($"tContact{i}") };
            }
            var coil = new CoilPart() { Operand = new SimaticLocalVariable("tCoil1") };

            var _ = segment.Powerrail & contacts[0] & (((contacts[1] | contacts[2]) & (contacts[3] | contacts[4])) | contacts[5]) & coil;

            segment.Create(fc);
            /*
            var compileUnit = fc.AddCompileUnit();

            //Create the parts that will form the compileUnit (Segment). Also add the operand of the part to the local variable created before.
            //A Part is everything that does something inside a segment (Contact, Coil, Block) that is not an FC/FB.
            var contact = new ContactPart(compileUnit) { Operand = new SimaticLocalVariable("tVar1") };
            var coil = new CoilPart(compileUnit) { Operand = new SimaticLocalVariable("tVar2") };

            //Create the connections between the parts
            // |
            // | --- || --- () 
            // |
            var _ = compileUnit.Powerrail & contact & coil; //Create a AND connection between all the parts.
            */

            //Create skeleton for the XML Document and add the FC to it.
            var xmlDocument = SimaticMLAPI.CreateDocument(fc);

            var fileDialog = GenerationUtils.CreateFileDialog(false, null, "xml");
            if (fileDialog.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrEmpty(fileDialog.FileName))
            {
                xmlDocument.Save(fileDialog.FileName);
            }

            //Save the file.
            //xmlDocument.Save(Directory.GetCurrentDirectory() + "/fc.xml");
        }
    }
}

/*
 


        private void generateButton_Click(object sender, EventArgs e)
        {
            GlobalIDGenerator.ResetID();

            var fc = new BlockFC();
            fc.Init();

            //BLOCK ATTRIBUTES
            var inputSection = fc.GetBlockAttributes().ComputeSection(SectionTypeEnum.INPUT);

            var variableInput = inputSection.AddMember("VariableInput", "Int");
            //BLOCK ATTRIBUTES

            //COMPILE UNITS
            var compileUnit = fc.AddCompileUnit();
            compileUnit.Init();

            var contactPart = compileUnit.AddPart(Part.Type.CONTACT).SetNegated();
            var coilPart = compileUnit.AddPart(Part.Type.COIL);

            compileUnit.AddPowerrail(new Dictionary<Part, string> {
                    { contactPart, "in" }
            });
            compileUnit.AddIdentWire(Access.Type.GLOBAL_VARIABLE, "IO.IN_01", contactPart, "operand");
            compileUnit.AddIdentWire(Access.Type.GLOBAL_VARIABLE, "IO.IN_02", coilPart, "operand");
            compileUnit.AddBoolANDWire(contactPart, "out", coilPart, "in");

            //COMPILE UNITS

            var xmlDocument = SiemensMLParser.CreateDocument();
            xmlDocument.DocumentElement.AppendChild(fc.Generate(xmlDocument));
            if (!string.IsNullOrEmpty(xmlPathTextBlock.Text))
            {
                xmlDocument.Save(xmlPathTextBlock.Text);
            }
        }

        private void generateTagTableButton_Click(object sender, EventArgs e)
        {
            var tagTable = SiemensMLParser.CreateEmptyTagTable();

            var tag = tagTable.AddTag();
            tag.SetLogicalAddress("%M40.0");
            tag.SetTagName("TagName?!");
            tag.SetDataTypeName("bool");

            var xmlDocument = SiemensMLParser.CreateDocument();
            xmlDocument.DocumentElement.AppendChild(tagTable.Generate(xmlDocument));
            xmlDocument.Save(excelPathTextBox.Text);
        }
        

        private void GenerateXMLExportFiles_MouseClick(object sender, MouseEventArgs e)
        {
            try
            {
                //Allow opening file while having excel open. TIMESAVER!
                using (var stream = new FileStream(configExcelPathTextBox.Text,
                                 FileMode.Open,
                                 FileAccess.Read,
                                 FileShare.ReadWrite))
                {

                    using (var configWorkbook = new XLWorkbook(stream))
                    {
                        var configWorksheet = configWorkbook.Worksheets.Worksheet(1);

                        var configTypeValue = configWorksheet.Cell("A2").Value;
                        if (!configTypeValue.IsText || string.IsNullOrEmpty(configTypeValue.GetText()))
                        {
                            throw new ApplicationException("Configuration excel file invalid");
                        }

                        switch (configTypeValue.GetText().ToLower())
                        {
                            case "type1":
                                var alarmExcelImporter = new AlarmExcelImporter();
                                alarmExcelImporter.ImportExcelConfig(configWorksheet);

                                var alarmXmlGenerator = new AlarmXmlGenerator(alarmExcelImporter.GetConfiguration(), alarmExcelImporter.GetAlarmDataList(), alarmExcelImporter.GetDeviceDataList());
                                alarmXmlGenerator.GenerateBlocks();
                                alarmXmlGenerator.ExportXML(exportPathTextBlock.Text);
                                break;
                            case "type3":
                                var ioExcelImporter = new IOExcelImporter();
                                ioExcelImporter.ImportExcelConfig(configWorksheet);

                                var ioXmlGenerator = new IOXmlGenerator(ioExcelImporter.GetConfiguration(), ioExcelImporter.GetDataList());
                                ioXmlGenerator.GenerateBlocks();
                                ioXmlGenerator.ExportXML(exportPathTextBlock.Text);
                                break;
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Utils.ShowExceptionMessage(ex);
            }
        }
        
*/