using CodeGenCore.Writers.CoreExtensions;

namespace CodeGenCore.Writers.Lang.CStyle
{
    /// <summary>
    /// Fluent helpers that emit <em>keyword + parenthesis + brace-block</em>
    /// constructs shared by most curly-brace languages (C, C++, C#, Java,
    /// JavaScript, GML, GLSL …).
    /// </summary>
    public static class CStyleWriterExtensions
    {
        /// <summary>
        /// Generic helper for statements that look like
        /// <c>keyword (expr) { … }</c>.
        /// </summary>
        public static ICodeWriter Keyword(this ICodeWriter io, string keyword, string parenExpr, Action<ICodeWriter> body) => io.Line($"{keyword} ({parenExpr})").Block(body).Line();

        /// <summary> <c>if (condition) { … }</c> with optional <c>else</c>. </summary>
        public static ICodeWriter If(this ICodeWriter io, string condition, Action<ICodeWriter> thenBody, Action<ICodeWriter>? elseBody = null)
        {
            io.Keyword("if", condition, thenBody);

            if (elseBody is not null)
                io.Line("else")
                  .Block(elseBody);

            return io;
        }

        public static ICodeWriter For(this ICodeWriter io, string init, string cond, string step, Action<ICodeWriter> body) => io.Keyword("for", $"{init}; {cond}; {step}", body);

        public static ICodeWriter While(this ICodeWriter io, string condition, Action<ICodeWriter> body) => io.Keyword("while", condition, body);

        public static ICodeWriter DoWhile(this ICodeWriter io, Action<ICodeWriter> body, string condition) => io.Line("do").Block(body).Line($" while ({condition});");

        public static ICodeWriter Switch(this ICodeWriter io, string expr, Action<SwitchBuilder> configureBody) => io.Keyword("switch", expr, body => configureBody(new SwitchBuilder(body)));

        /// <summary>
        /// Fluent builder returned by <see cref="Switch"/> for adding
        /// <c>case</c>/<c>default</c> sections.
        /// </summary>
        public readonly struct SwitchBuilder
        {
            private readonly ICodeWriter _w;
            internal SwitchBuilder(ICodeWriter w) => _w = w;

            public SwitchBuilder Case(string label, Action<ICodeWriter> body, bool addBreak = true)
            {
                _w.Line($"case {label}:")
                  .IncrementIndent()
                  .Apply(body)
                  .When(addBreak, w => w.Line("break;"))
                  .DecrementIndent();
                return this;
            }

            public SwitchBuilder Case(IEnumerable<string> labels, Action<ICodeWriter> body, bool addBreak = true)
            {
                foreach (var lab in labels)
                    _w.Line($"case {lab}:");
                _w.IncrementIndent()
                  .Apply(body)
                  .When(addBreak, w => w.Line("break;"))
                  .DecrementIndent();
                return this;
            }

            public SwitchBuilder Default(Action<ICodeWriter> body, bool addBreak = true)
            {
                _w.Line("default:")
                  .IncrementIndent()
                  .Apply(body)
                  .When(addBreak, w => w.Line("break;"))
                  .DecrementIndent();
                return this;
            }
        }

        private static ICodeWriter EmitArgs(this ICodeWriter io, IEnumerable<Action<ICodeWriter>> builders) => io.Append("(").ForEach(builders, (w, b) => b(w), ", ").Append(")");

        private static ICodeWriter EmitArgs(this ICodeWriter io, IEnumerable<string> argLiterals) => io.Append("(").AppendJoin(argLiterals).Append(")");

        public static ICodeWriter Call(this ICodeWriter io, string functionName, params Action<ICodeWriter>[] argBuilders) => io.Append(functionName).EmitArgs(argBuilders);

        public static ICodeWriter Call(this ICodeWriter io, string functionName, params string[] argLiterals) => io.Append(functionName).EmitArgs(argLiterals);

        public static ICodeWriter Call(this ICodeWriter io, string functionName, IEnumerable<string> argLiterals) => io.Call(functionName, argLiterals.ToArray());

        public static ICodeWriter Return(this ICodeWriter io, string expression) => io.Line($"return {expression};");

        public static ICodeWriter Return(this ICodeWriter io, Action<ICodeWriter> expression) => io.Append($"return ").Apply(expression).Line(";");

        /// <summary>
        /// Emits <c>// …</c> comments.  Handles multi-line strings automatically.
        /// </summary>
        public static ICodeWriter Comment(this ICodeWriter io, string comment)
        {
            if (string.IsNullOrEmpty(comment))
                return io.Line("//");

            foreach (var line in comment.Split(["\r\n", "\n"], StringSplitOptions.None))
            {
                io.Line($"// {line}");
            }

            return io;
        }

        public static ICodeWriter Section(this ICodeWriter io, string name)
        {
            if (string.IsNullOrEmpty(name))
                return io.Line("//");

            io.Comment($"""
                #####################################################################
                # {name}
                #####################################################################
                """);

            return io;
        }
    }

}

