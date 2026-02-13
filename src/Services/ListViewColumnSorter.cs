using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;

namespace CopilotApp.Services;

internal class ListViewColumnSorter : IComparer
{
    public int SortColumn { get; set; }
    public SortOrder Order { get; set; }

    private readonly HashSet<int> _numericColumns;

    public ListViewColumnSorter(int column = 1, SortOrder order = SortOrder.Descending, HashSet<int>? numericColumns = null)
    {
        SortColumn = column;
        Order = order;
        _numericColumns = numericColumns ?? new HashSet<int> { 1 };
    }

    public int Compare(object? x, object? y)
    {
        if (x is not ListViewItem itemX || y is not ListViewItem itemY)
        {
            return 0;
        }

        string textX = SortColumn < itemX.SubItems.Count ? itemX.SubItems[SortColumn].Text : "";
        string textY = SortColumn < itemY.SubItems.Count ? itemY.SubItems[SortColumn].Text : "";

        int result;
        if (_numericColumns.Contains(SortColumn))
        {
            int.TryParse(textX, out int numX);
            int.TryParse(textY, out int numY);
            result = numX.CompareTo(numY);
        }
        else
        {
            result = string.Compare(textX, textY, StringComparison.OrdinalIgnoreCase);
        }

        return Order == SortOrder.Descending ? -result : result;
    }
}
