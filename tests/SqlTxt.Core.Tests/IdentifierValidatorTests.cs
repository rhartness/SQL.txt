using SqlTxt.Contracts.Exceptions;
using SqlTxt.Core;

namespace SqlTxt.Core.Tests;

public class IdentifierValidatorTests
{
    [Fact]
    public void ValidateTableName_Valid_DoesNotThrow()
    {
        IdentifierValidator.ValidateTableName("User");
        IdentifierValidator.ValidateTableName("PageContent");
        IdentifierValidator.ValidateTableName("My_Table_123");
    }

    [Fact]
    public void ValidateTableName_StartsWithDigit_Throws()
    {
        var ex = Assert.Throws<ValidationException>(() => IdentifierValidator.ValidateTableName("123abc"));
        Assert.Contains("cannot start with a digit", ex.Message);
    }

    [Fact]
    public void ValidateTableName_ReservedKeyword_Throws()
    {
        var ex = Assert.Throws<ValidationException>(() => IdentifierValidator.ValidateTableName("SELECT"));
        Assert.Contains("reserved keyword", ex.Message);
    }

    [Fact]
    public void ValidateTableName_ContainsSpace_Throws()
    {
        var ex = Assert.Throws<ValidationException>(() => IdentifierValidator.ValidateTableName("My Table"));
        Assert.Contains("space", ex.Message);
    }

    [Fact]
    public void ValidateColumnName_WithBrackets_AllowsSpaces()
    {
        IdentifierValidator.ValidateColumnName("My Column", fromBrackets: true);
    }

    [Fact]
    public void ValidateColumnName_WithoutBrackets_RejectsSpaces()
    {
        var ex = Assert.Throws<ValidationException>(() => IdentifierValidator.ValidateColumnName("My Column", fromBrackets: false));
        Assert.Contains("space", ex.Message);
    }
}
