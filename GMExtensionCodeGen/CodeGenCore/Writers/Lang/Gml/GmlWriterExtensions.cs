using CodeGenCore.Writers.CoreExtensions;
using CodeGenCore.Writers.Lang.CStyle;

namespace CodeGenCore.Writers.Lang.Gml
{

    /// <summary>
    /// Fluent helpers that emit <strong>GameMaker-specific</strong> syntax:
    /// <list type="bullet">
    /// <item>function declarations &amp; constructors</item>
    /// <item><c>struct</c> constructor keyword</item>
    /// <item><c>enum</c> with trailing commas handled</item>
    /// <item>map/list/struct/array accessors (<c>[? ! $]</c>)</item>
    /// <item>var / static assignment shorthands</item>
    /// <item>JSDoc generation helpers</item>
    /// </list>
    /// All general C-style flow constructs live in
    /// <see cref="CStyleWriterExtensions"/>.
    /// </summary>
    public static class GmlWriterExtensions
    {
        public static ICodeWriter Repeat(this ICodeWriter io, string timesExpr, Action<ICodeWriter> body, string repeatKeyword = "repeat") => io.Keyword(repeatKeyword, timesExpr, body);

        public static ICodeWriter DoUntil(this ICodeWriter io, Action<ICodeWriter> body, string condition) => io.Line("do").Block(body).Line($" until ({condition});");

        public static ICodeWriter Enum(this ICodeWriter io, string name, IEnumerable<EnumMember> members)
        {
            var list = members.ToList();
            io.Line($"enum {name}")
              .Block(b =>
              {
                  for (int i = 0; i < list.Count; i++)
                  {
                      var line = list[i].ToMemberString();
                      if (i < list.Count - 1 && !line.EndsWith("//"))
                          line += ",";
                      b.Line(line);
                  }
              })
              .Line();               // blank after enum
            return io;
        }

        public static ICodeWriter Enum(this ICodeWriter io, string name, params (string Name, string? Value, string? Comment)[] members) => io.Enum(name, members.Select(m => new EnumMember(m.Name, m.Value, m.Comment)));

        public static ICodeWriter Enum(this ICodeWriter io, string name, IEnumerable<string> memberNames) => io.Enum(name, memberNames.Select(n => new EnumMember(n)));

        public static ICodeWriter Assign(this ICodeWriter io, string identifier, string rhs, VariableScope scope = VariableScope.None) => io.Assign(w => w.Append(identifier), w => w.Append(rhs), scope);

        public static ICodeWriter Assign(this ICodeWriter io, string identifier, Action<ICodeWriter> lhs, VariableScope scope = VariableScope.None) => io.Assign(w => w.Append(identifier), lhs, scope);

        public static ICodeWriter Assign(this ICodeWriter io, Action<ICodeWriter> lhs, Action<ICodeWriter> rhs, VariableScope scope = VariableScope.None)
        {
            var prefix = scope switch
            {
                VariableScope.Local => "var ",
                VariableScope.Static => "static ",
                _ => string.Empty
            };
            return io.Append(prefix).Apply(lhs).Append(" = ").Apply(rhs).Line(";");
        }

        public static ICodeWriter Method(this ICodeWriter io, IEnumerable<string> paramNames, Action<ICodeWriter> body) => io.Append("function(").AppendJoin(paramNames).Line(")").Block(body);

        public static ICodeWriter Method(this ICodeWriter io, Action<ICodeWriter> body) => io.Method([], body);

        public static ICodeWriter Function(this ICodeWriter io, string name, IEnumerable<string> parameters, Action<ICodeWriter> body)
        {
            io.Append($"function {name}(").AppendJoin(parameters).Line(")")
              .Block(body, trailingNewLine: true);
            return io;
        }

        public static ICodeWriter Struct(this ICodeWriter io, string name, IEnumerable<string> ctorParams, Action<ICodeWriter> body)
        {
            io.Append($"function {name}(").AppendJoin(ctorParams).Line(") constructor")
              .Block(body, trailingNewLine: true);
            return io;
        }

        public static ICodeWriter Struct(this ICodeWriter io, string name, Action<ICodeWriter> body) => io.Struct(name, [], body);

        private static string Sig(AccessorKind k) => k switch
        {
            AccessorKind.Map => "?",
            AccessorKind.List => "!",
            AccessorKind.Struct => "$",
            _ => string.Empty
        };

        public static ICodeWriter Access(this ICodeWriter io, string identifier, AccessorKind kind, Action<ICodeWriter> exprBuilder)
        {
            io.Append(identifier).Append("[").Append(Sig(kind));

            if (kind is not AccessorKind.Array) io.Append(" ");

            exprBuilder(io);

            io.Append("]");
            return io;
        }

        public static ICodeWriter Access(this ICodeWriter io, string identifier, AccessorKind kind, string exprLiteral) => io.Access(identifier, kind, w => w.Append(exprLiteral));

    }

    public enum VariableScope { None, Local, Static }

    public enum AccessorKind { Array, Map, List, Struct }

    public readonly record struct EnumMember(string Name, string? Value = null, string? Comment = null)
    {
        public string ToMemberString() => Comment is null ? Value is null ? Name : $"{Name} = {Value}" : $"{Name}{(Value is null ? string.Empty : $" = {Value}")} // {Comment}";
    }

}

