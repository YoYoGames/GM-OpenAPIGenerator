using CodeGenCore.Writers.CoreExtensions;

namespace CodeGenCore.Writers.Lang.Cpp
{
    /// <summary>
    /// Fluent helpers for emitting modern C++ (C++17/20) code.  Everything in this
    /// file is C++-specific; generic control-flow constructs live in
    /// <see cref="Writer.CStyle.CStyleWriterExtensions"/>.
    /// </summary>
    public static class CppWriterExtensions
    {
        public static ICodeWriter Include(this ICodeWriter io, string header, bool system = true) => io.Line(system ? $"#include <{header}>" : $"#include \"{header}\"");

        public static ICodeWriter UsingNamespace(this ICodeWriter io, string ns) => io.Line($"using namespace {ns};");

        public static ICodeWriter Namespace(this ICodeWriter io, string fullyQualified, Action<ICodeWriter> body)
        {
            var parts = fullyQualified.Split("::", StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
                io.AppendLine($"namespace {p}").Block(body, true);
            io.Line();                // blank line after namespace
            return io;
        }

        private static string ToLiteral(this CppLinkage l) => l switch
        {
            CppLinkage.C => "C",
            CppLinkage.CPP => "C++",
            _ => throw new ArgumentOutOfRangeException(nameof(l))
        };

        /// <summary>
        /// Wraps <paramref name="body"/> in an <c>extern "&lt;linkage&gt;" { … }</c>
        /// block.  The linkage is chosen from the <see cref="CppLinkage"/> enum so no
        /// magic strings can slip in.
        /// </summary>
        public static ICodeWriter Extern(this ICodeWriter io, CppLinkage linkage, Action<ICodeWriter> expr) => io.Append($"extern \"{linkage.ToLiteral()}\" ").Apply(expr);
        public static ICodeWriter ExternBlock(this ICodeWriter io, CppLinkage linkage, Action<ICodeWriter> body) => io.Line($"extern \"{linkage.ToLiteral()}\"").Block(body, trailingNewLine: true);

        public static ICodeWriter Assign(this ICodeWriter io, string identifier, string rhs, string? type = null) => io.Assign(w => w.Append(identifier), w => w.Append(rhs), type);

        public static ICodeWriter Assign(this ICodeWriter io, string identifier, Action<ICodeWriter> lhs, string? type = null) => io.Assign(w => w.Append(identifier), lhs, type);

        public static ICodeWriter Assign(this ICodeWriter io, Action<ICodeWriter> lhs, string expr, string? type = null) => io.Assign(lhs, w => w.Append(expr), type);

        public static ICodeWriter Assign(this ICodeWriter io, Action<ICodeWriter> lhs, Action<ICodeWriter> rhs, string? type = null) => io.When(!string.IsNullOrEmpty(type), c => c.Append($"{type} ")).Apply(lhs).Append(" = ").Apply(rhs).Line(";");


        /// <summary>
        /// Unified declaration / assignment helper.<br/>
        /// Examples:<br/>
        /// <list type="bullet">
        /// <item><c>io.Declare("int", "count");</c></item>
        /// <item><c>io.Declare("auto", "ptr", "*foo", initWithEq:false);</c></item>
        /// <item><c>io.Declare("std::vector&lt;T&gt;", "v", "{}", isConst:true);</c></item>
        /// </list>
        /// </summary>
        public static ICodeWriter Declare(
            this ICodeWriter io,
            string cppType,          // pass "" to omit type (assignment only)
            string identifier,
            string? initializer = null,
            bool isConst = false,
            bool isStatic = false,
            bool initWithEq = true,   // false -> use brace / paren init strings as-is
            bool endWithSemi = true)
        {
            var spec = (isStatic ? "static " : string.Empty) +
                         (isConst ? "const " : string.Empty);

            if (string.IsNullOrEmpty(cppType) && spec.Length > 0)
                throw new ArgumentException("Cannot apply const/static without a type.");

            io.Append($"{spec}{cppType}".Trim())      // Trim handles empty type
              .When(cppType.Length > 0 && cppType[^1] != ' ', w => w.Append(" "))
              .Append(identifier);

            if (initializer is not null)
            {
                io.Append(initWithEq ? " = " : " ")
                  .Append(initializer);
            }

            if (endWithSemi) io.Line(";");
            else io.Append(" ");

            return io;
        }

        /// <summary>
        /// Emits a free-standing function <em>definition</em> when you already have
        /// the parameter list as a raw C++ literal.
        /// </summary>
        public static ICodeWriter Function(this ICodeWriter io, string name, string rawParamList, Action<ICodeWriter> body, string? returnType = null, IEnumerable<string>? qualifiers = null)
        {
            var suffix = qualifiers is null ? string.Empty : $" {string.Join(" ", qualifiers)}";

            io.Line($"{returnType ?? "void"} {name}({rawParamList}){suffix}")
              .Block(body, trailingNewLine: true);

            return io;
        }

        /// <summary>
        /// Emits a free function <em>definition</em>.  Pass <c>null</c> to
        /// <paramref name="returnType"/> for <c>void</c>.
        /// </summary>
        public static ICodeWriter Function(this ICodeWriter io, string name, IEnumerable<Param> parameters, Action<ICodeWriter> body, string? returnType = null, IEnumerable<string>? qualifiers = null)
        {
            var paramList = string.Join(", ",
                parameters.Select(p => $"{p.CppType} {p.Name}"));

            return io.Function(name, paramList, body, returnType, qualifiers);
        }

        /// <summary>
        /// Emits a <em>prototype</em> (no body, ends with semicolon).
        /// </summary>
        public static ICodeWriter FunctionDecl(this ICodeWriter io, string name, IEnumerable<Param> parameters, string? returnType = null, IEnumerable<string>? qualifiers = null)
        {
            var paramList = string.Join(", ",
                parameters.Select(p => $"{p.CppType} {p.Name}"));

            var suffix = qualifiers is null ? string.Empty
                         : " " + string.Join(" ", qualifiers);

            io.Line($"{returnType ?? "void"} {name}({paramList}){suffix};");
            return io;
        }

        public static ICodeWriter Struct(this ICodeWriter io, string name, Action<ICodeWriter> body) => io.AppendLine($"struct {name}").Block(body).Line(";");
            
        /// <summary>
        /// Appends a C++ brace-initializer list e.g. <c>{1, 2, 3}</c>.
        /// </summary>
        public static ICodeWriter InitList(this ICodeWriter io, IEnumerable<string> items) => io.Append("{").AppendJoin(items).Append("}");
    }


    /// <summary>
    /// Well-known linkage names for <c>extern "..."</c>.
    /// </summary>
    public enum CppLinkage
    {
        C,
        CPP
    }

    /// <summary>Lightweight parameter record used by the new overloads.</summary>
    public readonly record struct Param(string CppType, string Name);

}

