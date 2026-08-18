using openapigen.Emitters;
using openapigen.Models.Config;

namespace openapigen.Mapping
{
    /// <summary>
    /// Single source of truth for config -> emitter settings mapping.
    /// </summary>
    public static class EmitterSettingsMappers
    {
        public static EmitterSettings ToSettings(this IGeneratorConfig cfg)
            => new() { OutputFile = cfg.OutputFile };
    }
}
