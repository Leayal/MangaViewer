namespace Leayal.MangaViewer.Classes
{
    sealed class MangaPageInfo
    {
        public readonly ReadOnlyMemory<char> PageName;

        public MangaPageInfo(in ReadOnlyMemory<char> pageName)
        {
            this.PageName = pageName;
        }
    }
}
