using System;
using System.Collections.Generic;
using System.Linq;

namespace MiloMusicPlayer.Scripting;


public abstract class MiloType
{
    public abstract string Name { get; }
    public override string ToString() => Name;
}

public sealed class PrimitiveType : MiloType
{
    public static readonly PrimitiveType Int    = new("Int");
    public static readonly PrimitiveType Float  = new("Float");
    public static readonly PrimitiveType String = new("String");
    public static readonly PrimitiveType Bool   = new("Bool");
    public static readonly PrimitiveType Void   = new("Void");
    public static readonly PrimitiveType Null   = new("Null");

    public override string Name { get; }
    private PrimitiveType(string name) { Name = name; }
}

public sealed class ClassType : MiloType
{
    public override string Name       { get; }
    public ClassType?      SuperClass { get; set; }
    public List<string>    TypeParams { get; } = new();

    public Dictionary<string, MiloType> Fields  { get; } = new();
    public Dictionary<string, FunctionType> Methods { get; } = new();
    public List<InterfaceType> Interfaces { get; } = new();

    public ClassType(string name) { Name = name; }
}

public sealed class InterfaceType : MiloType
{
    public override string Name { get; }
    public List<string>    TypeParams { get; } = new();
    public Dictionary<string, FunctionType> Methods { get; } = new();

    public InterfaceType(string name) { Name = name; }
}

public sealed class EnumType : MiloType
{
    public override string  Name    { get; }
    public List<string>     Members { get; } = new();

    public EnumType(string name) { Name = name; }
}

public sealed class FunctionType : MiloType
{
    public List<MiloType> ParamTypes { get; }
    public MiloType       ReturnType { get; }
    public override string Name =>
        $"fun({string.Join(", ", ParamTypes)}): {ReturnType}";

    public FunctionType(List<MiloType> paramTypes, MiloType returnType)
    {
        ParamTypes = paramTypes;
        ReturnType = returnType;
    }
}

public sealed class TypeParamType : MiloType
{
    public override string Name { get; }
    public TypeParamType(string name) { Name = name; }
}

public sealed class UnknownType : MiloType
{
    public static readonly UnknownType Instance = new();
    public override string Name => "unknown";
    private UnknownType() { }
}


public sealed class TypeException : Exception
{
    public int Line   { get; }
    public int Column { get; }

    public TypeException(string message, int line, int column)
        : base($"[{line}:{column}] Type error: {message}")
    {
        Line   = line;
        Column = column;
    }
}


public sealed class Scope
{
    private readonly Dictionary<string, MiloType> _bindings = new();
    public  Scope? Parent { get; }

    public Scope(Scope? parent = null) { Parent = parent; }

    public void Define(string name, MiloType type) => _bindings[name] = type;

    public MiloType? Lookup(string name)
    {
        if (_bindings.TryGetValue(name, out var t)) return t;
        return Parent?.Lookup(name);
    }

    public bool IsDefined(string name) => _bindings.ContainsKey(name);
}


public sealed class TypeEnvironment
{
    private readonly Dictionary<string, MiloType> _types = new();

    public TypeEnvironment()
    {
        Register(PrimitiveType.Int);
        Register(PrimitiveType.Float);
        Register(PrimitiveType.String);
        Register(PrimitiveType.Bool);
        Register(PrimitiveType.Void);
    }

    public void Register(MiloType type) => _types[type.Name] = type;

    public MiloType? Resolve(string name)
    {
        _types.TryGetValue(name, out var t);
        return t;
    }

    public bool IsRegistered(string name) => _types.ContainsKey(name);
}


public sealed class TypeChecker
{
    private readonly TypeEnvironment _env;
    private readonly List<TypeException> _errors = new();

    private MiloType? _currentReturnType;

    private ClassType? _currentClass;

    public IReadOnlyList<TypeException> Errors => _errors;
    public bool HasErrors => _errors.Count > 0;

