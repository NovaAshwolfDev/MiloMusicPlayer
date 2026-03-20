using System;
using System.Collections.Generic;

namespace MiloMusicPlayer.Scripting;

public sealed class ParseException : Exception
{
    public int Line   { get; }
    public int Column { get; }

    public ParseException(string message, int line, int column)
        : base($"[{line}:{column}] Parse error: {message}")
    {
        Line   = line;
        Column = column;
    }
}

public sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _current = 0;

    public Parser(List<Token> tokens)
    {
        _tokens = tokens;
    }

    public ScriptFile Parse(string filePath = "")
    {
        var statements = new List<Stmt>();

        while (!IsAtEnd())
            statements.Add(ParseTopLevelDecl());

        return new ScriptFile
        {
            FilePath   = filePath,
            Statements = statements,
            Line       = 1,
            Column     = 1
        };
    }


    private Stmt ParseTopLevelDecl()
    {
        if (Check(TokenType.Import))    return ParseImport();
        if (Check(TokenType.Class))     return ParseClass();
        if (Check(TokenType.Interface)) return ParseInterface();
        if (Check(TokenType.Enum))      return ParseEnum();
        if (Check(TokenType.Fun))       return ParseFunDecl();
        if (Check(TokenType.Var))       return ParseVarDecl();

        throw Error($"Expected a top-level declaration, got '{Peek().Lexeme}'");
    }

    private ImportStmt ParseImport()
    {
        var tok = Consume(TokenType.Import, "Expected 'import'");
        var name = Consume(TokenType.Identifier, "Expected module name after 'import'");
        Consume(TokenType.Semicolon, "Expected ';' after import");

        return new ImportStmt
        {
            ModuleName = name.Lexeme,
            Line       = tok.Line,
            Column     = tok.Column
        };
    }


    private ClassDecl ParseClass()
    {
        var tok = Consume(TokenType.Class, "Expected 'class'");
        var name = Consume(TokenType.Identifier, "Expected class name");

        var typeParams = ParseOptionalTypeParams();

        TypeRef? superClass = null;
        if (Match(TokenType.Extends))
            superClass = ParseTypeRef();

        var interfaces = new List<TypeRef>();

        if (Match(TokenType.Implements) || Match(TokenType.Colon))
        {
            interfaces.Add(ParseTypeRef());
            while (Match(TokenType.Comma))
                interfaces.Add(ParseTypeRef());
        }

        Consume(TokenType.LeftBrace, "Expected '{' before class body");

        var fields  = new List<FieldDecl>();
        var methods = new List<MethodDecl>();

        while (!Check(TokenType.RightBrace) && !IsAtEnd())
        {
            if (Check(TokenType.Var))
                fields.Add(ParseFieldDecl());
            else if (Check(TokenType.Fun))
                methods.Add(ParseMethodDecl());
            else
                throw Error($"Expected field or method declaration, got '{Peek().Lexeme}'");
        }

        Consume(TokenType.RightBrace, "Expected '}' after class body");

        return new ClassDecl
        {
            Name       = name.Lexeme,
            TypeParams = typeParams,
            SuperClass = superClass,
            Interfaces = interfaces,
            Fields     = fields,
            Methods    = methods,
            Line       = tok.Line,
            Column     = tok.Column
        };
    }

    private FieldDecl ParseFieldDecl()
    {
        var tok = Consume(TokenType.Var, "Expected 'var'");
        var name = Consume(TokenType.Identifier, "Expected field name");

        TypeRef? typeAnnot = null;
        if (Match(TokenType.Colon))
            typeAnnot = ParseTypeRef();

        Expr? initializer = null;
        if (Match(TokenType.Equals))
            initializer = ParseExpression();

        Consume(TokenType.Semicolon, "Expected ';' after field declaration");

        return new FieldDecl
        {
            Name        = name.Lexeme,
            TypeAnnot   = typeAnnot,
            Initializer = initializer,
            Line        = tok.Line,
            Column      = tok.Column
        };
    }

    private MethodDecl ParseMethodDecl()
    {
        var tok = Consume(TokenType.Fun, "Expected 'fun'");
        var name = Consume(TokenType.Identifier, "Expected method name");

        var typeParams = ParseOptionalTypeParams();
        var parameters = ParseParamList();

        TypeRef? returnType = null;
        if (Match(TokenType.Colon))
            returnType = ParseTypeRef();

        BlockStmt? body = null;
        if (Check(TokenType.LeftBrace))
            body = ParseBlock();
        else
            Consume(TokenType.Semicolon, "Expected '{' or ';' after method signature");

        return new MethodDecl
        {
            Name       = name.Lexeme,
            TypeParams = typeParams,
            Params     = parameters,
            ReturnType = returnType,
            Body       = body,
            Line       = tok.Line,
            Column     = tok.Column
        };
    }


    private InterfaceDecl ParseInterface()
    {
        var tok = Consume(TokenType.Interface, "Expected 'interface'");
        var name = Consume(TokenType.Identifier, "Expected interface name");
        var typeParams = ParseOptionalTypeParams();

        Consume(TokenType.LeftBrace, "Expected '{' before interface body");

        var methods = new List<MethodDecl>();
        while (!Check(TokenType.RightBrace) && !IsAtEnd())
        {
            if (Check(TokenType.Fun))
                methods.Add(ParseMethodDecl());
            else
                throw Error($"Expected method signature in interface, got '{Peek().Lexeme}'");
        }

        Consume(TokenType.RightBrace, "Expected '}' after interface body");

        return new InterfaceDecl
        {
            Name       = name.Lexeme,
            TypeParams = typeParams,
            Methods    = methods,
            Line       = tok.Line,
            Column     = tok.Column
        };
    }


    private EnumDecl ParseEnum()
    {
        var tok = Consume(TokenType.Enum, "Expected 'enum'");
        var name = Consume(TokenType.Identifier, "Expected enum name");

        Consume(TokenType.LeftBrace, "Expected '{' before enum body");

        var members = new List<string>();
        while (!Check(TokenType.RightBrace) && !IsAtEnd())
        {
            members.Add(Consume(TokenType.Identifier, "Expected enum member name").Lexeme);
            if (!Match(TokenType.Comma)) break;
        }

        Consume(TokenType.RightBrace, "Expected '}' after enum body");

        return new EnumDecl
        {
            Name    = name.Lexeme,
            Members = members,
            Line    = tok.Line,
            Column  = tok.Column
        };
    }


    private FunDecl ParseFunDecl()
    {
        var tok = Consume(TokenType.Fun, "Expected 'fun'");
        var name = Consume(TokenType.Identifier, "Expected function name");

        var typeParams = ParseOptionalTypeParams();
        var parameters = ParseParamList();

        TypeRef? returnType = null;
        if (Match(TokenType.Colon))
            returnType = ParseTypeRef();

        var body = ParseBlock();

        return new FunDecl
        {
            Name       = name.Lexeme,
            TypeParams = typeParams,
            Params     = parameters,
            ReturnType = returnType,
            Body       = body,
            Line       = tok.Line,
            Column     = tok.Column
        };
    }


    private Stmt ParseStatement()
    {
        if (Check(TokenType.Var))    return ParseVarDecl();
        if (Check(TokenType.Return)) return ParseReturn();
        if (Check(TokenType.If))     return ParseIf();
        if (Check(TokenType.While))  return ParseWhile();
        if (Check(TokenType.For))    return ParseForIn();
        if (Check(TokenType.Fun))    return ParseFunDecl();  
        if (Check(TokenType.LeftBrace)) return ParseBlock();

        return ParseExprStmt();
    }

    private BlockStmt ParseBlock()
    {
        var tok = Consume(TokenType.LeftBrace, "Expected '{'");
        var stmts = new List<Stmt>();

        while (!Check(TokenType.RightBrace) && !IsAtEnd())
            stmts.Add(ParseStatement());

        Consume(TokenType.RightBrace, "Expected '}' after block");

        return new BlockStmt
        {
            Statements = stmts,
            Line       = tok.Line,
            Column     = tok.Column
        };
    }

    private VarDeclStmt ParseVarDecl()
    {
        var tok = Consume(TokenType.Var, "Expected 'var'");
        var name = Consume(TokenType.Identifier, "Expected variable name");

        TypeRef? typeAnnot = null;
        if (Match(TokenType.Colon))
            typeAnnot = ParseTypeRef();

        Expr? initializer = null;
        if (Match(TokenType.Equals))
            initializer = ParseExpression();

        Consume(TokenType.Semicolon, "Expected ';' after variable declaration");

        return new VarDeclStmt
        {
            Name        = name.Lexeme,
            TypeAnnot   = typeAnnot,
            Initializer = initializer,
            Line        = tok.Line,
            Column      = tok.Column
        };
    }

    private ReturnStmt ParseReturn()
    {
        var tok = Consume(TokenType.Return, "Expected 'return'");

        Expr? value = null;
        if (!Check(TokenType.Semicolon))
            value = ParseExpression();

        Consume(TokenType.Semicolon, "Expected ';' after return");

        return new ReturnStmt
        {
            Value  = value,
            Line   = tok.Line,
            Column = tok.Column
        };
    }

    private IfStmt ParseIf()
    {
        var tok = Consume(TokenType.If, "Expected 'if'");
        Consume(TokenType.LeftParen, "Expected '(' after 'if'");
        var condition = ParseExpression();
        Consume(TokenType.RightParen, "Expected ')' after if condition");

        var then = ParseStatement();
        Stmt? els = null;
        if (Match(TokenType.Else))
            els = ParseStatement();

        return new IfStmt
        {
            Condition = condition,
            Then      = then,
            Else      = els,
            Line      = tok.Line,
            Column    = tok.Column
        };
    }

    private WhileStmt ParseWhile()
    {
        var tok = Consume(TokenType.While, "Expected 'while'");
        Consume(TokenType.LeftParen, "Expected '(' after 'while'");
        var condition = ParseExpression();
        Consume(TokenType.RightParen, "Expected ')' after while condition");
        var body = ParseStatement();

        return new WhileStmt
        {
            Condition = condition,
            Body      = body,
            Line      = tok.Line,
            Column    = tok.Column
        };
    }

    private ForInStmt ParseForIn()
    {
        var tok = Consume(TokenType.For, "Expected 'for'");
        Consume(TokenType.LeftParen, "Expected '(' after 'for'");
        var varName = Consume(TokenType.Identifier, "Expected loop variable name");
        Consume(TokenType.In, "Expected 'in' after loop variable");
        var iterable = ParseExpression();
        Consume(TokenType.RightParen, "Expected ')' after for-in expression");
        var body = ParseStatement();

        return new ForInStmt
        {
            VarName  = varName.Lexeme,
            Iterable = iterable,
            Body     = body,
            Line     = tok.Line,
            Column   = tok.Column
        };
    }

    private ExprStmt ParseExprStmt()
    {
        var expr = ParseExpression();
        Consume(TokenType.Semicolon, "Expected ';' after expression");

        return new ExprStmt
        {
            Expression = expr,
            Line       = expr.Line,
            Column     = expr.Column
        };
    }


    private Expr ParseExpression() => ParseAssignment();

    private Expr ParseAssignment()
    {
        var expr = ParseOr();

        if (Check(TokenType.Equals))
        {
            var eq = Advance();

            if (expr is GetExpr get)
            {
                var value = ParseAssignment();
                return new SetExpr
                {
                    Object = get.Object,
                    Name   = get.Name,
                    Value  = value,
                    Line   = eq.Line,
                    Column = eq.Column
                };
            }

            if (expr is VariableExpr varExpr)
            {
                var value = ParseAssignment();
                return new AssignExpr
                {
                    Name   = varExpr.Name,
                    Value  = value,
                    Line   = eq.Line,
                    Column = eq.Column
                };
            }

            throw Error("Invalid assignment target", eq.Line, eq.Column);
        }

        return expr;
    }

    private Expr ParseOr()
    {
        var left = ParseAnd();
        while (Check(TokenType.Or))
        {
            var op    = Advance();
            var right = ParseAnd();
            left = new BinaryExpr { Left = left, Op = op, Right = right, Line = op.Line, Column = op.Column };
        }
        return left;
    }

    private Expr ParseAnd()
    {
        var left = ParseEquality();
        while (Check(TokenType.And))
        {
            var op    = Advance();
            var right = ParseEquality();
            left = new BinaryExpr { Left = left, Op = op, Right = right, Line = op.Line, Column = op.Column };
        }
        return left;
    }

    private Expr ParseEquality()
    {
        var left = ParseComparison();
        while (Check(TokenType.EqualsEquals) || Check(TokenType.BangEquals))
        {
            var op    = Advance();
            var right = ParseComparison();
            left = new BinaryExpr { Left = left, Op = op, Right = right, Line = op.Line, Column = op.Column };
        }
        return left;
    }

    private Expr ParseComparison()
    {
        var left = ParseAddition();
        while (Check(TokenType.Less) || Check(TokenType.LessEquals) ||
               Check(TokenType.Greater) || Check(TokenType.GreaterEquals))
        {
            var op    = Advance();
            var right = ParseAddition();
            left = new BinaryExpr { Left = left, Op = op, Right = right, Line = op.Line, Column = op.Column };
        }
        return left;
    }

    private Expr ParseAddition()
    {
        var left = ParseMultiplication();
        while (Check(TokenType.Plus) || Check(TokenType.Minus))
        {
            var op    = Advance();
            var right = ParseMultiplication();
            left = new BinaryExpr { Left = left, Op = op, Right = right, Line = op.Line, Column = op.Column };
        }
        return left;
    }

    private Expr ParseMultiplication()
    {
        var left = ParseUnary();
        while (Check(TokenType.Star) || Check(TokenType.Slash) || Check(TokenType.Percent))
        {
            var op    = Advance();
            var right = ParseUnary();
            left = new BinaryExpr { Left = left, Op = op, Right = right, Line = op.Line, Column = op.Column };
        }
        return left;
    }

    private Expr ParseUnary()
    {
        if (Check(TokenType.Bang) || Check(TokenType.Minus))
        {
            var op      = Advance();
            var operand = ParseUnary();
            return new UnaryExpr { Op = op, Operand = operand, Line = op.Line, Column = op.Column };
        }
        return ParseCall();
    }

    private Expr ParseCall()
    {
        var expr = ParsePrimary();

        while (true)
        {
            if (Check(TokenType.LeftParen))
            {
                var paren = Advance();
                var args  = ParseArgList();
                Consume(TokenType.RightParen, "Expected ')' after arguments");
                expr = new CallExpr { Callee = expr, Arguments = args, Line = paren.Line, Column = paren.Column };
            } 
            
            else if (Check(TokenType.LeftBracket))
            {
                var tok   = Advance();
                var index = ParseExpression();
                Consume(TokenType.RightBracket, "Expected ']' after index");
                expr = new IndexExpr { Object = expr, Index = index, Line = tok.Line, Column = tok.Column };
            }

            else if (Check(TokenType.Dot))
            {
                var dot  = Advance();
                var name = Consume(TokenType.Identifier, "Expected property name after '.'");
                expr = new GetExpr { Object = expr, Name = name.Lexeme, Line = dot.Line, Column = dot.Column };
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private Expr ParsePrimary()
    {
        if (Check(TokenType.IntLiteral))
        {
            var tok = Advance();
            return new IntLiteralExpr { Value = (int)tok.Literal!, Line = tok.Line, Column = tok.Column };
        }
        if (Check(TokenType.FloatLiteral))
        {
            var tok = Advance();
            return new FloatLiteralExpr { Value = (float)tok.Literal!, Line = tok.Line, Column = tok.Column };
        }
        if (Check(TokenType.StringLiteral))
        {
            var tok = Advance();
            return new StringLiteralExpr { Value = (string)tok.Literal!, Line = tok.Line, Column = tok.Column };
        }
        if (Check(TokenType.BoolLiteral))
        {
            var tok = Advance();
            return new BoolLiteralExpr { Value = (bool)tok.Literal!, Line = tok.Line, Column = tok.Column };
        }
        if (Check(TokenType.NullLiteral))
        {
            var tok = Advance();
            return new NullLiteralExpr { Line = tok.Line, Column = tok.Column };
        }

        if (Check(TokenType.This))
        {
            var tok = Advance();
            return new ThisExpr { Line = tok.Line, Column = tok.Column };
        }

        if (Check(TokenType.New))
        {
            var tok  = Advance();
            var type = ParseTypeRef();
            Consume(TokenType.LeftParen, "Expected '(' after type in 'new'");
            var args = ParseArgList();
            Consume(TokenType.RightParen, "Expected ')' after constructor arguments");
            return new NewExpr { Type = type, Arguments = args, Line = tok.Line, Column = tok.Column };
        }

        if (Check(TokenType.Fun))
        {
            var tok    = Advance();
            var parms  = ParseParamList();
            TypeRef? ret = null;
            if (Match(TokenType.Colon))
                ret = ParseTypeRef();
            var body = ParseBlock();
            return new LambdaExpr { Params = parms, ReturnType = ret, Body = body, Line = tok.Line, Column = tok.Column };
        }

        if (Check(TokenType.LeftParen))
        {
            var tok   = Advance();
            var inner = ParseExpression();
            Consume(TokenType.RightParen, "Expected ')' after grouped expression");
            return new GroupExpr { Inner = inner, Line = tok.Line, Column = tok.Column };
        }

        if (Check(TokenType.Identifier))
        {
            var tok = Advance();
            return new VariableExpr { Name = tok.Lexeme, Line = tok.Line, Column = tok.Column };
        }
        
        if (Check(TokenType.LeftBracket))
        {
            var tok  = Advance();
            var items = new List<Expr>();

            if (!Check(TokenType.RightBracket))
            {
                items.Add(ParseExpression());
                while (Match(TokenType.Comma))
                    items.Add(ParseExpression());
            }

            Consume(TokenType.RightBracket, "Expected ']' after list literal");

            return new ListLiteralExpr
            {
                Items  = items,
                Line   = tok.Line,
                Column = tok.Column
            };
        }

        throw Error($"Unexpected token '{Peek().Lexeme}' in expression");
    }


    private List<string> ParseOptionalTypeParams()
    {
        var list = new List<string>();
        if (!Check(TokenType.Less)) return list;

        Advance(); 
        list.Add(Consume(TokenType.Identifier, "Expected type parameter name").Lexeme);
        while (Match(TokenType.Comma))
            list.Add(Consume(TokenType.Identifier, "Expected type parameter name").Lexeme);

        Consume(TokenType.Greater, "Expected '>' after type parameters");
        return list;
    }

    private List<Parameter> ParseParamList()
    {
        Consume(TokenType.LeftParen, "Expected '(' before parameter list");
        var list = new List<Parameter>();

        if (!Check(TokenType.RightParen))
        {
            list.Add(ParseParameter());
            while (Match(TokenType.Comma))
                list.Add(ParseParameter());
        }

        Consume(TokenType.RightParen, "Expected ')' after parameter list");
        return list;
    }

    private Parameter ParseParameter()
    {
        var name = Consume(TokenType.Identifier, "Expected parameter name");
        Consume(TokenType.Colon, "Expected ':' after parameter name");
        var type = ParseTypeRef();

        return new Parameter { Name = name.Lexeme, Type = type, Line = name.Line, Column = name.Column };
    }

    private List<Expr> ParseArgList()
    {
        var list = new List<Expr>();
        if (Check(TokenType.RightParen)) return list;

        list.Add(ParseExpression());
        while (Match(TokenType.Comma))
            list.Add(ParseExpression());

        return list;
    }

    private TypeRef ParseTypeRef()
    {
        Token nameTok;
        if (Check(TokenType.Identifier)  ||
            Check(TokenType.TypeInt)     ||
            Check(TokenType.TypeFloat)   ||
            Check(TokenType.TypeString)  ||
            Check(TokenType.TypeBool)    ||
            Check(TokenType.Void))
        {
            nameTok = Advance();
        }
        else
        {
            throw Error($"Expected type name, got '{Peek().Lexeme}'");
        }

        var typeArgs = new List<TypeRef>();
        if (Check(TokenType.Less))
        {
            Advance(); 
            typeArgs.Add(ParseTypeRef());
            while (Match(TokenType.Comma))
                typeArgs.Add(ParseTypeRef());
            Consume(TokenType.Greater, "Expected '>' after type arguments");
        }

        return new TypeRef
        {
            Name     = nameTok.Lexeme,
            TypeArgs = typeArgs,
            Line     = nameTok.Line,
            Column   = nameTok.Column
        };
    }


    private Token Peek()    => _tokens[_current];
    private Token Previous()=> _tokens[_current - 1];
    private bool  IsAtEnd() => Peek().Type == TokenType.Eof;

    private bool Check(TokenType type) => !IsAtEnd() && Peek().Type == type;

    private Token Advance()
    {
        if (!IsAtEnd()) _current++;
        return Previous();
    }

    private bool Match(TokenType type)
    {
        if (!Check(type)) return false;
        Advance();
        return true;
    }

    private Token Consume(TokenType type, string errorMessage)
    {
        if (Check(type)) return Advance();
        throw Error(errorMessage);
    }

    private ParseException Error(string message, int? line = null, int? column = null)
    {
        var tok = Peek();
        return new ParseException(message, line ?? tok.Line, column ?? tok.Column);
    }
}