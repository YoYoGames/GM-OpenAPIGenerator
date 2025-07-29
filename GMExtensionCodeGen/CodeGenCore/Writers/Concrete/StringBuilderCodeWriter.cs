

using CodeGenCore.Helpers;

namespace CodeGenCore.Writers.Concrete
{
    public sealed class StringBuilderCodeWriter(IndentedStringBuilder inner) : ICodeWriter
    {
        private readonly IndentedStringBuilder _inner = inner;

        public int IndentLevel => _inner.IndentCount;
        public ICodeWriter Append(string value) { _inner.Append(value); return this; }
        public ICodeWriter AppendLine(string value = "") { _inner.AppendLine(value); return this; }
        public ICodeWriter IncrementIndent() { _inner.IncrementIndent(); return this; }
        public ICodeWriter DecrementIndent() { _inner.DecrementIndent(); return this; }
        public ICodeWriter AppendJoin(IEnumerable<string> parts, string separator = ", ")
        {
            _inner.AppendJoin(parts, separator);
            return this;
        }
        public override string ToString() => _inner.ToString();
    }

}