    public TypeChecker(TypeEnvironment env)
    {
        _env = env;
    }


    public void Check(ScriptFile file, Scope globalScope)
    {
        foreach (var stmt in file.Statements)
            RegisterTypeDecl(stmt);

        foreach (var stmt in file.Statements)
            CheckStmt(stmt, globalScope);
    }


    private void RegisterTypeDecl(Stmt stmt)
    {
        switch (stmt)
        {
            case ClassDecl cd:
                var classType = new ClassType(cd.Name);
                foreach (var tp in cd.TypeParams)
                    classType.TypeParams.Add(tp);
                _env.Register(classType);
                break;

            case InterfaceDecl id:
                var ifaceType = new InterfaceType(id.Name);
                foreach (var tp in id.TypeParams)
                    ifaceType.TypeParams.Add(tp);
                _env.Register(ifaceType);
                break;

            case EnumDecl ed:
                var enumType = new EnumType(ed.Name);
                foreach (var m in ed.Members)
                    enumType.Members.Add(m);
                _env.Register(enumType);
                break;
        }
    }


    private void CheckStmt(Stmt stmt, Scope scope)
    {
        try
        {
            switch (stmt)
            {
                case ImportStmt:
                    break;

                case ClassDecl cd:
                    CheckClassDecl(cd, scope);
                    break;

                case InterfaceDecl id:
                    CheckInterfaceDecl(id, scope);
                    break;

                case EnumDecl:
                    break;

                case FunDecl fd:
                    CheckFunDecl(fd, scope);
                    break;

                case VarDeclStmt vd:
                    CheckVarDecl(vd, scope);
                    break;

                case BlockStmt block:
                    CheckBlock(block, scope);
                    break;

                case ExprStmt es:
                    InferExpr(es.Expression, scope);
                    break;

                case ReturnStmt rs:
                    CheckReturn(rs, scope);
                    break;

                case IfStmt ifs:
                    CheckIf(ifs, scope);
                    break;

                case WhileStmt ws:
                    CheckWhile(ws, scope);
                    break;

                case ForInStmt fis:
                    CheckForIn(fis, scope);
                    break;

                default:
                    RecordError($"Unknown statement type '{stmt.GetType().Name}'", stmt);
                    break;
            }
        }
        catch (TypeException ex)
        {
            _errors.Add(ex);
        }
    }

    private void CheckBlock(BlockStmt block, Scope parent)
    {
        var inner = new Scope(parent);
        foreach (var s in block.Statements)
            CheckStmt(s, inner);
    }

    private void CheckVarDecl(VarDeclStmt vd, Scope scope)
    {
        MiloType declared = PrimitiveType.Void;

        if (vd.TypeAnnot is not null)
            declared = ResolveTypeRef(vd.TypeAnnot);

        if (vd.Initializer is not null)
        {
            var initType = InferExpr(vd.Initializer, scope);

            if (vd.TypeAnnot is null)
            {
                declared = initType;
            }
            else
            {
                AssertAssignable(initType, declared, vd.Initializer);
            }
        }
        else if (vd.TypeAnnot is null)
        {
            RecordError("Variable declaration needs either a type annotation or an initializer", vd);
        }

        scope.Define(vd.Name, declared);
    }

    private void CheckReturn(ReturnStmt rs, Scope scope)
    {
        if (_currentReturnType is null)
        {
            RecordError("'return' outside of a function", rs);
            return;
        }

        if (rs.Value is null)
        {
            if (_currentReturnType != PrimitiveType.Void)
                RecordError($"Expected a return value of type '{_currentReturnType}'", rs);
            return;
        }

        var valueType = InferExpr(rs.Value, scope);
        AssertAssignable(valueType, _currentReturnType, rs.Value);
    }

