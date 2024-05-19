﻿using InfoBox;
using System.Data;
using TiaXmlReader.Generation.GridHandler;
using TiaXmlReader.Generation.GridHandler.Data;
using TiaXmlReader.GenerationForms;
using TiaXmlReader.Languages;
using TiaXmlReader.Utility;

namespace TiaUtilities.Generation.GridHandler
{
    public partial class GridFindForm : Form
    {
        public class FindData<C, T>(T data, GridDataColumn column, int row, string findText) where C : IGenerationConfiguration where T : IGridData<C>
        {
            public T Data { get; init; } = data;
            public GridDataColumn Column { get; init; } = column;
            public int Row { get; init; } = row;
            public string FindText { get; init; } = findText;
        }

        public static void StartFind<C, T>(GridHandler<C, T> gridHandler) where C : IGenerationConfiguration where T : IGridData<C>
        {
            var form = gridHandler.DataGridView.FindForm();
            if (form == null)
            {
                return;
            }

            var findForm = new GridFindForm();

            var currentCell = gridHandler.DataGridView.CurrentCell;
            if (currentCell != null && currentCell.Value is string strValue)
            {
                findForm.FindTextBox.Text = strValue;
            }

            findForm.FormClosed += (sender, args) => gridHandler.FindData = null;

            findForm.FindButton.Click += (sender, args) =>
            {
                TryFindText(gridHandler, findForm);
            };

            findForm.ReplaceButton.Click += (sender, args) =>
            {
                var replaceText = findForm.ReplaceTextBox.Text;
                if ((gridHandler.FindData == null && !TryFindText(gridHandler, findForm)) || string.IsNullOrEmpty(replaceText))
                {
                    return;
                }

                var cellChange = GridFindForm.CreateReplaceCellChange(gridHandler.FindData, replaceText);
                if (cellChange != null)
                {
                    gridHandler.ChangeCell(cellChange);
                    TryFindText(gridHandler, findForm); //Select the next one!
                }
            };

            findForm.ReplaceAllButton.Click += (sender, args) =>
            {
                var replaceText = findForm.ReplaceTextBox.Text;
                if (string.IsNullOrEmpty(replaceText))
                {
                    return;
                }

                gridHandler.FindData = null; //I want to search everything. So reset and start from the top.

                var cellChangeList = new List<GridCellChange>();
                while (TryFindText(gridHandler, findForm, showFoundCell: false, showInfoAndClearOnFail: false))
                {
                    var cellChange = CreateReplaceCellChange(gridHandler.FindData, replaceText);
                    if (cellChange != null)
                    {
                        cellChangeList.Add(cellChange);
                    }
                }
                gridHandler.ChangeCells(cellChangeList);

                var title = Localization.Get("FIND_FORM");
                var searchCompletedText = Localization.Get("FIND_FORM_REPLACE_ALL_COMPLETED").Replace("{count}", cellChangeList.Count.ToString());
                InformationBox.Show(searchCompletedText, title,
                        buttons: InformationBoxButtons.OK,
                        icon: InformationBoxIcon.Information,
                        titleStyle: InformationBoxTitleIconStyle.SameAsBox);
                gridHandler.FindData = null; //Allows the search to loop around and start from top!
            };

            findForm.Show(form);
        }


        private static GridCellChange? CreateReplaceCellChange<C, T>(FindData<C, T>? findData, string replaceText) where C : IGenerationConfiguration where T : IGridData<C>
        {
            if (findData == null)
            {
                return null;
            }

            var text = findData.Column.GetValueFrom<string>(findData.Data);
            if (text == null)
            {
                return null;
            }

            var replacedText = text.Replace(findData.FindText, replaceText, StringComparison.OrdinalIgnoreCase);
            return new GridCellChange(findData.Column.ColumnIndex, findData.Row) { NewValue = replacedText };
        }

        private static bool TryFindText<C, T>(GridHandler<C, T> gridHandler, GridFindForm findForm, bool showFoundCell = true, bool showInfoAndClearOnFail = true) where C : IGenerationConfiguration where T : IGridData<C>
        {
            var matchCase = findForm.MatchCaseCheckBox.Checked;
            var findText = findForm.FindTextBox.Text;
            if (string.IsNullOrEmpty(findText))
            {
                return false;
            }

            var dataDict = gridHandler.DataSource.GetNotEmptyDataDict();

            var found = false;
            foreach (var entry in dataDict)
            {
                var row = entry.Value;
                var data = entry.Key;

                var columnList = data.GetColumns();
                foreach (var column in columnList.Where(c => c.PropertyInfo.PropertyType == typeof(string)))
                {
                    var value = column.GetValueFrom<string>(data);
                    if (value == null)
                    {
                        continue;
                    }

                    if (value.Contains(findText, matchCase ? StringComparison.Ordinal : StringComparison.CurrentCultureIgnoreCase))
                    {
                        var findDataOK = gridHandler.FindData == null; //If the FindData is null nothing is selected yet and everything found will be fine.
                        if (!findDataOK && gridHandler.FindData != null)
                        {
                            var dataIsEqual = Utils.AreEqualsObject(gridHandler.FindData.Data, data);
                            if (!dataIsEqual) //If data is not equals, the next selectable should be on a row lower than the actual.
                            {
                                findDataOK = row > gridHandler.FindData.Row;
                            }
                            else //If data is the same, it needs to be on a column on the right.
                            {
                                findDataOK = column.ColumnIndex > gridHandler.FindData.Column.ColumnIndex;
                            }

                            if (!findDataOK)
                            {
                                continue;
                            }
                        }

                        gridHandler.FindData = new FindData<C, T>(data, column, row, findText);
                        found = true;

                        if (showFoundCell)
                        {
                            gridHandler.ShowCell(row, column.ColumnIndex);
                        }
                        break;
                    }
                }

                if (found)
                {
                    break;
                }
            }

            if (!found && showInfoAndClearOnFail)
            {
                var title = Localization.Get("FIND_FORM");
                var searchCompletedText = Localization.Get("FIND_FORM_SEARCH_COMPLETED");

                InformationBox.Show(searchCompletedText, title,
                        buttons: InformationBoxButtons.OK,
                        icon: InformationBoxIcon.Information,
                        titleStyle: InformationBoxTitleIconStyle.SameAsBox);
                gridHandler.FindData = null; //Allows the search to loop around and start from top!
            }

            return found;
        }


        public GridFindForm()
        {
            InitializeComponent();

            Init();
        }

        private void Init()
        {
            Translate();
        }

        private void Translate()
        {
            this.Text = Localization.Get("FIND_FORM");
            this.FindLabel.Text = this.FindButton.Text = Localization.Get("FIND_FORM_FIND");
            this.ReplaceLabel.Text = this.ReplaceButton.Text = Localization.Get("FIND_FORM_REPLACE");
            this.ReplaceAllButton.Text = Localization.Get("FIND_FORM_REPLACE_ALL");
            this.MatchCaseCheckBox.Text = Localization.Get("FIND_FORM_MATCH_CASE");
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Enter:
                    this.FindButton.PerformClick();
                    return true;
                case Keys.Enter | Keys.Control:
                    this.ReplaceButton.PerformClick();
                    return true;
                case Keys.Escape:
                    this.Close();
                    return true;
            }

            // Call the base class
            return base.ProcessCmdKey(ref msg, keyData);
        }

    }
}
