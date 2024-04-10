using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Leayal.MangaViewer.Classes
{
    sealed class MangaChapterInfo : IComparable<MangaChapterInfo>
    {
        public MangaChapterInfo(ReadOnlyMemory<char> chapterName, IReadOnlyList<Guid> pages)
        {
            this.ChapterName = chapterName;
            this.Pages = pages;
        }

        public readonly ReadOnlyMemory<char> ChapterName;

        public readonly IReadOnlyList<Guid> Pages;

        public int CompareTo(MangaChapterInfo? other)
        {
            if (other == null) return 1;
            return NaturalComparer.Default.Compare(this.ChapterName, other.ChapterName);
        }
    }
}
