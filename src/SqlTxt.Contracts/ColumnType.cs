namespace SqlTxt.Contracts;

/// <summary>
/// Supported column data types for Phase 1 fixed-width storage.
/// </summary>
public enum ColumnType
{
    /// <summary>Fixed-width character string of specified length.</summary>
    Char,

    /// <summary>32-bit integer.</summary>
    Int,

    /// <summary>8-bit integer.</summary>
    TinyInt,

    /// <summary>64-bit integer.</summary>
    BigInt,

    /// <summary>Boolean stored as "1" or "0".</summary>
    Bit,

    /// <summary>Decimal with precision and scale.</summary>
    Decimal,
}
