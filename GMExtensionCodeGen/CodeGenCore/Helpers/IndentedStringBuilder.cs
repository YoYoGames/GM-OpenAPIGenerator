using System.Text;

namespace CodeGenCore.Helpers
{
    /// <summary>
    /// Use this class to build strings with indentation.
    /// It mimics the behavior of Microsoft.EntityFrameworkCore.Internal.IndentedStringBuilder
    /// without requiring the Entity Framework dependency.
    /// </summary>
    public sealed class IndentedStringBuilder
    {
        private readonly StringBuilder _sb = new();
        private readonly string _indentToken;
        private int _indentLevel;
        private bool _tabsPending = true;   // are we starting a new line?

        /// <summary>
        /// Gets the current indentation level count.
        /// </summary>
        public int IndentCount { get { return _indentLevel; } }

        /// <summary>
        /// Initializes a new instance of <see cref="IndentedStringBuilder"/>.
        /// Specify the string used for indentation (default is tab).
        /// </summary>
        /// <param name="indentToken">The token we use for one indentation level (default: "\t").</param>
        /// <exception cref="ArgumentException">Thrown when the indent token is null or empty.</exception>
        public IndentedStringBuilder(string indentToken = "\t")
        {
            if (string.IsNullOrEmpty(indentToken))
                throw new ArgumentException("Indent token cannot be null or empty.", nameof(indentToken));

            _indentToken = indentToken;
        }

        /// <summary>
        /// Increase the indentation level by one.
        /// </summary>
        public IndentedStringBuilder IncrementIndent()
        {
            _indentLevel++;
            return this;
        }

        /// <summary>
        /// Decrease the indentation level by one if it is greater than zero.
        /// </summary>
        public IndentedStringBuilder DecrementIndent()
        {
            if (_indentLevel > 0)
                _indentLevel--;
            return this;
        }

        /// <summary>
        /// Append text at the current indentation level.
        /// </summary>
        /// <param name="value">The string to be append.</param>

        public IndentedStringBuilder Append(string? value)
        {
            ApplyIndentIfNeeded();
            _sb.Append(value);
            return this;
        }

        /// <summary>
        /// Append a new line and mark that the next append starts a new line.
        /// </summary>
        public IndentedStringBuilder AppendLine()
        {
            _sb.AppendLine();
            _tabsPending = true;
            return this;
        }

        /// <summary>
        /// Append text followed by a new line at the current indentation level.
        /// </summary>
        /// <param name="value">The string to be appended before the line break.</param>
        public IndentedStringBuilder AppendLine(string? value)
        {
            ApplyIndentIfNeeded();
            _sb.AppendLine(value);
            _tabsPending = true;
            return this;
        }

        /// <summary>
        /// Append a collection of strings joined by the specified separator.
        /// </summary>
        /// <param name="parts">The sequence of strings to join and append.</param>
        /// <param name="separator">The separator to be used between parts.</param>
        public IndentedStringBuilder AppendJoin(IEnumerable<string> parts, string separator)
        {
            return Append(string.Join(separator, parts));
        }

        /// <summary>
        /// Clear the internal buffer and reset indentation.
        /// </summary>
        public IndentedStringBuilder Clear()
        {
            _sb.Clear();
            _indentLevel = 0;
            _tabsPending = true;
            return this;
        }

        /// <summary>
        /// Gets the current length of the builder.
        /// </summary>
        public int Length => _sb.Length;

        /// <summary>
        /// Returns the full string built so far.
        /// </summary>
        public override string ToString() => _sb.ToString();

        /// <summary>
        /// Apply the indentation if we are at the start of a line.
        /// </summary>
        private void ApplyIndentIfNeeded()
        {
            if (!_tabsPending)
                return;

            if (_indentLevel > 0)
                _sb.Append(string.Concat(Enumerable.Repeat(_indentToken, _indentLevel)));

            _tabsPending = false;
        }
    }

}
