using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace Leayal.MangaViewer.Classes
{
    public sealed class NaturalComparer : StringComparer, IComparer<ReadOnlyMemory<char>>
    {
        public static readonly NaturalComparer Default = new NaturalComparer();

        private NaturalComparer() : base() { }

        public override int Compare(string? x, string? y)
        {
            if (this.Equals(x, y))
            {
                return 0;
            }
            else
            {
                if (x == null) return -1;
                else if (y == null) return 1;
                else return Windows.Win32.PInvoke.StrCmpLogical(x, y);
            }
        }

        // Greatest mess ever.
        public int Compare(ReadOnlySpan<char> x, ReadOnlySpan<char> y)
        {
            if (x.IsEmpty && y.IsEmpty)
            {
                return 0;
            }
            else
            {
                if (x.IsEmpty) return -1;
                else if (y.IsEmpty) return 1;
                else
                {
                    unsafe
                    {
                        char* pinnedX, pinnedY;
                        if (x.IndexOf(char.MinValue) == -1)
                        {
                            var stackX  = stackalloc char[x.Length + 1];
                            pinnedX = stackX;
                            x.CopyTo(new Span<char>(stackX, x.Length + 1));
                        }
                        else
                        {
                            pinnedX = (char*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(x));
                        }

                        if (y.IndexOf(char.MinValue) == -1)
                        {
                            var stackY = stackalloc char[y.Length + 1];
                            pinnedY = stackY;
                            y.CopyTo(new Span<char>(stackY, y.Length + 1));
                        }
                        else
                        {
                            pinnedY = (char*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(y));
                        }
                        return Windows.Win32.PInvoke.StrCmpLogical(new Windows.Win32.Foundation.PCWSTR(pinnedX), new Windows.Win32.Foundation.PCWSTR(pinnedY));
                    }
                }
            }
        }

        public int Compare(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y) => this.Compare(x.Span, y.Span);

        public override bool Equals(string? x, string? y)
            => StringComparer.OrdinalIgnoreCase.Equals(x, y);

        public override int GetHashCode(string obj)
            => StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
    }
}
