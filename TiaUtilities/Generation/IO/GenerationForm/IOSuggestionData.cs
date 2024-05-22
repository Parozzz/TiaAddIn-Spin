﻿using Newtonsoft.Json;
using TiaXmlReader.Generation.GridHandler.Data;
using TiaXmlReader.Languages;

namespace TiaXmlReader.Generation.IO.GenerationForm
{
    public class IOSuggestionData : IGridData<IOConfiguration>
    {
        private readonly static int COLUMN_COUNT = 0;
        //THESE IS THE ORDER IN WHICH THEY APPEAR!
        public static readonly GridDataColumn VALUE;
        public static readonly IReadOnlyList<GridDataColumn> COLUMN_LIST;

        static IOSuggestionData()
        {
            var type = typeof(IOSuggestionData);
            VALUE = GridDataColumn.GetFromReflection(type, COLUMN_COUNT++, nameof(IOSuggestionData.Value), "suggestion");

            var columnList = GridDataColumn.GetStaticColumnList(type);
            columnList.Sort((x, y) => x.ColumnIndex.CompareTo(y.ColumnIndex));
            COLUMN_LIST = columnList.AsReadOnly();
        }

        [JsonProperty][Localization("IO_SUGGESTION_DATA_VALUE")] public string? Value { get; set; }

        public object? this[int column]
        {
            get
            {
                if (column < 0 || column >= COLUMN_LIST.Count)
                {
                    throw new InvalidOperationException("Invalid index for get square bracket operator in IOData");
                }

                return COLUMN_LIST[column].PropertyInfo.GetValue(this);
            }
        }
        public IReadOnlyList<GridDataColumn> GetColumns()
        {
            return COLUMN_LIST;
        }
        public GridDataColumn GetColumn(int column)
        {
            return COLUMN_LIST[column];
        }
        public GridDataPreview GetPreview(GridDataColumn column, IOConfiguration config)
        {
            return this.GetPreview(column.ColumnIndex, config);
        }

        public GridDataPreview GetPreview(int column, IOConfiguration config)
        {
            return null;
        }

        public void Clear()
        {
            this.Value = "";
        }

        public bool IsEmpty()
        {
            return string.IsNullOrEmpty(Value);
        }
    }
}