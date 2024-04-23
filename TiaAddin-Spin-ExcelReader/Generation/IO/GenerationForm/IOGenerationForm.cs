﻿
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TiaXmlReader.Generation;
using TiaXmlReader.Generation.IO;
using TiaXmlReader.Localization;
using TiaXmlReader.SimaticML;
using TiaXmlReader.Utility;
using System.Data.Common;
using TiaXmlReader.Generation.Configuration;
using TiaXmlReader.Generation.GridHandler;
using TiaXmlReader.Generation.IO.GenerationForm;
using TiaXmlReader.Generation.IO.GenerationForm.ExcelImporter;

namespace TiaXmlReader.Generation.IO.GenerationForm
{
    public partial class IOGenerationForm : Form
    {
        private AutoSaveHandler autoSaveHandler;
        private readonly IOGenerationSettings settings;
        private readonly GridHandler<IOConfiguration, IOData> gridHandler;
        private readonly IOGenerationFormConfigHandler configHandler;

        private IOGenerationExcelImportConfiguration ExcelImportConfig { get => settings.ExcelImportConfiguration; }
        private IOConfiguration IOConfig { get => settings.IOConfiguration; }

        private string lastFilePath;

        public IOGenerationForm(AutoSaveHandler autoSaveHandler)
        {
            InitializeComponent();

            this.autoSaveHandler = autoSaveHandler;
            this.settings = IOGenerationSettings.Load();
            this.settings.Save(); //This could be avoided but is to be sure that all the classes that are created new will be saved to file!

            this.gridHandler = new GridHandler<IOConfiguration, IOData>(this.dataGridView, settings.GridSettings, IOConfig, IOData.COLUMN_LIST, new IOGenerationComparer())
            {
                RowCount = 2999
            };

            this.configHandler = new IOGenerationFormConfigHandler(this, IOConfig, this.gridHandler);


            Init();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.S | Keys.Control:
                    this.ProjectSave();
                    return true;
                case Keys.L | Keys.Control:
                    this.ProjectLoad();
                    return true;
            }

