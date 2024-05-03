﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using TiaXmlReader.UndoRedo;
using static TiaXmlReader.Generation.GridHandler.GridExcelDragHandler;
using TiaXmlReader.Utility;
using TiaXmlReader.Generation.GridHandler.Data;
using TiaXmlReader.GenerationForms;
using TiaXmlReader.Javascript;
using TiaXmlReader.Generation.GridHandler.CustomColumns;

namespace TiaXmlReader.Generation.GridHandler
{
    public class GridHandler<C, T> where C : IGenerationConfiguration where T : IGridData<C>
    {
        public class ColumnInfo
        {
            public DataGridViewColumn Column { get; internal set; }
            public GridDataColumn DataColumn { get; internal set; }
            public int Width { get; set; }
            public bool Visible { get; set; } = true;
        }

        private readonly DataGridView dataGridView;
        private readonly GridSettings settings;
        private readonly C configuration;
        public GridDataHandler<C, T> DataHandler { get; private set; }
        public GridDataSource<C, T> DataSource { get; private set; }
        private readonly UndoRedoHandler undoRedoHandler;
        private readonly GridExcelDragHandler excelDragHandler;
        private readonly GridSortHandler<C, T> sortHandler;
        public GridTableScript<C, T> TableScript { get; private set; }

        private readonly List<ColumnInfo> columnInfoList;
        private readonly List<IGridCellPainter> cellPainterList;

        private bool init;

        public uint RowCount { get; set; } = 9;
        public bool AddRowIndexToRowHeader { get; set; } = true;
        public bool EnablePasteFromExcel { get; set; } = true;
        public bool EnableRowSelectionFromRowHeaderClick { get; set; } = true;
        public bool ShowJSContextMenuTopLeft {  get; set; } = true;

        public GridHandler(JavascriptScriptErrorReportingThread jsErrorHandlingThread, DataGridView dataGridView, GridSettings settings, C configuration, List<GridDataColumn> dataColumnList, IGridRowComparer<C, T> comparer = null)
        {
            this.dataGridView = dataGridView;
            // BUG => System.InvalidOperationException: 'L'operazione non può essere eseguita mentre è in corso il ridimensionamento di una colonna con riempimento automatico.'
            // FIX = https://stackoverflow.com/questions/34344499/invalidoperationexception-this-operation-cannot-be-performed-while-an-auto-fill
            this.settings = settings;
            this.configuration = configuration;
            this.DataHandler = new GridDataHandler<C, T>(this.dataGridView, dataColumnList);
            this.DataSource = new GridDataSource<C, T>(this.dataGridView, this.DataHandler);
            this.undoRedoHandler = new UndoRedoHandler();
            this.excelDragHandler = new GridExcelDragHandler(this.dataGridView, settings);
            this.sortHandler = new GridSortHandler<C, T>(this.dataGridView, this.DataSource, this.undoRedoHandler, comparer);
            this.TableScript = new GridTableScript<C, T>(this, jsErrorHandlingThread);

            this.columnInfoList = new List<ColumnInfo>();
            this.cellPainterList = new List<IGridCellPainter>();
        }

        public void AddCellPainter(IGridCellPainter cellPainter)
        {
            cellPainterList.Add(cellPainter);
        }

        public void SetDragPreviewAction(Action<DragData> action)
        {
            this.excelDragHandler.SetPreviewAction(action);
        }

        public void SetDragMouseUpAction(Action<DragData> action)
        {
            this.excelDragHandler.SetMouseUpAction(action);
        }

        public void Refresh()
        {
            this.dataGridView.RefreshEdit();
            this.dataGridView.Refresh();
        }

