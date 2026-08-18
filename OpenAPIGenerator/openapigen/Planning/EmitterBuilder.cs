using openapigen.Emitters;
using openapigen.Emitters.Controller;
using openapigen.Emitters.Docs;
using openapigen.Emitters.Gml;
using openapigen.Helpers;
using openapigen.Mapping;

namespace openapigen.Planning
{
    /// <summary>
    /// Turns a resolved config into the ordered set of emitters to run.
    /// </summary>
    public static class EmitterBuilder
    {
        public static List<(string Key, IIrEmitter Emitter)> Build(ResolvedConfig rc)
        {
            var naming = new GmlNaming(rc.Prefix);
            var emitters = new List<(string, IIrEmitter)>();

            var code = rc.Raw.Code;
            if (code.Schemas is { Enabled: true } schemas)
                emitters.Add(("schemas", new SchemasEmitter(schemas.ToSettings(), naming)));

            if (code.EndPoints is { Enabled: true } endPoints)
                emitters.Add(("endPoints", new EndpointsEmitter(endPoints.ToSettings(), naming)));

            if (code.Helpers is { Enabled: true } helpers)
                emitters.Add(("helpers", new HelpersEmitter(helpers.ToSettings(), naming)));

            var controller = rc.Raw.Controller;
            if (controller.CreateEvent is { Enabled: true } create)
                emitters.Add(("controller.createEvent", new ControllerCreateEmitter(create.ToSettings(), naming)));

            if (controller.CleanupEvent is { Enabled: true } cleanup)
                emitters.Add(("controller.cleanupEvent", new ControllerCleanupEmitter(cleanup.ToSettings(), naming)));

            if (controller.HttpAsyncEvent is { Enabled: true } http)
                emitters.Add(("controller.httpAsyncEvent", new ControllerHttpEmitter(http.ToSettings(), naming)));

            var docs = rc.Raw.Docs;
            if (docs.Schemas is { Enabled: true } docSchemas)
                emitters.Add(("docs.schemas", new DocsSchemasEmitter(docSchemas.ToSettings(), naming)));

            if (docs.Functions is { Enabled: true } docFunctions)
                emitters.Add(("docs.functions", new DocsFunctionsEmitter(docFunctions.ToSettings(), naming)));

            return emitters;
        }
    }
}
