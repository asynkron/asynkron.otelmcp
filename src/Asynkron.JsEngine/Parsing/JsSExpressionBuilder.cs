using System;
using System.Collections.Generic;
using System.Linq;
using Asynkron.JsEngine.SExpressions;
using Esprima;
using Esprima.Ast;
using EsprimaLiteral = Esprima.Ast.Literal;

namespace Asynkron.JsEngine.Parsing;

/// <summary>
/// Transforms Esprima AST nodes into Lisp-style S-expressions so that the runtime
/// can evaluate JavaScript source using a small Lisp interpreter.
/// </summary>
public sealed class JsSExpressionBuilder
{
    private readonly ParserOptions _options;

    public JsSExpressionBuilder(ParserOptions? options = null)
    {
        _options = options ?? new ParserOptions
        {
            Tolerant = true
        };
    }

    /// <summary>
    /// Parses script code and returns an equivalent S-expression tree.
    /// </summary>
    public SExpression ParseScript(string source)
    {
        var parser = new JavaScriptParser(_options);
        var program = parser.ParseScript(source);
        return ConvertProgram(program);
    }

    private SExpression ConvertProgram(Script program)
    {
        var expressions = new List<SExpression> { SExpr.Symbol("begin") };
        foreach (var statement in program.Body)
        {
            expressions.AddRange(ConvertStatement(statement));
        }

        return SExpr.FromEnumerable(expressions);
    }

    private IEnumerable<SExpression> ConvertStatement(Statement statement)
    {
        return statement switch
        {
            BlockStatement block => new[] { ConvertBlock(block) },
            ExpressionStatement expressionStatement => new[] { ConvertExpression(expressionStatement.Expression) },
            VariableDeclaration declaration => ConvertVariableDeclaration(declaration),
            FunctionDeclaration function => new[] { ConvertFunctionDeclaration(function) },
            ReturnStatement ret => new[] { ConvertReturn(ret) },
            IfStatement ifStatement => new[] { ConvertIf(ifStatement) },
            EmptyStatement => Array.Empty<SExpression>(),
            _ => throw new NotSupportedException($"Statement '{statement.Type}' is not supported by the JS Lisp builder yet.")
        };
    }

    private SExpression ConvertBlock(BlockStatement block)
    {
        var expressions = new List<SExpression> { SExpr.Symbol("begin") };
        foreach (var statement in block.Body)
        {
            expressions.AddRange(ConvertStatement(statement));
        }

        return SExpr.FromEnumerable(expressions);
    }

    private IEnumerable<SExpression> ConvertVariableDeclaration(VariableDeclaration declaration)
    {
        foreach (var declarator in declaration.Declarations)
        {
            if (declarator.Id is not Identifier identifier)
            {
                throw new NotSupportedException("Only simple identifier bindings are supported in variable declarations.");
            }

            var initializer = declarator.Init is null
                ? SExpr.Nil
                : ConvertExpression(declarator.Init);

            var keyword = declaration.Kind switch
            {
                VariableDeclarationKind.Const => "const",
                VariableDeclarationKind.Let => "let",
                VariableDeclarationKind.Var => "var",
                _ => throw new ArgumentOutOfRangeException()
            };

            yield return SExpr.List(
                SExpr.Symbol(keyword),
                SExpr.Symbol(identifier.Name),
                initializer);
        }
    }

    private SExpression ConvertFunctionDeclaration(FunctionDeclaration declaration)
    {
        if (declaration.Id is null)
        {
            throw new NotSupportedException("Function declarations require a name.");
        }

        var lambda = ConvertFunctionLikeToLambda(declaration.Params, declaration.Body);
        return SExpr.List(SExpr.Symbol("def"), SExpr.Symbol(declaration.Id.Name), lambda);
    }

    private SExpression ConvertReturn(ReturnStatement ret)
    {
        var value = ret.Argument is null ? SExpr.Nil : ConvertExpression(ret.Argument);
        return SExpr.List(SExpr.Symbol("return"), value);
    }

    private SExpression ConvertIf(IfStatement ifStatement)
    {
        var test = ConvertExpression(ifStatement.Test);
        var consequent = ConvertStatementAsSingle(ifStatement.Consequent);
        var alternate = ifStatement.Alternate is null
            ? SExpr.Nil
            : ConvertStatementAsSingle(ifStatement.Alternate);

        return SExpr.List(SExpr.Symbol("if"), test, consequent, alternate);
    }

