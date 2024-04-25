﻿using ClosedXML.Excel;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using TiaXmlReader.Generation.IO;
using TiaXmlReader.Utility;
using TiaXmlReader.Generation.Alarms;
using TiaXmlReader.Generation.Alarms.GenerationForm;
using TiaXmlReader.Generation.IO.GenerationForm;
using System.Collections.Generic;
using System.Linq;
using TiaXmlReader.AutoSave;
using TiaXmlReader.SimaticML;
using System.Xml;
using TiaXmlReader.SimaticML.Enums;

namespace TiaXmlReader
{
    public partial class MainImportExportForm : Form
    {
        private readonly ProgramSettings programSettings;
        private readonly TimedSaveHandler autoSaveHandler;

        public MainImportExportForm()
        {
            InitializeComponent();

            this.programSettings = ProgramSettings.Load();
            this.programSettings.Save(); //To create file if not exist!

            this.autoSaveHandler = new TimedSaveHandler(programSettings, this.autoSaveComboBox.ComboBox);

            Init();
        }

        private void Init()
        {

            configExcelPathTextBox.Text = programSettings.lastExcelFileName;
            exportPathTextBlock.Text = programSettings.lastXMLExportPath;
            tiaVersionComboBox.Text = "" + programSettings.lastTIAVersion;

            this.languageComboBox.Items.AddRange(new string[]{ "it-IT", "en-US"});
            this.languageComboBox.TextChanged += (object sender, EventArgs args) =>
            {
                try
                {
                    var culture = CultureInfo.GetCultureInfo(this.languageComboBox.Text);
                    SystemVariables.LANG = programSettings.ietfLanguage = culture.IetfLanguageTag;
                }
                catch (CultureNotFoundException)
                {
                    this.languageComboBox.SelectedItem = this.languageComboBox.Items[0];
                }
            };
            this.languageComboBox.Text = programSettings.ietfLanguage; //Call this after so the text changed event changes the system lang.

            #region SETTINGS_SAVE_TICK + AUTO_SAVE
            this.autoSaveHandler.Start();

            var settingsWrapper = new AutoSaveSettingsWrapper(this.programSettings);
            settingsWrapper.Scan();

            var timer = new Timer { Interval = 1000 };
            timer.Start();
            timer.Tick += (sender, e) =>
            {
                if(!settingsWrapper.CompareSnapshot())
                {
                    this.programSettings.Save();
                }
            };
            #endregion
        }

        private void TiaVersionComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (uint.TryParse(tiaVersionComboBox.Text, out var version))
            {
                Constants.VERSION = programSettings.lastTIAVersion = version;
            }
        }

        private void ConfigExcelPathTextBox_MouseClick(object sender, MouseEventArgs e)
        {
            try
            {
                var fileDialog = new CommonOpenFileDialog
                {
                    EnsureFileExists = true,
                    EnsurePathExists = true,
                    DefaultExtension = ".xlsx",
                    Filters = { new CommonFileDialogFilter("Excel Files (*.xlsx)", "*.xlsx") }
                };
                fileDialog.InitialDirectory = string.IsNullOrEmpty(programSettings.lastExcelFileName) ? "" : Path.GetDirectoryName(programSettings.lastExcelFileName);

                if (fileDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    configExcelPathTextBox.Text = programSettings.lastExcelFileName = fileDialog.FileName;
                }
            }
            catch  {  }

        }

        private void ExportPathTextBlock_MouseClick(object sender, MouseEventArgs e)
        {
            try
            {
                var fileDialog = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    EnsurePathExists = true
                };
                fileDialog.InitialDirectory = programSettings.lastXMLExportPath;
                if (fileDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    exportPathTextBlock.Text = programSettings.lastXMLExportPath = fileDialog.FileName;
                }
            }
            catch { }

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

        private void DbDuplicationMenuItem_Click(object sender, EventArgs e)
        {
            var dbDuplicationForm = new DBDuplicationForm(programSettings);
            dbDuplicationForm.ShowInTaskbar = false;
            dbDuplicationForm.ShowDialog();
        }

        private void GenerateIOMenuItem_Click(object sender, EventArgs e)
        {
            new IOGenerationForm(this.autoSaveHandler, this.programSettings.IOSettings, this.programSettings.GridSettings).Show(this);
        }

        private void GenerateAlarmsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new AlarmGenerationForm(this.autoSaveHandler, this.programSettings.AlarmSettings, this.programSettings.GridSettings).Show(this);
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

                var xmlDocument = new XmlDocument();
                xmlDocument.Load(filePath);
                var xmlNodeConfiguration = SimaticMLParser.ParseXML(xmlDocument);


                xmlNodeConfiguration.UpdateID_UId(new IDGenerator());
                var document = SimaticMLParser.CreateDocument();
                var generatedXML = xmlNodeConfiguration.Generate(document);

                fileDialog = new CommonOpenFileDialog
                {
                    IsFolderPicker = false,
                    EnsurePathExists = true,
                    EnsureFileExists = false,
                    DefaultExtension = ".xml",
                    Filters = { new CommonFileDialogFilter("XML Files (*.xml)", "*.xml") }
                };

                if(fileDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    var __ = SimaticDataType.BOOLEAN;

                    filePath = fileDialog.FileName;
                    document.DocumentElement.AppendChild(generatedXML);
                    document.Save(filePath);
                }
                var _debug = "" + "";
            }
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
*/