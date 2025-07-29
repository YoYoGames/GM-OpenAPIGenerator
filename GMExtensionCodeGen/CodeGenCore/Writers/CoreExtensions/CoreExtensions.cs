namespace CodeGenCore.Writers.CoreExtensions
{
    /// <summary>
    /// Pure helpers that work for <em>any</em> curly-brace / indentation-based
    /// language.  They never emit language keywords; they only control whitespace,
    /// indentation, repetition, and conditional emission.
    /// <para>
    /// Keep this file pristine: if a helper needs to write <c>switch</c>, <c>enum</c>
    /// or any other keyword, move it to the C-style or language-specific layer.
    /// </para>
    /// </summary>
    public static class CoreExtensions
    {
        /// <summary>
        /// Runs <paramref name="configure"/> against <paramref name="io"/> then
        /// returns <paramref name="io"/>.<br/>
        /// Used to splice inline lambdas into a fluent chain.
        /// </summary>
        public static ICodeWriter Apply(this ICodeWriter io, Action<ICodeWriter> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            configure(io);
            return io;
        }

        /// <summary>Shortcut for <c>AppendLine(code)</c>.</summary>
        public static ICodeWriter Line(this ICodeWriter io, string code = "") => io.AppendLine(code);

        public static ICodeWriter Lines(this ICodeWriter io, string code = "")
        {
            var lines = code.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
            foreach (var line in lines)
            {
                io.Line(line);
            }
            return io;
        }

        /// <summary>
        /// Conditionally executes <paramref name="configure"/> without breaking the
        /// fluent chain.
        /// </summary>
        public static ICodeWriter When(this ICodeWriter io, bool condition, Action<ICodeWriter> configure) => condition ? io.Apply(configure) : io;

        /// <summary>
        /// Repeats <paramref name="emit"/> for every <typeparamref name="T"/> in
        /// <paramref name="items"/>.  If <paramref name="separator"/> is supplied,
        /// it is written <em>between</em> items (not after the last one).
        /// </summary>
        public static ICodeWriter ForEach<T>(this ICodeWriter io, IEnumerable<T> items, Action<ICodeWriter, T> emit, string? separator = null)
        {
            bool first = true;
            foreach (var item in items)
            {
                if (separator is not null && !first)
                    io.Append(separator);
                emit(io, item);
                first = false;
            }
            return io;
        }

        /// <summary>
        /// Emits an opening brace <c>{</c>, runs <paramref name="body"/> at
        /// <paramref name="io"/>'s deeper indent, emits the closing brace
        /// <c>}</c>.  If <paramref name="trailingNewLine"/> is <see langword="true"/>
        /// a newline is appended after the closing brace (handy for C or C++
        /// function definitions).
        /// </summary>
        public static ICodeWriter Block(this ICodeWriter io, Action<ICodeWriter> body, bool trailingNewLine = false)
        {
            io.AppendLine("{")
              .IncrementIndent()
              .Apply(body)
              .DecrementIndent()
              .Append("}");

            if (trailingNewLine) io.AppendLine();
            return io;
        }
    }

}