        public void Init()
        {
            if(init)
            {
                return;
            }

            typeof(DataGridView).InvokeMember("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty, null, this.dataGridView, new object[] { true });

            this.dataGridView.SuspendLayout();

            this.dataGridView.AutoGenerateColumns = false;

            this.dataGridView.Dock = DockStyle.Fill;
            this.dataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

            this.dataGridView.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            this.dataGridView.SelectionMode = DataGridViewSelectionMode.CellSelect;
            this.dataGridView.MultiSelect = true;

            this.dataGridView.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            this.dataGridView.Font = settings.GridFont;
            this.dataGridView.AllowUserToAddRows = false;

            this.dataGridView.DataError += DataErrorEventHandler;

            this.DataSource.InitializeData(this.RowCount);

            InitColumns();

            #region Cell Paiting
            var paintHandler = new GridCellPaintHandler(this.dataGridView);
            paintHandler.AddPainter(this.sortHandler); //ORDER IS IMPORTANT!
            paintHandler.AddPainter(this.excelDragHandler);
            paintHandler.AddPainter(new GridCellPreview<C,T>(this.DataSource, this.configuration, this.settings));
            paintHandler.AddPainterRange(cellPainterList);
            paintHandler.Init();
            #endregion

            #region RowHeaderNumber
            if (AddRowIndexToRowHeader)
            {
                this.dataGridView.RowPostPaint += (sender, args) =>
                {
                    var style = args.InheritedRowStyle;

                    var rowIdx = (args.RowIndex + 1).ToString();

                    var centerFormat = new StringFormat()
                    {
                        // right alignment might actually make more sense for numbers
                        Alignment = StringAlignment.Far,
                        LineAlignment = StringAlignment.Far
                    };

                    var textSize = TextRenderer.MeasureText(rowIdx, style.Font); //get the size of the string
                    dataGridView.RowHeadersWidth = Math.Max(dataGridView.RowHeadersWidth, textSize.Width + 15); //if header width lower then string width then resize

                    var headerBounds = new Rectangle(args.RowBounds.Left, args.RowBounds.Top, dataGridView.RowHeadersWidth, args.RowBounds.Height);
                    args.Graphics.DrawString(rowIdx, style.Font, SystemBrushes.ControlText, headerBounds, centerFormat);
                };
            }
            #endregion

            #region KeyDown - Paste/Delete/Undo/Redo
            this.dataGridView.KeyDown += (sender, args) =>
            {
                bool ctrlZ = args.Modifiers == Keys.Control && args.KeyCode == Keys.Z;
                bool ctrlY = args.Modifiers == Keys.Control && args.KeyCode == Keys.Y;
                bool ctrlV = args.Modifiers == Keys.Control && args.KeyCode == Keys.V;
                bool shiftIns = args.Modifiers == Keys.Shift && args.KeyCode == Keys.Insert;

                if (EnablePasteFromExcel && (ctrlV || shiftIns))
                {
                    PasteFromExcel();
                }

                if (args.KeyCode == Keys.Delete || args.KeyCode == Keys.Cancel)
                {
                    DeleteSelectedCells();
                }

                if (ctrlZ)
                {
                    undoRedoHandler.Undo();
                }

                if (ctrlY)
                {
                    undoRedoHandler.Redo();
                }
            };
            #endregion

            #region MouseClick - RowSelection
            if (EnableRowSelectionFromRowHeaderClick)
            {
                int lastClickedRowIndex = -1;
                this.dataGridView.MouseDown += (sender, args) =>
                {
                    var hitTest = dataGridView.HitTest(args.X, args.Y);
                    if (hitTest.Type != DataGridViewHitTestType.RowHeader)
                    {
                        lastClickedRowIndex = -1;
                    }

                    switch (hitTest.Type)
                    {
                        case DataGridViewHitTestType.None: //I want that to clear the selection, you do a simple click in an empty area!
                            dataGridView.ClearSelection();
                            dataGridView.CurrentCell = null; //This avoid the situation where if you click the old cell again, it start editing immediately! 
                            break;
                        case DataGridViewHitTestType.RowHeader: //If i click a row head, i want the whole row to be selected!
                            if (Control.ModifierKeys == Keys.Shift && dataGridView.CurrentRow != null)
                            {
                                var biggestIndex = Math.Max(lastClickedRowIndex, hitTest.RowIndex);
                                var lowestIndex = Math.Min(lastClickedRowIndex, hitTest.RowIndex);
                                for (int x = lowestIndex + 1; x < biggestIndex + 1; x++)
                                {
                                    if (x == dataGridView.CurrentRow.Index)
                                    {
                                        continue;
                                    }

                                    foreach (DataGridViewCell cell in dataGridView.Rows[x].Cells)
                                    {
                                        cell.Selected = !cell.Selected;
                                    }
                                }

                                lastClickedRowIndex = hitTest.RowIndex;
                            }
                            else
                            {
                                dataGridView.ClearSelection();
                                dataGridView.CurrentCell = dataGridView.Rows[hitTest.RowIndex].Cells[0]; //I need to set the current cell, because i use the CurrentRow as a "starting row"
                                                                                                         //Do not cancel current cell! It might select the first cell in the grid and mess up selection.

                                lastClickedRowIndex = hitTest.RowIndex;
                                foreach (DataGridViewCell cell in dataGridView.Rows[hitTest.RowIndex].Cells)
                                {
                                    cell.Selected = true;
                                }
                            }

                            break;
                        case DataGridViewHitTestType.Cell:
                            break;
                    }
                };
            }
            #endregion

            #region CellEdit Begin-End
            GridCellChange textBoxCellEdit = null;
            dataGridView.CellBeginEdit += (sender, args) =>
            {
                textBoxCellEdit = null;

                var cell = this.dataGridView.Rows[args.RowIndex].Cells[args.ColumnIndex];
                if (cell is DataGridViewTextBoxCell || cell is DataGridViewComboBoxCell)
                {
                    textBoxCellEdit = new GridCellChange(cell);
                }
            };

            dataGridView.CellEndEdit += (sender, args) =>
            {
                if (textBoxCellEdit?.cell == null)
                {
                    return;
                }

                var cell = this.dataGridView.Rows[args.RowIndex].Cells[args.ColumnIndex];
                if (cell == textBoxCellEdit.cell)
                {
                    textBoxCellEdit.NewValue = dataGridView.Rows[args.RowIndex].Cells[args.ColumnIndex].Value;
                    ChangeCell(textBoxCellEdit, applyChanges: false);

                    textBoxCellEdit = null;
                }
            };

            dataGridView.CellContentClick += (sender, args) =>
            {
                var rowIndex = args.RowIndex;
                var columnIndex = args.ColumnIndex;
                if(rowIndex < 0 || rowIndex >= this.dataGridView.RowCount || columnIndex < 0 || columnIndex >= this.dataGridView.ColumnCount)
                {
                    return;
                }

                var cell = this.dataGridView.Rows[rowIndex].Cells[columnIndex];
                if (cell is DataGridViewCheckBoxCell checkBoxCell)
                {
                    var oldValue = (bool)(checkBoxCell.Value ?? false); //In this case the value is still the old one. For checkBox, been boolean value, i can predict the next!
                    var newValue = !oldValue;
                    ChangeCell(new GridCellChange(checkBoxCell) { OldValue = oldValue, NewValue = newValue }, applyChanges: false);
                }
            };
            #endregion

            #region SHOW_JS_CONTEXT_MENU
            this.dataGridView.CellMouseClick += (sender, args) =>
            {
                if (args.RowIndex == -1 && args.ColumnIndex == -1 && args.Button == MouseButtons.Right && this.TableScript.Valid)
                {
                    var menuItem = new MenuItem();
                    menuItem.Text = "Execute Javascript";
                    menuItem.Click += (s, a) => this.TableScript.ShowConfigForm(this.dataGridView);

                    var contextMenu = new ContextMenu();
                    contextMenu.MenuItems.Add(menuItem);

                    contextMenu.Show(this.dataGridView, this.dataGridView.PointToClient(Cursor.Position));
                }
            };
            #endregion

            this.excelDragHandler.Init();
            this.sortHandler.Init();

            this.dataGridView.ResumeLayout();

            init = true;
        }

        private void DataErrorEventHandler(object sender, DataGridViewDataErrorEventArgs args)
        {
            var cell = this.dataGridView.Rows[args.RowIndex].Cells[args.ColumnIndex];
            if (cell is DataGridViewComboBoxCell comboBoxCell)
            {
                comboBoxCell.Value = "";
            }
        }

        public DataGridViewTextBoxColumn AddTextBoxColumn(GridDataColumn dataColumn, int width)
        {
            return AddColumn(new DataGridViewTextBoxColumn(), dataColumn, width);
        }

        public DataGridViewCheckBoxColumn AddCheckBoxColumn(GridDataColumn dataColumn, int width)
        {
            return AddColumn(new DataGridViewCheckBoxColumn(), dataColumn, width);
        }

        public DataGridViewComboBoxColumn AddComboBoxColumn(GridDataColumn dataColumn, int width, string[] items)
        {
            var column = AddColumn(new DataGridViewComboBoxColumn(), dataColumn, width);
            column.Items.AddRange(items);
            column.FlatStyle = FlatStyle.Flat;
            return column;
        }

        public CC AddCustomColumn<CC>(CC customColumn, GridDataColumn dataColumn, int width) where CC : DataGridViewColumn, IGridCustomColumn
        {
            return this.AddColumn(customColumn, dataColumn, width);
        }

        private CL AddColumn<CL>(CL column, GridDataColumn dataColumn, int width) where CL : DataGridViewColumn
        {
            this.columnInfoList.Add(new ColumnInfo()
            {
                Column = column,
                DataColumn = dataColumn,
                Width = width
            });
            return column;
        }

        public ColumnInfo GetColumnInfo(GridDataColumn dataColumn)
        {
           return columnInfoList.Where(i => i.DataColumn == dataColumn).FirstOrDefault();
        }

        public void InitColumns()
        {
            foreach(var column in this.dataGridView.Columns)
            {
                if(column is IGridCustomColumn customColumn)
                {
                    customColumn.UnregisterEvents(this.dataGridView);
                }
            }

            this.dataGridView.Columns.Clear();

            columnInfoList.Sort((one, two) => one.DataColumn.ColumnIndex.CompareTo(two.DataColumn.ColumnIndex));
            foreach (var columnInfo in columnInfoList)
            {
                var column = columnInfo.Column;
                if(columnInfo.Visible)
                {
                    if (column is IGridCustomColumn customColumn)
                    {
                        customColumn.RegisterEvents(this.dataGridView);
                    }

                    column.Visible = true;

                    column.Name = columnInfo.DataColumn.Name;
                    column.DisplayIndex = columnInfo.DataColumn.ColumnIndex;
                    column.DataPropertyName = columnInfo.DataColumn.DataPropertyName;
                    column.DefaultCellStyle.SelectionBackColor = Color.LightGray;
                    column.DefaultCellStyle.BackColor = SystemColors.ControlLightLight;
                    column.DefaultCellStyle.SelectionForeColor = Color.Black;
                    column.DefaultCellStyle.ForeColor = Color.Black;
                    column.AutoSizeMode = columnInfo.Width <= 0 ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None;
                    column.Width = columnInfo.Width;
                    column.MinimumWidth = 15;
                    column.SortMode = DataGridViewColumnSortMode.Programmatic;

                    column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    column.HeaderCell.Style.Padding = new Padding(0);
                    column.HeaderCell.Style.WrapMode = DataGridViewTriState.True;
                }
                else
                {
                    column.Visible = false;
                }

                this.dataGridView.Columns.Add(column);
            }
        }

        public bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            foreach(var column in this.dataGridView.Columns)
            {
                if(column is IGridCustomColumn customColumn && customColumn.ProcessCmdKey(ref msg, keyData))
                {
                    return true;
                }
            }

            return false;
        }

