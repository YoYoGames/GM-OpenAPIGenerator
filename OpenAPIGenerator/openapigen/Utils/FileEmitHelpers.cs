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
        /// <summary>
        /// Writes a file through a language-specific writer, via a temp file in the same directory.
        /// Emitter failure is an expected outcome (exit 30) and root often points straight at a .yyp
        /// tree, so writing the destination directly would leave a truncated file where a working
        /// one was.
        /// </summary>
        public static void WriteFile<TWriter>(
            string fullPath,
            Func<ICodeWriter, TWriter> writerFactory,
            Action<TWriter> emit,
            bool emitUtf8Bom = false)
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var tempPath = fullPath + ".tmp";

            try
            {
                using (var tw = new StreamWriter(
                    tempPath,
                    append: false,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: emitUtf8Bom)))
                {
                    var cw = CodeWriter.From(tw, "    ");
                    emit(writerFactory(cw));
                }

                File.Move(tempPath, fullPath, overwrite: true);
            }
            catch
            {
                // A stack overflow cannot be caught, so this will not always run - but the
                // destination is never opened, which is what actually protects it.
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                throw;
            }
        }

        /// <summary>Writes a GML file.</summary>
        public static void WriteGml(string fullPath, Action<GmlWriter> emit) =>
            WriteFile(fullPath, cw => new GmlWriter(cw), emit);
    }
}