    private void CheckIf(IfStmt ifs, Scope scope)
    {
        var condType = InferExpr(ifs.Condition, scope);
        AssertType(condType, PrimitiveType.Bool, ifs.Condition, "If condition must be Bool");

        CheckStmt(ifs.Then, scope);
        if (ifs.Else is not null)
            CheckStmt(ifs.Else, scope);
    }

    private void CheckWhile(WhileStmt ws, Scope scope)
    {
        var condType = InferExpr(ws.Condition, scope);
        AssertType(condType, PrimitiveType.Bool, ws.Condition, "While condition must be Bool");
        CheckStmt(ws.Body, scope);
    }

    private void CheckForIn(ForInStmt fis, Scope scope)
    {
        var iterType = InferExpr(fis.Iterable, scope);

        MiloType elemType;
        if (iterType is ClassType ct && ct.Name == "List" && ct.TypeParams.Count == 1)
        {
            elemType = _env.Resolve(ct.TypeParams[0]) ?? UnknownType.Instance;
        }
        else
        {
            RecordError($"Cannot iterate over type '{iterType.Name}'; expected List<T>", fis);
            elemType = UnknownType.Instance;
        }

        var inner = new Scope(scope);
        inner.Define(fis.VarName, elemType);
        CheckStmt(fis.Body, inner);
    }


    private void CheckClassDecl(ClassDecl cd, Scope scope)
    {
        var classType = (ClassType)_env.Resolve(cd.Name)!;

        var classScope = new Scope(scope);
        foreach (var tp in cd.TypeParams)
        {
            var tpt = new TypeParamType(tp);
            classScope.Define(tp, tpt);
        }

        if (cd.SuperClass is not null)
        {
            var super = ResolveTypeRef(cd.SuperClass);
            if (super is ClassType superClass)
            {
                classType.SuperClass = superClass;
                foreach (var (k, v) in superClass.Fields)
                    if (!classType.Fields.ContainsKey(k))
                        classType.Fields[k] = v;
                foreach (var (k, v) in superClass.Methods)
                    if (!classType.Methods.ContainsKey(k))
                        classType.Methods[k] = v;
            }
            else
            {
                RecordError($"'{cd.SuperClass.Name}' is not a class", cd);
            }
        }

        foreach (var ifaceRef in cd.Interfaces)
        {
            var ifaceType = ResolveTypeRef(ifaceRef);
            if (ifaceType is InterfaceType iface)
                classType.Interfaces.Add(iface);
            else
                RecordError($"'{ifaceRef.Name}' is not an interface", cd);
        }

        foreach (var field in cd.Fields)
        {
            var ft = field.TypeAnnot is not null
                ? ResolveTypeRef(field.TypeAnnot)
                : PrimitiveType.Void;
            classType.Fields[field.Name] = ft;
        }

        foreach (var method in cd.Methods)
        {
            var sig = BuildMethodSignature(method, classScope);
            classType.Methods[method.Name] = sig;
        }

        _currentClass = classType;
        var instanceScope = new Scope(classScope);
        instanceScope.Define("this", classType);

        foreach (var (fieldName, fieldType) in classType.Fields)
            instanceScope.Define(fieldName, fieldType);

        foreach (var field in cd.Fields)
        {
            if (field.Initializer is not null)
            {
                var initType = InferExpr(field.Initializer, instanceScope);
                var declType = classType.Fields[field.Name];
                AssertAssignable(initType, declType, field.Initializer);
            }
        }

        foreach (var method in cd.Methods)
        {
            if (method.Body is not null)
                CheckMethodBody(method, classType, instanceScope);
        }

        _currentClass = null;

        foreach (var iface in classType.Interfaces)
            VerifyInterfaceImpl(classType, iface, cd);
    }

    private FunctionType BuildMethodSignature(MethodDecl method, Scope scope)
    {
        var paramTypes = method.Params
            .Select(p => ResolveTypeRef(p.Type))
            .ToList();

        var returnType = method.ReturnType is not null
            ? ResolveTypeRef(method.ReturnType)
            : PrimitiveType.Void;

        return new FunctionType(paramTypes, returnType);
    }

