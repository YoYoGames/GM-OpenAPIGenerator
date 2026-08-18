using openapigen.Utils;

namespace openapigen.Emitters
{
    /// <summary>
    /// Resolves an emitter's configured output file against the run's output root.
    /// </summary>
    internal sealed class EmitterLayout
    {
        /// <summary>Absolute path of the file to write.</summary>
        public string FullPath { get; }

        public EmitterLayout(string root, EmitterSettings settings)
        {
            FullPath = settings.OutputFile.ResolvePath(root);
        }
    }
}
