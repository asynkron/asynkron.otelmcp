using System;
using System.Collections.Generic;
using System.Linq;
using Asynkron.JsEngine.SExpressions;
using Esprima;
using Esprima.Ast;

namespace Asynkron.JsEngine.Parsing;

/// <summary>
/// Transforms Esprima's JavaScript AST into cons-based S-expressions.
/// </summary>
public static class SExpressionBuilder
{
    public static SExpr BuildProgram(Script script)
    {
        var forms = script.Body.Select(BuildStatement).Where(form => form is not Nil).ToList();
        return forms.Count switch
        {
            0 => Nil.Instance,
            1 => forms[0],
            _ => CreateList("begin", forms)
        };
    }

    private static SExpr BuildStatement(Statement statement) => statement switch
    {
        BlockStatement block => BuildBlock(block),
        ExpressionStatement expressionStatement => BuildExpression(expressionStatement.Expression),
        VariableDeclaration declaration => BuildVariableDeclaration(declaration),
        FunctionDeclaration functionDeclaration => BuildFunctionDeclaration(functionDeclaration),
        IfStatement ifStatement => BuildIf(ifStatement),
        ReturnStatement returnStatement => SExpr.List(new Symbol("return"),
            returnStatement.Argument is null ? Nil.Instance : BuildExpression(returnStatement.Argument)),
        _ => throw new NotSupportedException($"Statement type '{statement.Type}' is not supported by the S-expression builder.")
    };

    private static SExpr BuildBlock(BlockStatement block)
    {
        var forms = block.Body.Select(BuildStatement).Where(form => form is not Nil).ToList();
        return forms.Count switch
        {
            0 => Nil.Instance,
            1 => forms[0],
            _ => CreateList("begin", forms)
        };
    }

    private static SExpr BuildVariableDeclaration(VariableDeclaration declaration)
    {
        var forms = declaration.Declarations.Select(BuildVariableDeclarator).Where(form => form is not Nil).ToList();
        return forms.Count switch
        {
            0 => Nil.Instance,
            1 => forms[0],
            _ => CreateList("begin", forms)
        };
    }

    private static SExpr BuildVariableDeclarator(VariableDeclarator declarator)
    {
        if (declarator.Id is not Identifier identifier)
        {
            throw new NotSupportedException("Only simple identifier variable declarations are supported.");
        }

        var name = identifier.Name ?? throw new NotSupportedException("Unnamed variables are not supported.");
        var init = declarator.Init is null ? Nil.Instance : BuildExpression((Expression)declarator.Init);
        return SExpr.List(new Symbol("let"), new Symbol(name), init);
    }

    private static SExpr BuildFunctionDeclaration(FunctionDeclaration declaration)
    {
        if (declaration.Id is not Identifier identifier)
        {
            throw new NotSupportedException("Function declarations require a named identifier.");
        }

        var name = identifier.Name ?? throw new NotSupportedException("Function declarations require a named identifier.");
        var parameterList = declaration.Params;
        var parameters = BuildParameterList(parameterList);
        if (declaration.Body is not BlockStatement body)
        {
            throw new NotSupportedException("Only block function bodies are supported.");
        }

        var bodyExpr = BuildBlock(body);
        return SExpr.List(new Symbol("function"), new Symbol(name), parameters, bodyExpr);
    }

    private static SExpr BuildIf(IfStatement ifStatement)
    {
        var consequent = BuildStatement(ifStatement.Consequent);
        var alternate = ifStatement.Alternate is null ? Nil.Instance : BuildStatement(ifStatement.Alternate);
        return SExpr.List(
            new Symbol("if"),
            BuildExpression(ifStatement.Test),
            consequent,
            alternate);
    }

    private static SExpr BuildExpression(Expression expression) => expression switch
    {
        Identifier identifier => new Symbol(identifier.Name ?? throw new NotSupportedException("Identifier name missing.")),
        Literal literal => BuildLiteral(literal),
        BinaryExpression binary => BuildBinary(binary),
        AssignmentExpression assignment => BuildAssignment(assignment),
        UnaryExpression unary => BuildUnary(unary),
        CallExpression call => BuildCall(call),
        FunctionExpression functionExpression => BuildFunctionExpression(functionExpression),
        MemberExpression member => BuildMember(member),
        _ => throw new NotSupportedException($"Expression type '{expression.Type}' is not supported by the S-expression builder.")
    };

