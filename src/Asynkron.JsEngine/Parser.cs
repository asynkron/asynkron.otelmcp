namespace Asynkron.JsEngine;

internal sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _current;

    public Parser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    public Cons ParseProgram()
    {
        var statements = new List<object?> { JsSymbols.Program };
        while (!Check(TokenType.Eof))
        {
            statements.Add(ParseDeclaration());
        }

        return Cons.FromEnumerable(statements);
    }

    private object ParseDeclaration()
    {
        if (Match(TokenType.Function))
        {
            return ParseFunctionDeclaration();
        }

        if (Match(TokenType.Class))
        {
            return ParseClassDeclaration();
        }

        if (Match(TokenType.Let))
        {
            return ParseVariableDeclaration();
        }

        return ParseStatement();
    }

    private object ParseFunctionDeclaration()
    {
        var nameToken = Consume(TokenType.Identifier, "Expected function name.");
        var name = Symbol.Intern(nameToken.Lexeme);
        Consume(TokenType.LeftParen, "Expected '(' after function name.");
        var parameters = ParseParameterList();
        Consume(TokenType.RightParen, "Expected ')' after function parameters.");
        var body = ParseBlock();

        return Cons.FromEnumerable(new object?[] { JsSymbols.Function, name, parameters, body });
    }

    private object ParseClassDeclaration()
    {
        var nameToken = Consume(TokenType.Identifier, "Expected class name.");
        var name = Symbol.Intern(nameToken.Lexeme);
        Consume(TokenType.LeftBrace, "Expected '{' after class name.");

        Cons? constructor = null;
        var methods = new List<object?>();

        while (!Check(TokenType.RightBrace))
        {
            var methodNameToken = Consume(TokenType.Identifier, "Expected method name in class body.");
            var methodName = methodNameToken.Lexeme;
            Consume(TokenType.LeftParen, "Expected '(' after method name.");
            var parameters = ParseParameterList();
            Consume(TokenType.RightParen, "Expected ')' after method parameters.");
            var body = ParseBlock();

            var lambdaName = string.Equals(methodName, "constructor", StringComparison.Ordinal)
                ? name
                : null;
            var lambda = Cons.FromEnumerable(new object?[] { JsSymbols.Lambda, lambdaName, parameters, body });

            if (string.Equals(methodName, "constructor", StringComparison.Ordinal))
            {
                if (constructor is not null)
                {
                    throw new ParseException("Class cannot declare multiple constructors.");
                }

                constructor = lambda;
            }
            else
            {
                methods.Add(Cons.FromEnumerable(new object?[] { JsSymbols.Method, methodName, lambda }));
            }
        }

        Consume(TokenType.RightBrace, "Expected '}' after class body.");
        Match(TokenType.Semicolon); // allow optional semicolon terminator

        constructor ??= CreateDefaultConstructor(name);
        var methodList = Cons.FromEnumerable(methods);

        return Cons.FromEnumerable(new object?[] { JsSymbols.Class, name, constructor, methodList });
    }

    private Cons ParseParameterList()
    {
        var parameters = new List<object?>();
        if (!Check(TokenType.RightParen))
        {
            do
            {
                var identifier = Consume(TokenType.Identifier, "Expected parameter name.");
                parameters.Add(Symbol.Intern(identifier.Lexeme));
            } while (Match(TokenType.Comma));
        }

        return Cons.FromEnumerable(parameters);
    }

    private object ParseVariableDeclaration()
    {
        var nameToken = Consume(TokenType.Identifier, "Expected variable name after 'let'.");
        var name = Symbol.Intern(nameToken.Lexeme);
        Consume(TokenType.Equal, "Expected '=' after variable name.");
        var initializer = ParseExpression();
        Consume(TokenType.Semicolon, "Expected ';' after variable declaration.");
        return Cons.FromEnumerable(new object?[] { JsSymbols.Let, name, initializer });
    }

    private object ParseStatement()
    {
        if (Match(TokenType.Return))
        {
            return ParseReturnStatement();
        }

        if (Match(TokenType.LeftBrace))
        {
            return ParseBlock(leftBraceConsumed: true);
        }

        return ParseExpressionStatement();
    }

    private object ParseReturnStatement()
    {
        object? value = null;
        var hasValue = false;
        if (!Check(TokenType.Semicolon))
        {
            value = ParseExpression();
            hasValue = true;
        }

        Consume(TokenType.Semicolon, "Expected ';' after return statement.");
        return hasValue
            ? Cons.FromEnumerable(new object?[] { JsSymbols.Return, value })
            : Cons.FromEnumerable(new object?[] { JsSymbols.Return });
    }

    private object ParseExpressionStatement()
    {
        var expression = ParseExpression();
        Consume(TokenType.Semicolon, "Expected ';' after expression statement.");
        return Cons.FromEnumerable(new object?[] { JsSymbols.ExpressionStatement, expression });
    }

    private Cons ParseBlock(bool leftBraceConsumed = false)
    {
        if (!leftBraceConsumed)
        {
            Consume(TokenType.LeftBrace, "Expected '{' to begin block.");
        }

        var statements = new List<object?> { JsSymbols.Block };
        while (!Check(TokenType.RightBrace) && !Check(TokenType.Eof))
        {
            statements.Add(ParseDeclaration());
        }

        Consume(TokenType.RightBrace, "Expected '}' after block.");
        return Cons.FromEnumerable(statements);
    }

    private object? ParseExpression() => ParseAssignment();

    private object? ParseAssignment()
    {
        var expr = ParseEquality();

        if (Match(TokenType.Equal))
        {
            var equals = Previous();
            var value = ParseAssignment();

            if (expr is Symbol symbol)
            {
                return Cons.FromEnumerable(new object?[] { JsSymbols.Assign, symbol, value });
            }

            if (expr is Cons { Head: Symbol head } assignmentTarget && ReferenceEquals(head, JsSymbols.GetProperty))
            {
                var target = assignmentTarget.Rest.Head;
                var propertyName = assignmentTarget.Rest.Rest.Head;
                return Cons.FromEnumerable(new object?[] { JsSymbols.SetProperty, target, propertyName, value });
            }

            throw new ParseException($"Invalid assignment target near line {equals.Line} column {equals.Column}.");
        }

        return expr;
    }

    private object? ParseEquality()
    {
        var expr = ParseComparison();

        while (Match(TokenType.BangEqual, TokenType.EqualEqual))
        {
            var operatorToken = Previous();
            var right = ParseComparison();
            expr = Cons.FromEnumerable(new object?[]
            {
                JsSymbols.Operator(operatorToken.Type == TokenType.EqualEqual ? "==" : "!="),
                expr,
                right
            });
        }

        return expr;
    }

    private object? ParseComparison()
    {
        var expr = ParseTerm();
        while (Match(TokenType.Greater, TokenType.GreaterEqual, TokenType.Less, TokenType.LessEqual))
        {
            var op = Previous();
            var right = ParseTerm();
            var symbol = op.Type switch
            {
                TokenType.Greater => JsSymbols.Operator(">"),
                TokenType.GreaterEqual => JsSymbols.Operator(">="),
                TokenType.Less => JsSymbols.Operator("<"),
                TokenType.LessEqual => JsSymbols.Operator("<="),
                _ => throw new InvalidOperationException("Unexpected comparison operator.")
            };

            expr = Cons.FromEnumerable(new object?[] { symbol, expr, right });
        }

        return expr;
    }

    private object? ParseTerm()
    {
        var expr = ParseFactor();
        while (Match(TokenType.Plus, TokenType.Minus))
        {
            var op = Previous();
            var right = ParseFactor();
            var symbol = JsSymbols.Operator(op.Type == TokenType.Plus ? "+" : "-");
            expr = Cons.FromEnumerable(new object?[] { symbol, expr, right });
        }

        return expr;
    }

    private object? ParseFactor()
    {
        var expr = ParseUnary();
        while (Match(TokenType.Star, TokenType.Slash))
        {
            var op = Previous();
            var right = ParseUnary();
            var symbol = JsSymbols.Operator(op.Type == TokenType.Star ? "*" : "/");
            expr = Cons.FromEnumerable(new object?[] { symbol, expr, right });
        }

        return expr;
    }

    private object? ParseUnary()
    {
        if (Match(TokenType.Bang))
        {
            return Cons.FromEnumerable(new object?[] { JsSymbols.Not, ParseUnary() });
        }

        if (Match(TokenType.Minus))
        {
            return Cons.FromEnumerable(new object?[] { JsSymbols.Negate, ParseUnary() });
        }

        return ParseCall();
    }

    private object? ParseCall()
    {
        var expr = ParsePrimary();
        while (true)
        {
            if (Match(TokenType.LeftParen))
            {
                expr = FinishCall(expr);
                continue;
            }

            if (Match(TokenType.Dot))
            {
                expr = FinishGet(expr);
                continue;
            }

            break;
        }

        return expr;
    }

    private object FinishCall(object? callee)
    {
        var arguments = ParseArgumentList();
        var items = new List<object?> { JsSymbols.Call, callee };
        items.AddRange(arguments);
        return Cons.FromEnumerable(items);
    }

    private List<object?> ParseArgumentList()
    {
        var arguments = new List<object?>();
        if (!Check(TokenType.RightParen))
        {
            do
            {
                arguments.Add(ParseExpression());
            } while (Match(TokenType.Comma));
        }

        Consume(TokenType.RightParen, "Expected ')' after arguments.");
        return arguments;
    }

    private object? ParsePrimary()
    {
        if (Match(TokenType.New))
        {
            return ParseNewExpression();
        }

        if (Match(TokenType.False))
        {
            return false;
        }

        if (Match(TokenType.True))
        {
            return true;
        }

        if (Match(TokenType.Null))
        {
            return null;
        }

        if (Match(TokenType.Number))
        {
            return Previous().Literal is double number ? number : 0d;
        }

        if (Match(TokenType.String))
        {
            return Previous().Literal as string ?? string.Empty;
        }

        if (Match(TokenType.Identifier))
        {
            return Symbol.Intern(Previous().Lexeme);
        }

        if (Match(TokenType.This))
        {
            return JsSymbols.This;
        }

        if (Match(TokenType.Function))
        {
            return ParseFunctionExpression();
        }

        if (Match(TokenType.LeftBrace))
        {
            return ParseObjectLiteral();
        }

        if (Match(TokenType.LeftParen))
        {
            var expr = ParseExpression();
            Consume(TokenType.RightParen, "Expected ')' after expression.");
            return expr;
        }

        throw new ParseException($"Unexpected token {Peek().Type} at line {Peek().Line} column {Peek().Column}.");
    }

    private object ParseNewExpression()
    {
        var constructor = ParsePrimary();

        while (Match(TokenType.Dot))
        {
            constructor = FinishGet(constructor);
        }

        var arguments = new List<object?>();
        if (Match(TokenType.LeftParen))
        {
            arguments = ParseArgumentList();
        }

        var items = new List<object?> { JsSymbols.New, constructor };
        items.AddRange(arguments);
        return Cons.FromEnumerable(items);
    }

    private object ParseFunctionExpression()
    {
        Symbol? name = null;
        if (Check(TokenType.Identifier))
        {
            name = Symbol.Intern(Advance().Lexeme);
        }

        Consume(TokenType.LeftParen, "Expected '(' after function keyword.");
        var parameters = ParseParameterList();
        Consume(TokenType.RightParen, "Expected ')' after lambda parameters.");
        var body = ParseBlock();
        return Cons.FromEnumerable(new object?[] { JsSymbols.Lambda, name, parameters, body });
    }

    private object ParseObjectLiteral()
    {
        var properties = new List<object?>();
        if (!Check(TokenType.RightBrace))
        {
            do
            {
                var name = ParseObjectPropertyName();
                Consume(TokenType.Colon, "Expected ':' after property name.");
                var value = ParseExpression();
                properties.Add(Cons.FromEnumerable(new object?[] { JsSymbols.Property, name, value }));
            } while (Match(TokenType.Comma));
        }

        Consume(TokenType.RightBrace, "Expected '}' after object literal.");
        var items = new List<object?> { JsSymbols.ObjectLiteral };
        items.AddRange(properties);
        return Cons.FromEnumerable(items);
    }

    private string ParseObjectPropertyName()
    {
        if (Match(TokenType.String))
        {
            return Previous().Literal as string ?? string.Empty;
        }

        var identifier = Consume(TokenType.Identifier, "Expected property name.");
        return identifier.Lexeme;
    }

    private object FinishGet(object? target)
    {
        var nameToken = Consume(TokenType.Identifier, "Expected property name after '.'.");
        var propertyName = nameToken.Lexeme;
        return Cons.FromEnumerable(new object?[] { JsSymbols.GetProperty, target, propertyName });
    }

    private static Cons CreateDefaultConstructor(Symbol name)
    {
        var body = Cons.FromEnumerable(new object?[] { JsSymbols.Block });
        return Cons.FromEnumerable(new object?[] { JsSymbols.Lambda, name, Cons.Empty, body });
    }

    private bool Match(params TokenType[] types)
    {
        foreach (var type in types)
        {
            if (Check(type))
            {
                Advance();
                return true;
            }
        }

        return false;
    }

    private bool Check(TokenType type)
    {
        if (IsAtEnd)
        {
            return type == TokenType.Eof;
        }

        return Peek().Type == type;
    }

    private Token Advance()
    {
        if (!IsAtEnd)
        {
            _current++;
        }

        return Previous();
    }

    private bool IsAtEnd => Peek().Type == TokenType.Eof;

    private Token Peek() => _tokens[_current];

    private Token Previous() => _tokens[_current - 1];

    private Token Consume(TokenType type, string message)
    {
        if (Check(type))
        {
            return Advance();
        }

        throw new ParseException(message);
    }
}
