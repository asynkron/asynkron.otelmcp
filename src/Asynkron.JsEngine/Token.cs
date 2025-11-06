namespace Asynkron.JsEngine;

internal enum TokenType
{
    LeftParen,
    RightParen,
    LeftBrace,
    RightBrace,
    Comma,
    Semicolon,
    Plus,
    Minus,
    Star,
    Slash,
    Equal,
    EqualEqual,
    Bang,
    BangEqual,
    Greater,
    GreaterEqual,
    Less,
    LessEqual,
    Identifier,
    Number,
    String,
    Let,
    Function,
    Return,
    True,
    False,
    Null,
    Eof
}

internal sealed record Token(TokenType Type, string Lexeme, object? Literal, int Line, int Column)
{
    public override string ToString() => $"{Type} '{Lexeme}'";
}