    private void CheckMethodBody(MethodDecl method, ClassType classType, Scope instanceScope)
    {
        var sig = classType.Methods[method.Name];
        var methodScope = new Scope(instanceScope);

        foreach (var (fieldName, fieldType) in classType.Fields)
            methodScope.Define(fieldName, fieldType);

        for (int i = 0; i < method.Params.Count; i++)
            methodScope.Define(method.Params[i].Name, sig.ParamTypes[i]);

        foreach (var m in classType.Methods)
            methodScope.Define(m.Key, m.Value);
        var prevReturn = _currentReturnType;
        _currentReturnType = sig.ReturnType;
        CheckBlock(method.Body!, methodScope);

        _currentReturnType = prevReturn;
    }

    private void VerifyInterfaceImpl(ClassType classType, InterfaceType iface, AstNode site)
    {
        foreach (var (methodName, ifaceSig) in iface.Methods)
        {
            if (!classType.Methods.TryGetValue(methodName, out var implSig))
            {
                RecordError(
                    $"Class '{classType.Name}' does not implement '{iface.Name}.{methodName}'",
                    site);
                continue;
            }

            if (implSig.ParamTypes.Count != ifaceSig.ParamTypes.Count)
            {
                RecordError(
                    $"'{classType.Name}.{methodName}' has wrong parameter count " +
                    $"(expected {ifaceSig.ParamTypes.Count}, got {implSig.ParamTypes.Count})",
                    site);
                continue;
            }

            for (int i = 0; i < ifaceSig.ParamTypes.Count; i++)
            {
                if (!TypesCompatible(implSig.ParamTypes[i], ifaceSig.ParamTypes[i]))
                    RecordError(
                        $"'{classType.Name}.{methodName}' parameter {i + 1} type mismatch " +
                        $"(expected '{ifaceSig.ParamTypes[i]}', got '{implSig.ParamTypes[i]}')",
                        site);
            }

            if (!TypesCompatible(implSig.ReturnType, ifaceSig.ReturnType))
                RecordError(
                    $"'{classType.Name}.{methodName}' return type mismatch " +
                    $"(expected '{ifaceSig.ReturnType}', got '{implSig.ReturnType}')",
                    site);
        }
    }


    private void CheckInterfaceDecl(InterfaceDecl id, Scope scope)
    {
        var ifaceType = (InterfaceType)_env.Resolve(id.Name)!;

        var ifaceScope = new Scope(scope);
        foreach (var tp in id.TypeParams)
            ifaceScope.Define(tp, new TypeParamType(tp));

        foreach (var method in id.Methods)
        {
            var sig = new FunctionType(
                method.Params.Select(p => ResolveTypeRef(p.Type)).ToList(),
                method.ReturnType is not null ? ResolveTypeRef(method.ReturnType) : PrimitiveType.Void
            );
            ifaceType.Methods[method.Name] = sig;
        }
    }


    private void CheckFunDecl(FunDecl fd, Scope scope)
    {
        var paramTypes = fd.Params.Select(p => ResolveTypeRef(p.Type)).ToList();
        var returnType = fd.ReturnType is not null
            ? ResolveTypeRef(fd.ReturnType)
            : PrimitiveType.Void;

        var funType = new FunctionType(paramTypes, returnType);
        scope.Define(fd.Name, funType);

        var funScope = new Scope(scope);
        for (int i = 0; i < fd.Params.Count; i++)
            funScope.Define(fd.Params[i].Name, paramTypes[i]);

        var prevReturn = _currentReturnType;
        _currentReturnType = returnType;
        CheckBlock(fd.Body, funScope);
        _currentReturnType = prevReturn;
    }


