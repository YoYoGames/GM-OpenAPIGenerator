namespace openapigen.Emitters
{
    /// <summary>
    /// Where a single generated artifact is written. Mirrors one config section.
    /// </summary>
    public sealed class EmitterSettings
    {
        /// <summary>Destination path, resolved relative to the config's output root.</summary>
        public required string OutputFile { get; init; }
    }
}
