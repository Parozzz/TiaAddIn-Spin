﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TiaXmlReader.GenerationForms.IO
{
    public class IOGenerationDataSource
    {
        private readonly DataGridView dataGridView;

        private readonly List<IOGenerationData> dataList;
        private readonly BindingList<IOGenerationData> bindingList;

        public IOGenerationDataSource(DataGridView dataGridView)
        {
            this.dataGridView = dataGridView;

            dataList = new List<IOGenerationData>();
            for (int i = 0; i < IOGenerationForm.TOTAL_ROW_COUNT; i++)
            {
                dataList.Add(new IOGenerationData());
            }

            bindingList = new BindingList<IOGenerationData>(dataList);
        }

        public void Init()
        {
            this.dataGridView.DataSource = new BindingSource() { DataSource = bindingList }; ;
        }

        public void Sort(IComparer<IOGenerationData> comparer, SortOrder sortOrder)
        {
            if (sortOrder != SortOrder.None)
            {
                dataList.Sort(comparer);
                if (sortOrder == SortOrder.Descending)
                {
                    dataList.Reverse();
                }

                dataGridView.Refresh();
            }
        }

        public Dictionary<IOGenerationData, int> CreateDataListSnapshot()
        {
            var dict = new Dictionary<IOGenerationData, int>();
            for (int x = 0; x < dataList.Count; x++)
            {
                dict.Add(dataList[x], x);
            }
            return dict;
        }

        public void RestoreDataListSnapshot(Dictionary<IOGenerationData, int> dict)
        {
            dataList.Sort((x, y) =>
            {
                if (!dict.ContainsKey(x) || !dict.ContainsKey(y))
                {
                    return 0;
                }

                return dict[x].CompareTo(dict[y]);
            });
            dataGridView.Refresh();
        }

    }
}