    private MiloType InferExpr(Expr expr, Scope scope)
    {
        try
        {
            return expr switch
            {
                IntLiteralExpr    => PrimitiveType.Int,
                FloatLiteralExpr  => PrimitiveType.Float,
                StringLiteralExpr => PrimitiveType.String,
                BoolLiteralExpr   => PrimitiveType.Bool,
                NullLiteralExpr   => PrimitiveType.Null,
                ListLiteralExpr lle => InferListLiteral(lle, scope),
                IndexExpr ie => InferIndex(ie, scope),

                VariableExpr ve   => InferVariable(ve, scope),
                ThisExpr te       => InferThis(te),
                GroupExpr ge      => InferExpr(ge.Inner, scope),
                UnaryExpr ue      => InferUnary(ue, scope),
                BinaryExpr be     => InferBinary(be, scope),
                AssignExpr ae     => InferAssign(ae, scope),
                SetExpr se        => InferSet(se, scope),
                GetExpr ge        => InferGet(ge, scope),
                CallExpr ce       => InferCall(ce, scope),
                NewExpr ne        => InferNew(ne, scope),
                LambdaExpr le     => InferLambda(le, scope),

                _ => throw new TypeException(
                    $"Unknown expression type '{expr.GetType().Name}'",
                    expr.Line, expr.Column)
            };
        }
        catch (TypeException ex)
        {
            _errors.Add(ex);
            return UnknownType.Instance;
        }
    }

    private MiloType InferListLiteral(ListLiteralExpr lle, Scope scope)
    {
        foreach (var item in lle.Items)
            InferExpr(item, scope);
        return _env.Resolve("List") ?? UnknownType.Instance;
    }
    
    private MiloType InferIndex(IndexExpr ie, Scope scope)
    {
        InferExpr(ie.Object, scope);
        var indexType = InferExpr(ie.Index, scope);
        AssertType(indexType, PrimitiveType.Int, ie.Index, "Index must be Int");
        return UnknownType.Instance;
    }
    
    private MiloType InferVariable(VariableExpr ve, Scope scope)
    {
        var type = scope.Lookup(ve.Name);
        if (type is null)
            throw new TypeException($"Undefined variable '{ve.Name}'", ve.Line, ve.Column);
        return type;
    }

    private MiloType InferThis(ThisExpr te)
    {
        if (_currentClass is null)
            throw new TypeException("'this' used outside of a class", te.Line, te.Column);
        return _currentClass;
    }

    private MiloType InferUnary(UnaryExpr ue, Scope scope)
    {
        var operandType = InferExpr(ue.Operand, scope);

        return ue.Op.Type switch
        {
            TokenType.Bang  => operandType == PrimitiveType.Bool
                ? PrimitiveType.Bool
                : throw new TypeException($"'!' requires Bool, got '{operandType}'", ue.Line, ue.Column),

            TokenType.Minus => operandType == PrimitiveType.Int || operandType == PrimitiveType.Float
                ? operandType
                : throw new TypeException($"Unary '-' requires Int or Float, got '{operandType}'", ue.Line, ue.Column),

            _ => throw new TypeException($"Unknown unary operator '{ue.Op.Lexeme}'", ue.Line, ue.Column)
        };
    }