        private void PasteFromExcel()
        {
            var clipboardDataObject = (DataObject)Clipboard.GetDataObject();
            if (clipboardDataObject.GetDataPresent(DataFormats.Text))
            {
                var pastedCellList = new List<GridCellChange>();

                var pasteString = clipboardDataObject.GetData(DataFormats.Text).ToString();
                if (pasteString.Contains("\r\n") || pasteString.Contains('\t'))
                {//If contains new lines or tab it needs to handled like an excel file. New line => next row. Tab => next column.
                    string[] pastedRows = Regex.Split(pasteString.TrimEnd("\r\n".ToCharArray()), "\r\n");

                    int startRowPointer = dataGridView.CurrentCell.RowIndex; //The currentCell row index needs to be taken BEFORE adding cells otherwise it will be moved!
                    int startColumnPointer = dataGridView.CurrentCell.ColumnIndex;

                    int rowPointer = startRowPointer;
                    for (int pastedRowIndex = startRowPointer; pastedRowIndex < pastedRows.Length; rowPointer++, pastedRowIndex++)
                    {
                        if (rowPointer >= dataGridView.RowCount)
                        {
                            break;
                        }

                        string[] pastedColumns = pastedRows[pastedRowIndex].Split('\t');

                        int columnPointer = startColumnPointer;
                        for (int pastedColumnPointer = 0; pastedColumnPointer < pastedColumns.Length; columnPointer++, pastedColumnPointer++)
                        {
                            if (columnPointer >= dataGridView.ColumnCount)
                            {
                                break;
                            }

                            var cell = dataGridView.Rows[rowPointer]?.Cells[columnPointer];
                            if (cell != null)
                            {
                                pastedCellList.Add(new GridCellChange(cell) { NewValue = pastedColumns[pastedColumnPointer] });
                            }
                        }
                    }
                }
                else
                {//If is a normal string, i will paste in ALL the selected cells!
                    foreach (DataGridViewCell selectedCell in dataGridView.SelectedCells)
                    {
                        pastedCellList.Add(new GridCellChange(selectedCell) { NewValue = pasteString });
                    }
                }

                ChangeCells(pastedCellList);
            }
        }