    private SExpression ConvertFunctionLikeToLambda(NodeList<Node> parameters, Statement body)
    {
        var parameterSymbols = new List<SExpression>();
        foreach (var parameter in parameters)
        {
            if (parameter is Identifier identifier)
            {
                parameterSymbols.Add(SExpr.Symbol(identifier.Name));
            }
            else
            {
                throw new NotSupportedException("Only simple identifier parameters are supported for now.");
            }
        }

        var lambdaParts = new List<SExpression>
        {
            SExpr.Symbol("lambda"),
            SExpr.FromEnumerable(parameterSymbols)
        };

        var bodyExpressions = ConvertStatement(body).ToList();
        if (bodyExpressions.Count == 0)
        {
            lambdaParts.Add(SExpr.Nil);
        }
        else
        {
            lambdaParts.AddRange(bodyExpressions);
        }

        return SExpr.FromEnumerable(lambdaParts);
    }

    private SExpression ConvertStatementAsSingle(Statement statement)
    {
        var expressions = ConvertStatement(statement).ToList();
        return expressions.Count switch
        {
            0 => SExpr.Nil,
            1 => expressions[0],
            _ => SExpr.FromEnumerable(new[] { SExpr.Symbol("begin") }.Concat(expressions))
        };
    }

    private SExpression ConvertExpression(Expression expression)
    {
        return expression switch
        {
            EsprimaLiteral literal => ConvertLiteral(literal),
            Identifier identifier => SExpr.Symbol(identifier.Name),
            BinaryExpression binary => ConvertBinaryExpression(binary),
            AssignmentExpression assignment => ConvertAssignment(assignment),
            CallExpression call => ConvertCall(call),
            FunctionExpression func => ConvertFunctionLikeToLambda(func.Params, func.Body),
            ArrowFunctionExpression arrow => ConvertArrowFunction(arrow),
            MemberExpression member => ConvertMember(member),
            UpdateExpression update => ConvertUpdate(update),
            UnaryExpression unary => ConvertUnary(unary),
            ConditionalExpression conditional => ConvertConditional(conditional),
            SequenceExpression sequence => ConvertSequence(sequence),
            _ => throw new NotSupportedException($"Expression '{expression.Type}' is not supported by the JS Lisp builder yet.")
        };
    }

    private SExpression ConvertSequence(SequenceExpression sequence)
    {
        var expressions = new List<SExpression> { SExpr.Symbol("begin") };
        foreach (var expr in sequence.Expressions)
        {
            expressions.Add(ConvertExpression(expr));
        }

        return SExpr.FromEnumerable(expressions);
    }

    private SExpression ConvertConditional(ConditionalExpression conditional)
    {
        var test = ConvertExpression(conditional.Test);
        var consequent = ConvertExpression(conditional.Consequent);
        var alternate = ConvertExpression(conditional.Alternate);
        return SExpr.List(SExpr.Symbol("if"), test, consequent, alternate);
    }

    private SExpression ConvertUpdate(UpdateExpression update)
    {
        if (update.Argument is not Identifier identifier)
        {
            throw new NotSupportedException("Only identifier increment/decrement is supported.");
        }

        var op = update.Operator switch
        {
            UnaryOperator.Increment => "+",
            UnaryOperator.Decrement => "-",
            _ => throw new ArgumentOutOfRangeException(nameof(update.Operator))
        };

        var binary = SExpr.List(SExpr.Symbol(op), SExpr.Symbol(identifier.Name), SExpr.Literal(1));
        return update.Prefix
            ? SExpr.List(SExpr.Symbol("set"), SExpr.Symbol(identifier.Name), binary)
            : SExpr.List(SExpr.Symbol("post-update"), SExpr.Symbol(identifier.Name), binary);
    }

    private SExpression ConvertUnary(UnaryExpression unary)
    {
        var operand = ConvertExpression(unary.Argument);
        return unary.Operator switch
        {
            UnaryOperator.Plus => SExpr.List(SExpr.Symbol("+"), SExpr.Literal(0), operand),
            UnaryOperator.Minus => SExpr.List(SExpr.Symbol("-"), SExpr.Literal(0), operand),
            UnaryOperator.LogicalNot => SExpr.List(SExpr.Symbol("not"), operand),
            UnaryOperator.BitwiseNot => SExpr.List(SExpr.Symbol("bnot"), operand),
            UnaryOperator.TypeOf => SExpr.List(SExpr.Symbol("typeof"), operand),
            _ => throw new NotSupportedException($"Unary operator '{unary.Operator}' is not supported yet.")
        };
    }

