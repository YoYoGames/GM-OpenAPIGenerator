using codegencore.Writers;
using codegencore.Writers.Concrete;
using codegencore.Writers.Lang;
using System.Text;

namespace openapigen.Utils
{
    /// <summary>
    /// Helpers for writing generated files, creating parent directories as needed.
    /// </summary>
    public static class FileEmitHelpers
    {
        /// <summary>Writes a file using a language-specific writer.</summary>
        public static void WriteFile<TWriter>(
            string fullPath,
            Func<ICodeWriter, TWriter> writerFactory,
            Action<TWriter> emit,
            bool emitUtf8Bom = false)
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var tw = new StreamWriter(
                fullPath,
                append: false,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: emitUtf8Bom));

            var cw = CodeWriter.From(tw, "    ");
            emit(writerFactory(cw));
        }

        /// <summary>Writes a GML file.</summary>
        public static void WriteGml(string fullPath, Action<GmlWriter> emit) =>
            WriteFile(fullPath, cw => new GmlWriter(cw), emit);
    }
}
