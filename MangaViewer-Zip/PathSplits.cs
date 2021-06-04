using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaViewer_Zip
{
    public readonly ref struct PathSplits
    {
        private readonly ReadOnlySpan<char> target;

        public PathSplits(in string str, int start, int length)
        {
            this.target = str.AsSpan(start, length);
        }

        public PathSplits(in string str, int start)
        {
            this.target = str.AsSpan(start);
        }

        public PathSplits(in string str)
        {
            
            this.target = str.AsSpan();
        }

        public PathSplits(in ReadOnlySpan<char> str)
        {
            this.target = str;
        }

        public ref readonly char this[int index] => ref this.target[index];
    }
}
