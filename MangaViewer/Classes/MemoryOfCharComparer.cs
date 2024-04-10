using System.Diagnostics.CodeAnalysis;

namespace Leayal.MangaViewer.Classes
{
    abstract class MemoryOfCharEqualityComparer : IEqualityComparer<ReadOnlyMemory<char>>
    {
        public readonly static MemoryOfCharEqualityComparer Ordinal = new OrdinalMemoryOfCharEqualityComparer();
        public readonly static MemoryOfCharEqualityComparer OrdinalIgnoreCase = new OrdinalIgnoreCaseMemoryOfCharEqualityComparer();

        private MemoryOfCharEqualityComparer() { }

        public abstract bool Equals(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y);

        public abstract int GetHashCode([DisallowNull] ReadOnlyMemory<char> obj);

        sealed class OrdinalMemoryOfCharEqualityComparer : MemoryOfCharEqualityComparer
        {
            public override bool Equals(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y)
            => MemoryExtensions.Equals(x.Span, y.Span, StringComparison.Ordinal);

            public override int GetHashCode([DisallowNull] ReadOnlyMemory<char> obj)
                => string.GetHashCode(obj.Span, StringComparison.Ordinal);
        }

        sealed class OrdinalIgnoreCaseMemoryOfCharEqualityComparer : MemoryOfCharEqualityComparer
        {
            public override bool Equals(ReadOnlyMemory<char> x, ReadOnlyMemory<char> y)
            => MemoryExtensions.Equals(x.Span, y.Span, StringComparison.OrdinalIgnoreCase);

            public override int GetHashCode([DisallowNull] ReadOnlyMemory<char> obj)
                => string.GetHashCode(obj.Span, StringComparison.OrdinalIgnoreCase);
        }
    }
}
