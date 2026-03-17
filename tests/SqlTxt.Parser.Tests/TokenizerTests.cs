using SqlTxt.Parser;

namespace SqlTxt.Parser.Tests;

public class TokenizerTests
{
    [Fact]
    public void Tokenize_Keywords_ReturnsKeywordTokens()
    {
        var t = new Tokenizer("CREATE TABLE");
        var tokens = t.ReadAll().ToList();
        Assert.Equal(2, tokens.Count);
        Assert.Equal(TokenType.Keyword, tokens[0].Type);
        Assert.Equal("CREATE", tokens[0].Value);
        Assert.Equal(TokenType.Keyword, tokens[1].Type);
        Assert.Equal("TABLE", tokens[1].Value);
    }

    [Fact]
    public void Tokenize_Identifier_ReturnsIdentifierToken()
    {
        var t = new Tokenizer("Users");
        var tokens = t.ReadAll().ToList();
        Assert.Single(tokens);
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal("Users", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_StringLiteral_ReturnsStringToken()
    {
        var t = new Tokenizer("'hello'");
        var tokens = t.ReadAll().ToList();
        Assert.Single(tokens);
        Assert.Equal(TokenType.StringLiteral, tokens[0].Type);
        Assert.Equal("hello", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_CharType_ReturnsCorrectTokens()
    {
        var t = new Tokenizer("CHAR(10)");
        var tokens = t.ReadAll().ToList();
        Assert.Equal(4, tokens.Count);
        Assert.Equal("CHAR", tokens[0].Value);
        Assert.Equal(TokenType.LeftParen, tokens[1].Type);
        Assert.Equal("10", tokens[2].Value);
        Assert.Equal(TokenType.RightParen, tokens[3].Type);
    }

    [Fact]
    public void Tokenize_SkipsComments()
    {
        var t = new Tokenizer("-- comment\nCREATE TABLE");
        var tokens = t.ReadAll().ToList();
        Assert.Equal(2, tokens.Count);
        Assert.Equal("CREATE", tokens[0].Value);
        Assert.Equal("TABLE", tokens[1].Value);
    }
}
