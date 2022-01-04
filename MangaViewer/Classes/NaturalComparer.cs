using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Leayal.MangaViewer.Classes
{
    class NaturalComparer : StringComparer, IComparer<string>
    {
        public static readonly NaturalComparer Default = new NaturalComparer();
        private NaturalComparer() : base() { }
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string? psz1, string? psz2);

        public override int Compare(string? x, string? y)
        {
            if (this.Equals(x, y))
            {
                return 0;
            }
            else
            {
                return StrCmpLogicalW(x, y);
            }
        }

        public override bool Equals(string? x, string? y)
            => StringComparer.OrdinalIgnoreCase.Equals(x, y);

        public override int GetHashCode(string obj)
            => StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
    }
}
