using System.Collections.Generic;

namespace MiloMusicPlayer.Scripting;


public abstract class AstNode
{
    public int Line   { get; init; }
    public int Column { get; init; }
}

public abstract class Expr : AstNode { }

public abstract class Stmt : AstNode { }

public sealed class TypeRef : AstNode
{
    public string       Name       { get; init; } = "";
    public List<TypeRef> TypeArgs  { get; init; } = new(); 
    public bool         IsNullable { get; init; }          
}


public sealed class IntLiteralExpr : Expr
{
    public int Value { get; init; }
}

public sealed class FloatLiteralExpr : Expr
{
    public float Value { get; init; }
}

public sealed class StringLiteralExpr : Expr
{
    public string Value { get; init; } = "";
}

public sealed class BoolLiteralExpr : Expr
{
    public bool Value { get; init; }
}

public sealed class NullLiteralExpr : Expr { }

public sealed class VariableExpr : Expr
{
    public string Name { get; init; } = "";
}

public sealed class ThisExpr : Expr { }

public sealed class UnaryExpr : Expr
{
    public Token    Op      { get; init; } = null!;
    public Expr     Operand { get; init; } = null!;
}

public sealed class BinaryExpr : Expr
{
    public Expr  Left  { get; init; } = null!;
    public Token Op    { get; init; } = null!;
    public Expr  Right { get; init; } = null!;
}

public sealed class AssignExpr : Expr
{
    public string Name  { get; init; } = "";
    public Expr   Value { get; init; } = null!;
}

public sealed class SetExpr : Expr
{
    public Expr   Object { get; init; } = null!;
    public string Name   { get; init; } = "";
    public Expr   Value  { get; init; } = null!;
}

public sealed class GetExpr : Expr
{
    public Expr   Object { get; init; } = null!;
    public string Name   { get; init; } = "";
}

public sealed class CallExpr : Expr
{
    public Expr        Callee    { get; init; } = null!;
    public List<Expr>  Arguments { get; init; } = new();
}

public sealed class NewExpr : Expr
{
    public TypeRef     Type      { get; init; } = null!;
    public List<Expr>  Arguments { get; init; } = new();
}

public sealed class GroupExpr : Expr
{
    public Expr Inner { get; init; } = null!;
}

public sealed class LambdaExpr : Expr
{
    public List<Parameter> Params     { get; init; } = new();
    public TypeRef?        ReturnType { get; init; }
    public BlockStmt       Body       { get; init; } = null!;
}


public sealed class BlockStmt : Stmt
{
    public List<Stmt> Statements { get; init; } = new();
}

public sealed class VarDeclStmt : Stmt
{
    public string   Name        { get; init; } = "";
    public TypeRef? TypeAnnot   { get; init; }   
    public Expr?    Initializer { get; init; }
}

public sealed class ExprStmt : Stmt
{
    public Expr Expression { get; init; } = null!;
}

public sealed class ReturnStmt : Stmt
{
    public Expr? Value { get; init; }
}

public sealed class IfStmt : Stmt
{
    public Expr      Condition  { get; init; } = null!;
    public Stmt      Then       { get; init; } = null!;
    public Stmt?     Else       { get; init; }
}

public sealed class WhileStmt : Stmt
{
    public Expr Condition { get; init; } = null!;
    public Stmt Body      { get; init; } = null!;
}

public sealed class ForInStmt : Stmt
{
    public string VarName   { get; init; } = "";
    public Expr   Iterable  { get; init; } = null!;
    public Stmt   Body      { get; init; } = null!;
}


public sealed class Parameter : AstNode
{
    public string  Name { get; init; } = "";
    public TypeRef Type { get; init; } = null!;
}

public sealed class FunDecl : Stmt
{
    public string         Name       { get; init; } = "";
    public List<string>   TypeParams { get; init; } = new(); 
    public List<Parameter> Params    { get; init; } = new();
    public TypeRef?       ReturnType { get; init; }
    public BlockStmt      Body       { get; init; } = null!;
}

public sealed class FieldDecl : AstNode
{
    public string   Name        { get; init; } = "";
    public TypeRef? TypeAnnot   { get; init; }
    public Expr?    Initializer { get; init; }
}

public sealed class MethodDecl : AstNode
{
    public string          Name       { get; init; } = "";
    public List<string>    TypeParams { get; init; } = new();
    public List<Parameter> Params     { get; init; } = new();
    public TypeRef?        ReturnType { get; init; }
    public BlockStmt?      Body       { get; init; }  
}

public sealed class ClassDecl : Stmt
{
    public string         Name        { get; init; } = "";
    public List<string>   TypeParams  { get; init; } = new();  
    public TypeRef?       SuperClass  { get; init; }            
    public List<TypeRef>  Interfaces  { get; init; } = new();  
    public List<FieldDecl>  Fields    { get; init; } = new();
    public List<MethodDecl> Methods   { get; init; } = new();
}

public sealed class InterfaceDecl : Stmt
{
    public string          Name       { get; init; } = "";
    public List<string>    TypeParams { get; init; } = new();
    public List<MethodDecl> Methods   { get; init; } = new();
}

public sealed class EnumDecl : Stmt
{
    public string       Name    { get; init; } = "";
    public List<string> Members { get; init; } = new();
}

public sealed class ImportStmt : Stmt
{
    public string ModuleName { get; init; } = "";
}


public sealed class ScriptFile : AstNode
{
    public string     FilePath   { get; init; } = "";
    public List<Stmt> Statements { get; init; } = new();
}