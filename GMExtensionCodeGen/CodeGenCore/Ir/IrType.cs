using CodeGenCore.Helpers;
using System.Collections.Immutable;

namespace CodeGenCore.Ir
{
    public sealed record IrType(
        IrTypeKind Kind,
        string Name,
        bool IsCollection = false,
        int? FixedLength = null,
        bool IsNullable = false,
        ImmutableArray<IrType>? Variants = null,
        ImmutableArray<string>? EnumLiterals = null)
    {
        public static readonly IrType Void = new(IrTypeKind.Void, "void");
        public static readonly IrType String = new (IrTypeKind.Scalar, "string");
        public static readonly IrType AnyArray = new(IrTypeKind.AnyArray, "any");
        public static readonly IrType AnyMap = new(IrTypeKind.AnyMap, "any");
        public static readonly IrType Function = new(IrTypeKind.Function, "function");

        //  GameMaker passes *numbers* as double. A uint64 cannot be represented
        //  exactly, so we exclude it from the fast‑path.
        private static bool IsDoubleFriendly(string scalar)
            => ScalarTypes.IsNumeric(scalar) && !scalar.Equals("uint64", StringComparison.OrdinalIgnoreCase);

        public bool IsNumericScalar => Kind == IrTypeKind.Scalar && IsDoubleFriendly(Name) && !IsCollection;
        public bool IsStringScalar => Kind == IrTypeKind.Scalar && Name.Equals("string", StringComparison.OrdinalIgnoreCase) && !IsCollection;

        public bool IsAggregate => Kind is IrTypeKind.Struct or IrTypeKind.Variant or IrTypeKind.AnyArray or IrTypeKind.AnyMap || IsCollection;

        public bool IsVariant => Kind == IrTypeKind.Variant && Name.Equals("variant", StringComparison.OrdinalIgnoreCase);

        public bool IsEnum => EnumLiterals is { Length: > 0 };

        public bool IsDirectGameMakerArg => IsNumericScalar || IsStringScalar;

        public static IrType CreateVariant(IEnumerable<IrType> alternatives,
                                           bool isCollection = false,
                                           int? fixedLength = null,
                                           bool isNullable = false)
            => new(IrTypeKind.Variant, "variant", isCollection, fixedLength, isNullable, alternatives.ToImmutableArray());
    }

}
