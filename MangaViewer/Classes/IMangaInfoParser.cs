using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Leayal.MangaViewer.Classes
{
    public interface IMangaInfoParser : IDisposable
    {
        public ReadOnlySpan<char> GetMangaName();

        public IEnumerable<IMangaChapterParser> BeginReadChapters();
    }

    public interface IMangaChapterParser : IDisposable
    {
        public static readonly string Chapter0 = "Chapter 0";

        public ReadOnlyMemory<char> GetChapterName();

        public IEnumerable<KeyValuePair<ReadOnlyMemory<char>, Guid>> BeginReadPages();
    }
}