    private static SExpr BuildLiteral(Literal literal) => literal switch
    {
        Literal { Value: null } => new NullLiteral(),
        Literal { Value: bool value } => new BooleanLiteral(value),
        Literal { Value: double number } => new NumberLiteral(number),
        Literal { Value: int number } => new NumberLiteral(number),
        Literal { Value: long number } => new NumberLiteral(number),
        Literal { Value: string value } => new StringLiteral(value),
        _ => throw new NotSupportedException($"Literal value '{literal.Value}' is not supported by the S-expression builder.")
    };

    private static SExpr BuildBinary(BinaryExpression binary) => SExpr.List(
        new Symbol(GetBinaryOperator(binary.Operator)),
        BuildExpression(binary.Left),
        BuildExpression(binary.Right));

    private static SExpr BuildAssignment(AssignmentExpression assignment)
    {
        if (assignment.Left is not Identifier identifier)
        {
            throw new NotSupportedException("Only simple identifier assignments are supported.");
        }

        var name = identifier.Name ?? throw new NotSupportedException("Assignments require a named identifier.");
        return SExpr.List(new Symbol("set"), new Symbol(name), BuildExpression(assignment.Right));
    }

    private static SExpr BuildUnary(UnaryExpression unary)
    {
        return SExpr.List(
            new Symbol(GetUnaryOperator(unary.Operator)),
            BuildExpression(unary.Argument));
    }

    private static SExpr BuildCall(CallExpression call)
    {
        var argumentList = call.Arguments;
        var arguments = argumentList.Select(argument => argument switch
        {
            SpreadElement => throw new NotSupportedException("Spread arguments are not supported."),
            _ => BuildExpression(argument),
        }).ToList();

        return SExpr.List(new[] { new Symbol("call"), BuildExpression(call.Callee) }.Concat(arguments));
    }

    private static SExpr BuildMember(MemberExpression member)
    {
        if (member.Property is not Identifier identifier)
        {
            throw new NotSupportedException("Only simple identifier member access is supported.");
        }

        var name = identifier.Name ?? throw new NotSupportedException("Member access requires an identifier property.");
        return SExpr.List(new Symbol("member"), BuildExpression(member.Object), new StringLiteral(name));
    }

    private static SExpr BuildFunctionExpression(FunctionExpression function)
    {
        var parameterList = function.Params;
        var parameters = BuildParameterList(parameterList);
        if (function.Body is not BlockStatement body)
        {
            throw new NotSupportedException("Only block function bodies are supported.");
        }

        var bodyExpr = BuildBlock(body);
        return SExpr.List(new Symbol("lambda"), parameters, bodyExpr);
    }

    private static SExpr BuildParameterList(NodeList<Expression> parameters)
    {
        var parameterSymbols = parameters.Select(parameter => parameter switch
        {
            Identifier identifier => (SExpr)new Symbol(identifier.Name ?? throw new NotSupportedException("Parameter name missing.")),
            _ => throw new NotSupportedException("Only simple identifier parameters are supported."),
        });

        return SExpr.List(parameterSymbols);
    }

    private static string GetBinaryOperator(BinaryOperator @operator) => @operator switch
    {
        BinaryOperator.Plus => "+",
        BinaryOperator.Minus => "-",
        BinaryOperator.Times => "*",
        BinaryOperator.Divide => "/",
        BinaryOperator.Modulo => "%",
        BinaryOperator.StrictlyEqual => "===",
        BinaryOperator.StricltyNotEqual => "!==",
        BinaryOperator.Greater => ">",
        BinaryOperator.GreaterOrEqual => ">=",
        BinaryOperator.Less => "<",
        BinaryOperator.LessOrEqual => "<=",
        BinaryOperator.Equal => "==",
        BinaryOperator.NotEqual => "!=",
        BinaryOperator.LogicalAnd => "and",
        BinaryOperator.LogicalOr => "or",
        BinaryOperator.NullishCoalescing => "??",
        _ => throw new NotSupportedException($"Binary operator '{@operator}' is not supported.")
    };

    private static string GetUnaryOperator(UnaryOperator @operator) => @operator switch
    {
        UnaryOperator.Plus => "u+",
        UnaryOperator.Minus => "u-",
        UnaryOperator.LogicalNot => "not",
        _ => throw new NotSupportedException($"Unary operator '{@operator}' is not supported.")
    };

    private static SExpr CreateList(string head, IEnumerable<SExpr> items)
        => SExpr.List(new[] { new Symbol(head) }.Concat(items));
}