    private MiloType InferBinary(BinaryExpr be, Scope scope)
    {
        var left  = InferExpr(be.Left, scope);
        var right = InferExpr(be.Right, scope);
        var op    = be.Op.Type;

        if (op is TokenType.Plus or TokenType.Minus or TokenType.Star or TokenType.Slash or TokenType.Percent)
        {
            if (op == TokenType.Plus && left == PrimitiveType.String && right == PrimitiveType.String)
                return PrimitiveType.String;

            if ((left == PrimitiveType.Int   || left == PrimitiveType.Float) &&
                (right == PrimitiveType.Int  || right == PrimitiveType.Float))
            {
                return (left == PrimitiveType.Float || right == PrimitiveType.Float)
                    ? PrimitiveType.Float
                    : PrimitiveType.Int;
            }

            throw new TypeException(
                $"Operator '{be.Op.Lexeme}' cannot be applied to '{left}' and '{right}'",
                be.Line, be.Column);
        }

        if (op is TokenType.Less or TokenType.LessEquals or TokenType.Greater or TokenType.GreaterEquals)
        {
            if ((left == PrimitiveType.Int   || left == PrimitiveType.Float) &&
                (right == PrimitiveType.Int  || right == PrimitiveType.Float))
                return PrimitiveType.Bool;

            throw new TypeException(
                $"Comparison '{be.Op.Lexeme}' requires numeric types, got '{left}' and '{right}'",
                be.Line, be.Column);
        }

        if (op is TokenType.EqualsEquals or TokenType.BangEquals)
        {
            if (!TypesCompatible(left, right) && !TypesCompatible(right, left))
                RecordError(
                    $"Equality check between unrelated types '{left}' and '{right}'",
                    be);
            return PrimitiveType.Bool;
        }

        if (op is TokenType.And or TokenType.Or)
        {
            if (left != PrimitiveType.Bool || right != PrimitiveType.Bool)
                throw new TypeException(
                    $"'{be.Op.Lexeme}' requires Bool operands, got '{left}' and '{right}'",
                    be.Line, be.Column);
            return PrimitiveType.Bool;
        }

        throw new TypeException($"Unknown binary operator '{be.Op.Lexeme}'", be.Line, be.Column);
    }

    private MiloType InferAssign(AssignExpr ae, Scope scope)
    {
        var existing = scope.Lookup(ae.Name);
        if (existing is null)
            throw new TypeException($"Undefined variable '{ae.Name}'", ae.Line, ae.Column);

        var valueType = InferExpr(ae.Value, scope);
        AssertAssignable(valueType, existing, ae.Value);
        return existing;
    }

    private MiloType InferSet(SetExpr se, Scope scope)
    {
        var objType   = InferExpr(se.Object, scope);
        var valueType = InferExpr(se.Value, scope);

        if (objType is ClassType ct)
        {
            if (!ct.Fields.TryGetValue(se.Name, out var fieldType))
                throw new TypeException(
                    $"Class '{ct.Name}' has no field '{se.Name}'", se.Line, se.Column);

            AssertAssignable(valueType, fieldType, se.Value);
            return fieldType;
        }

        throw new TypeException(
            $"Cannot set property on non-class type '{objType}'", se.Line, se.Column);
    }

    private MiloType InferGet(GetExpr ge, Scope scope)
    {
        var objType = InferExpr(ge.Object, scope);

        if (objType is UnknownType)
            return UnknownType.Instance;

        if (objType is ClassType ct)
        {
            if (ct.Fields.TryGetValue(ge.Name, out var ft))  return ft;
            if (ct.Methods.TryGetValue(ge.Name, out var mt)) return mt;
            throw new TypeException(
                $"Class '{ct.Name}' has no member '{ge.Name}'", ge.Line, ge.Column);
        }

        if (objType is EnumType et)
        {
            if (!et.Members.Contains(ge.Name))
                throw new TypeException(
                    $"Enum '{et.Name}' has no member '{ge.Name}'", ge.Line, ge.Column);
            return et;
        }

        throw new TypeException(
            $"Cannot access property '{ge.Name}' on type '{objType}'", ge.Line, ge.Column);
    }

    private MiloType InferCall(CallExpr ce, Scope scope)
    {
        var calleeType = InferExpr(ce.Callee, scope);

        if (calleeType is not FunctionType ft)
            throw new TypeException(
                $"Type '{calleeType}' is not callable", ce.Line, ce.Column);

        if (ce.Arguments.Count != ft.ParamTypes.Count)
            throw new TypeException(
                $"Expected {ft.ParamTypes.Count} argument(s), got {ce.Arguments.Count}",
                ce.Line, ce.Column);

        for (int i = 0; i < ce.Arguments.Count; i++)
        {
            var argType = InferExpr(ce.Arguments[i], scope);
            AssertAssignable(argType, ft.ParamTypes[i], ce.Arguments[i]);
        }

        return ft.ReturnType;
    }