        public void DeleteSelectedCells()
        {
            var deletedCellList = new List<GridCellChange>();
            foreach (DataGridViewCell selectedCell in dataGridView.SelectedCells)
            {
                deletedCellList.Add(new GridCellChange(selectedCell) { NewValue = null }); //Set value to null so it will clear also checkboxes
            }

            this.ChangeCells(deletedCellList);
        }

        public void ChangeRow(int rowIndex, T data)
        {
            this.ChangeCells(this.DataHandler.CreateCellChanges(rowIndex, data));
        }

        public void ChangeMultipleRows(Dictionary<int, T> dataDict)
        {
            this.ChangeCells(this.DataHandler.CreateCellChanges(dataDict));
        }

        public void ChangeCell(GridCellChange cell, bool applyChanges = true)
        {
            ChangeCells(Utils.SingletonList(cell), applyChanges);
        }

        public void ChangeCells(List<GridCellChange> cellChangeList, bool applyChanges = true)
        {
            if (cellChangeList.Count == 0)
            {
                return;
            }

            try
            {
                if (applyChanges)
                {
                    undoRedoHandler.Lock(); //Lock the handler. I do not want more actions been added by events here since are all handled below!

                    cellChangeList.ForEach(cellChange => cellChange.ApplyNewValue());
                    dataGridView.Refresh();

                    undoRedoHandler.Unlock();
                }

                void undoRedoAction()
                {
                    undoRedoHandler.Lock();
                    cellChangeList.ForEach(cellChange => cellChange.ApplyOldValue());
                    undoRedoHandler.Unlock();

                    ShowCell(cellChangeList[cellChangeList.Count - 1].cell);  //Setting se current cell already center the grid to it.
                    undoRedoHandler.AddRedo(() =>
                    {
                        ChangeCells(cellChangeList);
                        ShowCell(cellChangeList[0].cell);  //Setting se current cell already center the grid to it.
                    });
                }

                undoRedoHandler.AddUndo(undoRedoAction);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error while changing cells", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowCell(DataGridViewCell cell)
        {
            dataGridView.RefreshEdit(); //This is required to refresh checkbox otherwise, if the undo is in a selected cell, it will not update visually (DATA IS CHANGED!)
            dataGridView.Refresh();

            dataGridView.CurrentCell = cell; //Setting se current cell already center the grid to it.
            dataGridView.Refresh();
        }

    }
}