    private SExpression ConvertMember(MemberExpression member)
    {
        var target = ConvertExpression(member.Object);
        SExpression property = member.Property switch
        {
            Identifier identifier when !member.Computed => SExpr.Symbol(identifier.Name),
            Expression expr => ConvertExpression(expr),
            _ => throw new NotSupportedException("Unsupported member expression property type.")
        };

        return SExpr.List(SExpr.Symbol("member"), target, property);
    }

    private SExpression ConvertArrowFunction(ArrowFunctionExpression arrow)
    {
        var bodyStatement = arrow.Body switch
        {
            BlockStatement block => (Statement)block,
            Expression expr => new ReturnStatement(expr),
            _ => throw new NotSupportedException("Unsupported arrow function body.")
        };

        return ConvertFunctionLikeToLambda(arrow.Params, bodyStatement);
    }

    private SExpression ConvertCall(CallExpression call)
    {
        var arguments = new List<SExpression> { SExpr.Symbol("call"), ConvertExpression(call.Callee) };
        foreach (var argument in call.Arguments)
        {
            if (argument is Expression expr)
            {
                arguments.Add(ConvertExpression(expr));
            }
            else
            {
                throw new NotSupportedException("Spread elements are not supported in calls yet.");
            }
        }

        return SExpr.FromEnumerable(arguments);
    }

    private SExpression ConvertAssignment(AssignmentExpression assignment)
    {
        if (assignment.Left is not Identifier identifier)
        {
            throw new NotSupportedException("Only identifier assignments are supported for now.");
        }

        if (assignment.Operator != AssignmentOperator.Assign)
        {
            throw new NotSupportedException($"Assignment operator '{assignment.Operator}' is not supported yet.");
        }

        return SExpr.List(SExpr.Symbol("set"), SExpr.Symbol(identifier.Name), ConvertExpression(assignment.Right));
    }

    private SExpression ConvertBinaryExpression(BinaryExpression binary)
    {
        var op = binary.Operator switch
        {
            BinaryOperator.Plus => "+",
            BinaryOperator.Minus => "-",
            BinaryOperator.Times => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.Modulo => "%",
            BinaryOperator.Equal => "==",
            BinaryOperator.NotEqual => "!=",
            BinaryOperator.StrictlyEqual => "===",
            BinaryOperator.StrictlyNotEqual => "!==",
            BinaryOperator.Less => "<",
            BinaryOperator.LessOrEqual => "<=",
            BinaryOperator.Greater => ">",
            BinaryOperator.GreaterOrEqual => ">=",
            BinaryOperator.LogicalAnd => "and",
            BinaryOperator.LogicalOr => "or",
            BinaryOperator.BitwiseAnd => "band",
            BinaryOperator.BitwiseOr => "bor",
            BinaryOperator.BitwiseXor => "bxor",
            BinaryOperator.LeftShift => "shl",
            BinaryOperator.RightShift => "shr",
            BinaryOperator.UnsignedRightShift => "ushr",
            BinaryOperator.InstanceOf => throw new NotSupportedException("The 'instanceof' operator is not supported yet."),
            BinaryOperator.In => throw new NotSupportedException("The 'in' operator is not supported yet."),
            BinaryOperator.Exponentiation => "pow",
            BinaryOperator.NullishCoalescing => throw new NotSupportedException("The '??' operator is not supported yet."),
            _ => throw new ArgumentOutOfRangeException(nameof(binary.Operator))
        };

        return SExpr.List(
            SExpr.Symbol(op),
            ConvertExpression(binary.Left),
            ConvertExpression(binary.Right));
    }

    private SExpression ConvertLiteral(EsprimaLiteral literal)
    {
        return literal.Value switch
        {
            null => SExpr.Literal(null),
            double d when double.IsInteger(d) => SExpr.Literal((long)d),
            double d => SExpr.Literal(d),
            bool b => SExpr.Literal(b),
            string s => SExpr.Literal(s),
            _ => SExpr.Literal(literal.Value)
        };
    }
}