            // Call the base class
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public void Init()
        {
            //var addressColumn = this.dataTable.Rows.Add(TOTAL_ROW_COUNT);

            #region TopMenu
            this.saveToolStripMenuItem.Click += (object sender, EventArgs args) => { this.ProjectSave(); };
            this.saveAsToolStripMenuItem.Click += (object sender, EventArgs args) => { this.ProjectSave(true); };
            this.loadToolStripMenuItem.Click += (object sender, EventArgs args) => { this.ProjectLoad(); };
            this.exportXMLToolStripMenuItem.Click += (object sender, EventArgs args) =>
            {
                try
                {
                    var fileDialog = new CommonOpenFileDialog
                    {
                        IsFolderPicker = true,
                        EnsurePathExists = true,
                        EnsureValidNames = true,
                        InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                    };

                    if (fileDialog.ShowDialog() == CommonFileDialogResult.Ok)
                    {
                        var ioDataList = new List<IOData>(this.gridHandler.DataSource.GetNotEmptyDataDict().Keys);

                        var ioXmlGenerator = new IOXmlGenerator(this.IOConfig, ioDataList);
                        ioXmlGenerator.GenerateBlocks();
                        ioXmlGenerator.ExportXML(fileDialog.FileName);
                    }
                }
                catch (Exception ex)
                {
                    Utils.ShowExceptionMessage(ex);
                }
            };
            this.importExcelToolStripMenuItem.Click += (object sender, EventArgs args) =>
            {
                var excelImportForm = new IOGenerationExcelImportForm(this.ExcelImportConfig, this.settings.GridSettings);

                var dialogResult = excelImportForm.ShowDialog();
                if (dialogResult == DialogResult.OK)
                {
                    var ioDataList = new List<IOData>();
                    foreach (var importData in excelImportForm.ImportDataEnumerable)
                    {
                        ioDataList.Add(new IOData()
                        {
                            Address = importData.Address,
                            IOName = importData.IOName,
                            Comment = importData.Comment
                        });
                    }

                    var ioDataCounter = 0;
                    foreach (var emptyRowIndex in this.gridHandler.DataSource.GetFirstEmptyRowIndexes(ioDataList.Count))
                    {
                        if (ioDataCounter >= ioDataList.Count)
                        {
                            break;
                        }

                        var ioData = ioDataList[ioDataCounter++];
                        this.gridHandler.ChangeRow(emptyRowIndex, ioData);
                    }
                }
            };
            this.preferencesToolStripMenuItem.Click += (object sender, EventArgs args) =>
            {
                var configForm = new ConfigForm("Preferenze");
                configForm.AddColorPickerLine("Bordo cella selezionata")
                    .ApplyColor(settings.GridSettings.SingleSelectedCellBorderColor)
                    .ColorChanged(color => settings.GridSettings.SingleSelectedCellBorderColor = color);

                configForm.AddColorPickerLine("Sfondo cella trascinata")
                    .ApplyColor(settings.GridSettings.DragSelectedCellBorderColor)
                    .ColorChanged(color => settings.GridSettings.DragSelectedCellBorderColor = color);

                configForm.AddColorPickerLine("Triangolo trascinamento")
                    .ApplyColor(settings.GridSettings.SelectedCellTriangleColor)
                    .ColorChanged(color => settings.GridSettings.SelectedCellTriangleColor = color);

                configForm.AddColorPickerLine("Anteprima")
                    .ApplyColor(settings.GridSettings.PreviewColor)
                    .ColorChanged(color => settings.GridSettings.PreviewColor = color);

                configForm.StartShowingAtLocation(Cursor.Position);
                configForm.Init();
                configForm.Show(this);

                dataGridView.Refresh();
            };
            #endregion

            #region MemoryType ComboBox
            this.memoryTypeComboBox.DisplayMember = "Text";
            this.memoryTypeComboBox.ValueMember = "Value";

            var memoryTypeItems = new List<object>();
            foreach (IOMemoryTypeEnum memoryType in Enum.GetValues(typeof(IOMemoryTypeEnum)))
            {
                memoryTypeItems.Add(new { Text = memoryType.GetEnumDescription(), Value = memoryType });
            }
            this.memoryTypeComboBox.DataSource = memoryTypeItems;

            this.memoryTypeComboBox.SelectionChangeCommitted += (object sender, EventArgs args) => this.UpdateConfigPanel();
            #endregion

            #region GroupingType ComboBox
            this.groupingTypeComboBox.DisplayMember = "Text";
            this.groupingTypeComboBox.ValueMember = "Value";

            var gropingTypeItems = new List<object>();
            foreach (IOGroupingTypeEnum groupingType in Enum.GetValues(typeof(IOGroupingTypeEnum)))
            {
                gropingTypeItems.Add(new { Text = groupingType.GetEnumDescription(), Value = groupingType });
            }
            this.groupingTypeComboBox.DataSource = gropingTypeItems;
            #endregion

            #region CELL_PAINTERS
            
            #endregion

            #region DRAG
            this.gridHandler.SetDragPreviewAction(data => { IOGenerationUtils.DragPreview(data, this.gridHandler); });
            this.gridHandler.SetDragMouseUpAction(data => { IOGenerationUtils.DragMouseUp(data, this.gridHandler); });
            #endregion
            //Column initialization before gridHandler.Init()
            #region COLUMNS
            var addressColumn = this.gridHandler.AddTextBoxColumn(IOData.ADDRESS, 65);
            addressColumn.MaxInputLength = 10;

            this.gridHandler.AddTextBoxColumn(IOData.IO_NAME, 110);
            this.gridHandler.AddTextBoxColumn(IOData.DB_NAME, 110);
            this.gridHandler.AddTextBoxColumn(IOData.VARIABLE, 140);
            this.gridHandler.AddTextBoxColumn(IOData.COMMENT, 0);
            #endregion

            this.gridHandler?.Init();
            this.configHandler?.Init();

            #region PROGRAM_SAVE_TICK + AUTO_SAVE
            void eventHandler(object sender, EventArgs args)
            {
                if (!string.IsNullOrEmpty(this.lastFilePath))
                {
                    this.ProjectSave();
                }
            }
            this.Shown += (sender, args) => autoSaveHandler.AddTickEventHandler(eventHandler);
            this.FormClosed += (sender, args) => autoSaveHandler.RemoveTickEventHandler(eventHandler);

            var timer = new Timer { Interval = 1000 };
            timer.Start();

            var objectSnapshotDict = new Dictionary<object, Dictionary<string, object>>()
            {
                {this.IOConfig, null },
                {this.ExcelImportConfig, null },
                {this.settings.GridSettings, null }
            };

            ParseSnapshotDict(objectSnapshotDict, forceRefreshSnapshot: true, skipSave: true);
            timer.Tick += (sender, e) =>
            {
                ParseSnapshotDict(objectSnapshotDict, forceRefreshSnapshot: false, skipSave: false);
            };
            #endregion

            UpdateConfigPanel();
        }

        private void ParseSnapshotDict(Dictionary<object, Dictionary<string, object>> objectSnapshotDict, bool forceRefreshSnapshot = false, bool skipSave = false)
        {
            bool saveNecessary = false;
            foreach(var entry in objectSnapshotDict.ToList()) //To list neede to make a copy so i can change the dict below!
            {
                var obj = entry.Key;
                var oldSnapshotDict = entry.Value;
                if(oldSnapshotDict == null || !Utils.ComparePublicFieldSnapshot(obj, oldSnapshotDict) || forceRefreshSnapshot)
                {
                    saveNecessary = true;

                    var snap = Utils.CreatePublicFieldSnapshot(obj);
                    objectSnapshotDict[obj] = snap;
                }
            }

            if(saveNecessary && !skipSave)
            {
                this.settings.Save();
            }
        }

