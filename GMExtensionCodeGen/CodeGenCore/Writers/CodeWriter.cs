using CodeGenCore.Helpers;
using CodeGenCore.Writers.Concrete;
using System.Text;

namespace CodeGenCore.Writers
{
    public interface ICodeWriter
    {
        int IndentLevel { get; }
        ICodeWriter Append(string value);
        ICodeWriter AppendLine(string value = "");
        ICodeWriter IncrementIndent();
        ICodeWriter DecrementIndent();
        ICodeWriter AppendJoin(IEnumerable<string> parts, string separator = ", ");
    }

    public static class CodeWriter
    {
        public static ICodeWriter From(IndentedStringBuilder builder) => new StringBuilderCodeWriter(builder);
        public static ICodeWriter From(TextWriter writer, string indentUnit = "    ") => new TextCodeWriter(writer, indentUnit);
        public static ICodeWriter ToFile(string path, string indentUnit = "    ")
            => new TextCodeWriter(new StreamWriter(File.Create(path), Encoding.UTF8) { AutoFlush = true }, indentUnit);
        public static ICodeWriter AsCodeWriter(this IndentedStringBuilder builder) => From(builder);
        public static ICodeWriter AsCodeWriter(this TextWriter writer) => From(writer);
    }

}

