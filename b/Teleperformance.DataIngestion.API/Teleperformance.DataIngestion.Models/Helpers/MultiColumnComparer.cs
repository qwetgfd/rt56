using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Teleperformance.DataIngestion.Models.Helpers
{
    public class MultiColumnComparer : IComparer<object[]>
    {
        private bool isDescending;

        public MultiColumnComparer(bool descending = true)
        {
            isDescending = descending;
        }

        public int Compare(object[] x, object[] y)
        {
            for (int i = 0; i < x.Length; i++)
            {
                int result = Comparer<object>.Default.Compare(x[i], y[i]);
                if (result != 0)
                {
                    return isDescending ? -result : result; //negative result for Descending
                }
            }
            return 0;
        }
    }
}