    private MiloType InferNew(NewExpr ne, Scope scope)
    {
        var type = ResolveTypeRef(ne.Type);

        if (type is not ClassType ct)
            throw new TypeException(
                $"'{ne.Type.Name}' is not a class and cannot be instantiated",
                ne.Line, ne.Column);

        if (ct.Methods.TryGetValue("constructor", out var ctor))
        {
            if (ne.Arguments.Count != ctor.ParamTypes.Count)
                throw new TypeException(
                    $"Constructor expects {ctor.ParamTypes.Count} argument(s), got {ne.Arguments.Count}",
                    ne.Line, ne.Column);

            for (int i = 0; i < ne.Arguments.Count; i++)
            {
                var argType = InferExpr(ne.Arguments[i], scope);
                AssertAssignable(argType, ctor.ParamTypes[i], ne.Arguments[i]);
            }
        }

        return ct;
    }

    private MiloType InferLambda(LambdaExpr le, Scope scope)
    {
        var paramTypes = le.Params.Select(p => ResolveTypeRef(p.Type)).ToList();
        var returnType = le.ReturnType is not null
            ? ResolveTypeRef(le.ReturnType)
            : PrimitiveType.Void;

        var lambdaScope = new Scope(scope);
        for (int i = 0; i < le.Params.Count; i++)
            lambdaScope.Define(le.Params[i].Name, paramTypes[i]);

        var prevReturn = _currentReturnType;
        _currentReturnType = returnType;
        CheckBlock(le.Body, lambdaScope);
        _currentReturnType = prevReturn;

        return new FunctionType(paramTypes, returnType);
    }


    private MiloType ResolveTypeRef(TypeRef typeRef)
    {
        var name = typeRef.Name switch
        {
            "void" or "Void" => "Void",
            "Int"            => "Int",
            "Float"          => "Float",
            "String"         => "String",
            "Bool"           => "Bool",
            _                => typeRef.Name
        };

        var resolved = _env.Resolve(name);
        if (resolved is null)
            throw new TypeException(
                $"Unknown type '{typeRef.Name}'", typeRef.Line, typeRef.Column);

        if (typeRef.TypeArgs.Count > 0)
        {
            foreach (var arg in typeRef.TypeArgs)
                ResolveTypeRef(arg); 
        }

        return resolved;
    }


    private bool TypesCompatible(MiloType from, MiloType to)
    {
        if (from == to)                         return true;
        if (from == PrimitiveType.Null)         return true;  
        if (to   == UnknownType.Instance)       return true;  
        if (from == UnknownType.Instance)       return true;  

        if (from == PrimitiveType.Int && to == PrimitiveType.Float) return true;

        if (from is ClassType fromClass)
        {
            var super = fromClass.SuperClass;
            while (super is not null)
            {
                if (super == to) return true;
                super = super.SuperClass;
            }

            foreach (var iface in fromClass.Interfaces)
                if (iface == to) return true;
        }

        if (from is TypeParamType || to is TypeParamType) return true;

        return false;
    }

    private void AssertAssignable(MiloType from, MiloType to, AstNode site)
    {
        if (!TypesCompatible(from, to))
            RecordError($"Cannot assign '{from}' to '{to}'", site);
    }

    private void AssertType(MiloType actual, MiloType expected, AstNode site, string message)
    {
        if (!TypesCompatible(actual, expected))
            RecordError($"{message} (got '{actual}')", site);
    }


    private void RecordError(string message, AstNode site)
    {
        _errors.Add(new TypeException(message, site.Line, site.Column));
    }
}