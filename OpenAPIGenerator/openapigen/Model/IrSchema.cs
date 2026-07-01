using codegencore.Model;
using openapigen;
using System.Collections.Immutable;

namespace openapigen.Model
{
    public abstract record IrValueSchema
    {
        public sealed record Simple(IrType Type) : IrValueSchema;

        public sealed record OneOf(ImmutableArray<IrValueSchema> Options) : IrValueSchema;
        public sealed record AnyOf(ImmutableArray<IrValueSchema> Options) : IrValueSchema;
        public sealed record AllOf(ImmutableArray<IrValueSchema> Parts) : IrValueSchema;
    }

    public abstract record IrSchema(string Name, IrValueSchema Schema, string? Description)
    {
        public sealed record Struct(
            string Name,
            ImmutableArray<IrField> Fields,
            bool AdditionalPropertiesAllowed,
            IrType? AdditionalPropertiesType,
            string? Description = null) : IrSchema(Name, new IrValueSchema.Simple(new IrType.Named(NamedKind.Struct, Name)), Description); 

        public sealed record Enum(
            string Name,
            IrType Underlying,
            ImmutableArray<string> Literals,
            string? Description = null) : IrSchema(Name, new IrValueSchema.Simple(new IrType.Named(NamedKind.Enum, Name)), Description);

        public sealed record Alias(
            string Name,
            IrValueSchema Target,
            string? Description = null) : IrSchema(Name, Target, Description);

        public sealed record OneOf(
            string Name,
            ImmutableArray<IrValueSchema> Options,
            string? DiscriminatorProperty,
            string? Description = null) : IrSchema(Name, new IrValueSchema.OneOf(Options), Description);

        public sealed record AnyOf(
            string Name,
            ImmutableArray<IrValueSchema> Options,
            string? Description = null) : IrSchema(Name, new IrValueSchema.AnyOf(Options), Description);

        public sealed record AllOf(
            string Name,
            ImmutableArray<IrValueSchema> Parts,
            string? Description = null) : IrSchema(Name, new IrValueSchema.AllOf(Parts), Description);
    }

}