        public void ProjectSave(bool saveAs = false)
        {
            var projectSave = new IOGenerationProjectSave();
            foreach (var entry in gridHandler.DataSource.GetNotEmptyDataDict())
            {
                projectSave.RowDict.Add(entry.Value, entry.Key);
            }
            projectSave.Save(ref lastFilePath, saveAs);

            this.Text = this.Name + ". File: " + lastFilePath;
        }

        public void ProjectLoad()
        {
            var loadedProjectSave = IOGenerationProjectSave.Load(ref lastFilePath);
            if (loadedProjectSave != null)
            {
                this.dataGridView.SuspendLayout();
                this.gridHandler.DataSource.InitializeData(this.gridHandler.RowCount);

                foreach (var entry in loadedProjectSave.RowDict)
                {
                    var rowIndex = entry.Key;
                    var data = entry.Value;
                    if (rowIndex >= 0 && rowIndex <= this.gridHandler.RowCount)
                    {
                        this.gridHandler.DataHandler.MoveValues(data, this.gridHandler.DataSource[rowIndex]);
                    }
                }

                this.dataGridView.Refresh();
                this.dataGridView.ResumeLayout();

                this.Text = this.Name + ". File: " + lastFilePath;
            }
        }
        private void UpdateConfigPanel()
        {
            configButtonPanel.SuspendLayout();

            configButtonPanel.Controls.Clear();

            configButtonPanel.Controls.Add(fcConfigButton);
            if (IOMemoryTypeEnum.DB.Equals(memoryTypeComboBox.SelectedValue))
            {
                configButtonPanel.Controls.Add(dbConfigButton);
            }
            else if (IOMemoryTypeEnum.MERKER.Equals(memoryTypeComboBox.SelectedValue))
            {
                configButtonPanel.Controls.Add(variableTableConfigButton);
            }
            configButtonPanel.Controls.Add(ioTableConfigButton);
            configButtonPanel.Controls.Add(segmentNameConfigButton);

            configButtonPanel.ResumeLayout();
        }
    }
}

/*
this.dataGridView.SortCompare += (sender, args) =>
{
    if (args.Column.Index == 0)
    {
        //This is required to avoid the values to go bottom and top when sorting. I want the empty lines always at the bottom!.
        var sortOrderMultiplier = dataGridView.SortOrder == SortOrder.Ascending ? -1 : 1;

        var cell1Address = SimaticTagAddress.FromAddress(args.CellValue1?.ToString());
        var cell2Address = SimaticTagAddress.FromAddress(args.CellValue2?.ToString());
        if (cell1Address == null)
        {
            args.SortResult = -1 * sortOrderMultiplier;
        }
        else if (cell2Address == null)
        {
            args.SortResult = 1 * sortOrderMultiplier;
        }
        else
        {
            args.SortResult = cell1Address.CompareTo(cell2Address);
        }

        args.Handled = true;
    }
};

dataGridView.MouseDoubleClick += (sender, args) =>
{
    var hitTest = dataGridView.HitTest(args.X, args.Y);
    if (hitTest.Type == DataGridViewHitTestType.Cell)
    {
        var cell = dataGridView.Rows[hitTest.RowIndex].Cells[hitTest.ColumnIndex];
        dataGridView.CurrentCell = cell;
        dataGridView.BeginEdit(true);
    }
};

dataGridView.CellPainting += (sender, args) =>
{
    if (args.RowIndex >= 0 && args.ColumnIndex >= 0)
    {
        if (dataGridView.Rows[args.RowIndex].Cells[args.ColumnIndex].Selected == true)
        {
            args.Paint(args.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.Border);
            using (Pen p = new Pen(Color.Red, 3))
            {
                Rectangle rect = args.CellBounds;
                rect.Width -= 2;
                rect.Height -= 2;
                args.Graphics.DrawRectangle(p, rect);
            }
            args.Handled = true;
        }
    }

    e.PaintBackground(e.CellBounds, true);  
    e.PaintContent(e.CellBounds);  
    using (SolidBrush brush = new SolidBrush(Color.FromArgb(255, 0, 0)))  
    {  
        Point[] pt = new Point[] { new Point(e.CellBounds.Right - 1, e.CellBounds.Bottom - 10), new Point(e.CellBounds.Right - 1, e.CellBounds.Bottom - 1), new Point(e.CellBounds.Right - 10, e.CellBounds.Bottom - 1) };  
        e.Graphics.FillPolygon(brush, pt);  
    }  
    e.Handled = true;  
};
*/