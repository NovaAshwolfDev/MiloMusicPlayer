using System;
using System.Collections.Generic;
using System.Linq;

namespace MiloMusicPlayer.Scripting;

public abstract class MiloValue
{
    public abstract string Display();
}

public sealed class IntValue : MiloValue
{
    public int Value { get; }
    public IntValue(int v) { Value = v; }
    public override string Display() => Value.ToString();
}

public sealed class FloatValue : MiloValue
{
    public float Value { get; }
    public FloatValue(float v) { Value = v; }
    public override string Display() => Value.ToString("G");
}

public sealed class StringValue : MiloValue
{
    public string Value { get; }
    public StringValue(string v) { Value = v; }
    public override string Display() => Value;
}

public sealed class BoolValue : MiloValue
{
    public bool Value { get; }
    public static readonly BoolValue True  = new(true);
    public static readonly BoolValue False = new(false);
    public BoolValue(bool v) { Value = v; }
    public override string Display() => Value ? "true" : "false";
}

public sealed class NullValue : MiloValue
{
    public static readonly NullValue Instance = new();
    private NullValue() { }
    public override string Display() => "null";
}

public sealed class InstanceValue : MiloValue
{
    public ClassType Type { get; }
    public Dictionary<string, MiloValue> Fields { get; } = new();
    public InstanceValue(ClassType type) { Type = type; }
    public override string Display() => $"<{Type.Name}>";
}

public sealed class FunctionValue : MiloValue
{
    public string          Name      { get; }
    public List<Parameter> Params    { get; }
    public BlockStmt       Body      { get; }
    public RuntimeScope    Closure   { get; }
    public InstanceValue?  BoundThis { get; }

    public FunctionValue(string name, List<Parameter> parms, BlockStmt body, RuntimeScope closure, InstanceValue? boundThis = null)
    {
        Name      = name;
        Params    = parms;
        Body      = body;
        Closure   = closure;
        BoundThis = boundThis;
    }

    public override string Display() => $"<fun {Name}>";
}

public sealed class NativeFunctionValue : MiloValue
{
    public string                           Name { get; }
    public Func<List<MiloValue>, MiloValue> Call { get; }

    public NativeFunctionValue(string name, Func<List<MiloValue>, MiloValue> call)
    {
        Name = name;
        Call = call;
    }

    public override string Display() => $"<native {Name}>";
}

public sealed class EnumValue : MiloValue
{
    public EnumType Type   { get; }
    public string   Member { get; }

    public EnumValue(EnumType type, string member)
    {
        Type   = type;
        Member = member;
    }

    public override string Display() => $"{Type.Name}.{Member}";
}

public sealed class ListValue : MiloValue
{
    public List<MiloValue> Items { get; } = new();
    public override string Display() => $"[{string.Join(", ", Items.Select(i => i.Display()))}]";
}

public sealed class InstanceFactory : MiloValue
{
    public string                               TypeName  { get; }
    public Func<List<MiloValue>, InstanceValue> Construct { get; }

    public InstanceFactory(string typeName, Func<List<MiloValue>, InstanceValue> construct)
    {
        TypeName  = typeName;
        Construct = construct;
    }

    public override string Display() => $"<class {TypeName}>";
}

public sealed class RuntimeScope
{
    private readonly Dictionary<string, MiloValue> _bindings = new();
    public RuntimeScope? Parent { get; }

    public RuntimeScope(RuntimeScope? parent = null) { Parent = parent; }

    public void Define(string name, MiloValue value) => _bindings[name] = value;

    public MiloValue Get(string name)
    {
        if (_bindings.TryGetValue(name, out var v)) return v;
        if (Parent is not null) return Parent.Get(name);
        throw new RuntimeException($"Undefined variable '{name}'", 0, 0);
    }

    public void Set(string name, MiloValue value)
    {
        if (_bindings.ContainsKey(name)) { _bindings[name] = value; return; }
        if (Parent is not null)          { Parent.Set(name, value); return; }
        throw new RuntimeException($"Undefined variable '{name}'", 0, 0);
    }

    public bool IsDefined(string name) =>
        _bindings.ContainsKey(name) || (Parent?.IsDefined(name) ?? false);
}

internal sealed class ReturnSignal : Exception
{
    public MiloValue Value { get; }
    public ReturnSignal(MiloValue value) : base() { Value = value; }
}

public sealed class RuntimeException : Exception
{
    public int Line   { get; }
    public int Column { get; }

    public RuntimeException(string message, int line, int column)
        : base($"[{line}:{column}] Runtime error: {message}")
    {
        Line   = line;
        Column = column;
    }
}

public sealed partial class Interpreter
{
    private readonly RuntimeScope _globalScope;

    public Interpreter(RuntimeScope globalScope)
    {
        _globalScope = globalScope;
    }

    public void Execute(ScriptFile file)
    {
        foreach (var stmt in file.Statements)
            ExecStmt(stmt, _globalScope);
    }

    private void ExecStmt(Stmt stmt, RuntimeScope scope)
    {
        switch (stmt)
        {
            case ImportStmt:
                break;
            case VarDeclStmt vd:
                ExecVarDecl(vd, scope);
                break;
            case ExprStmt es:
                EvalExpr(es.Expression, scope);
                break;
            case BlockStmt block:
                ExecBlock(block, scope);
                break;
            case ReturnStmt rs:
                ExecReturn(rs, scope);
                break;
            case IfStmt ifs:
                ExecIf(ifs, scope);
                break;
            case WhileStmt ws:
                ExecWhile(ws, scope);
                break;
            case ForInStmt fis:
                ExecForIn(fis, scope);
                break;
            case FunDecl fd:
                ExecFunDecl(fd, scope);
                break;
            case ClassDecl cd:
                ExecClassDecl(cd, scope);
                break;
            case InterfaceDecl:
            case EnumDecl:
                break;
            default:
                throw new RuntimeException($"Cannot execute statement '{stmt.GetType().Name}'", stmt.Line, stmt.Column);
        }
    }

    private void ExecBlock(BlockStmt block, RuntimeScope parent)
    {
        var inner = new RuntimeScope(parent);
        foreach (var s in block.Statements)
            ExecStmt(s, inner);
    }

    private void ExecVarDecl(VarDeclStmt vd, RuntimeScope scope)
    {
        var value = vd.Initializer is not null ? EvalExpr(vd.Initializer, scope) : NullValue.Instance;
        scope.Define(vd.Name, value);
    }

    private void ExecReturn(ReturnStmt rs, RuntimeScope scope)
    {
        var value = rs.Value is not null ? EvalExpr(rs.Value, scope) : NullValue.Instance;
        throw new ReturnSignal(value);
    }

    private void ExecIf(IfStmt ifs, RuntimeScope scope)
    {
        var cond = EvalExpr(ifs.Condition, scope);
        if (IsTruthy(cond))
            ExecStmt(ifs.Then, scope);
        else if (ifs.Else is not null)
            ExecStmt(ifs.Else, scope);
    }

    private void ExecWhile(WhileStmt ws, RuntimeScope scope)
    {
        while (IsTruthy(EvalExpr(ws.Condition, scope)))
            ExecStmt(ws.Body, scope);
    }

    private void ExecForIn(ForInStmt fis, RuntimeScope scope)
    {
        var iterable = EvalExpr(fis.Iterable, scope);
        if (iterable is not ListValue list)
            throw new RuntimeException($"Cannot iterate over non-list value", fis.Line, fis.Column);

        foreach (var item in list.Items)
        {
            var inner = new RuntimeScope(scope);
            inner.Define(fis.VarName, item);
            ExecStmt(fis.Body, inner);
        }
    }

    private void ExecFunDecl(FunDecl fd, RuntimeScope scope)
    {
        var fn = new FunctionValue(fd.Name, fd.Params, fd.Body, scope);
        scope.Define(fd.Name, fn);
    }

    private void ExecClassDecl(ClassDecl cd, RuntimeScope scope)
    {
        var classType = new ClassType(cd.Name);

        scope.Define(cd.Name, new InstanceFactory(cd.Name, args =>
        {
            var inst = new InstanceValue(classType);

            foreach (var field in cd.Fields)
            {
                var value = field.Initializer is not null
                    ? EvalExpr(field.Initializer, scope)
                    : NullValue.Instance;
                inst.Fields[field.Name] = value;
            }

            foreach (var method in cd.Methods)
            {
                if (method.Body is null) continue;
                var fn = new FunctionValue(method.Name, method.Params, method.Body, scope, inst);
                inst.Fields[$"__method_{method.Name}"] = fn;
            }

            if (inst.Fields.TryGetValue("__method_constructor", out var ctorVal)
                && ctorVal is FunctionValue ctor)
            {
                CallFunction(ctor, args, cd);
            }

            return inst;
        }));
    }

    public MiloValue EvalExpr(Expr expr, RuntimeScope scope)
    {
        return expr switch
        {
            IntLiteralExpr    ile => new IntValue(ile.Value),
            FloatLiteralExpr  fle => new FloatValue(fle.Value),
            StringLiteralExpr sle => new StringValue(sle.Value),
            BoolLiteralExpr   ble => ble.Value ? BoolValue.True : BoolValue.False,
            NullLiteralExpr       => NullValue.Instance,

            VariableExpr ve => scope.Get(ve.Name),
            ThisExpr        => scope.Get("this"),
            GroupExpr ge    => EvalExpr(ge.Inner, scope),
            AssignExpr ae   => EvalAssign(ae, scope),
            SetExpr se      => EvalSet(se, scope),
            GetExpr ge      => EvalGet(ge, scope),
            CallExpr ce     => EvalCall(ce, scope),
            NewExpr ne      => EvalNew(ne, scope),
            UnaryExpr ue    => EvalUnary(ue, scope),
            BinaryExpr be   => EvalBinary(be, scope),
            LambdaExpr le   => EvalLambda(le, scope),

            _ => throw new RuntimeException($"Unknown expression '{expr.GetType().Name}'", expr.Line, expr.Column)
        };
    }

    private MiloValue EvalAssign(AssignExpr ae, RuntimeScope scope)
    {
        var value = EvalExpr(ae.Value, scope);
        scope.Set(ae.Name, value);
        return value;
    }

    private MiloValue EvalSet(SetExpr se, RuntimeScope scope)
    {
        var obj = EvalExpr(se.Object, scope);
        if (obj is not InstanceValue inst)
            throw new RuntimeException($"Cannot set property on non-instance value", se.Line, se.Column);

        var value = EvalExpr(se.Value, scope);
        inst.Fields[se.Name] = value;
        return value;
    }

    private MiloValue EvalGet(GetExpr ge, RuntimeScope scope)
    {
        var obj = EvalExpr(ge.Object, scope);

        if (obj is InstanceValue inst)
        {
            if (inst.Fields.TryGetValue(ge.Name, out var field)) return field;
            if (inst.Fields.TryGetValue($"__method_{ge.Name}", out var method)) return method;
            throw new RuntimeException($"'{inst.Type.Name}' has no member '{ge.Name}'", ge.Line, ge.Column);
        }

        throw new RuntimeException($"Cannot access property '{ge.Name}' on value '{obj.Display()}'", ge.Line, ge.Column);
    }

    private MiloValue EvalCall(CallExpr ce, RuntimeScope scope)
    {
        var callee = EvalExpr(ce.Callee, scope);
        var args   = ce.Arguments.Select(a => EvalExpr(a, scope)).ToList();

        return callee switch
        {
            FunctionValue fn       => CallFunction(fn, args, ce),
            NativeFunctionValue nf => nf.Call(args),
            _ => throw new RuntimeException($"'{callee.Display()}' is not callable", ce.Line, ce.Column)
        };
    }

    internal MiloValue CallFunction(FunctionValue fn, List<MiloValue> args, AstNode site)
    {
        if (args.Count != fn.Params.Count)
            throw new RuntimeException($"'{fn.Name}' expects {fn.Params.Count} argument(s), got {args.Count}", site.Line, site.Column);

        var callScope = new RuntimeScope(fn.Closure);

        if (fn.BoundThis is not null)
        {
            callScope.Define("this", fn.BoundThis);

            foreach (var (fieldName, fieldValue) in fn.BoundThis.Fields)
                if (!fieldName.StartsWith("__method_"))
                    callScope.Define(fieldName, fieldValue);
        }

        for (int i = 0; i < fn.Params.Count; i++)
            callScope.Define(fn.Params[i].Name, args[i]);

        MiloValue result = NullValue.Instance;
        try
        {
            ExecBlock(fn.Body, callScope);
        }
        catch (ReturnSignal rs)
        {
            result = rs.Value;
        }
        finally
        {
            if (fn.BoundThis is not null)
            {
                foreach (var fieldName in fn.BoundThis.Fields.Keys.ToList())
                {
                    if (!fieldName.StartsWith("__method_") && callScope.IsDefined(fieldName))
                        fn.BoundThis.Fields[fieldName] = callScope.Get(fieldName);
                }
            }
        }

        return result;
    }

    private MiloValue EvalNew(NewExpr ne, RuntimeScope scope)
    {
        var typeName = ne.Type.Name;

        if (!scope.IsDefined(typeName))
            throw new RuntimeException($"Unknown class '{typeName}'", ne.Line, ne.Column);

        var ctor = scope.Get(typeName);

        if (ctor is InstanceFactory factory)
        {
            var args = ne.Arguments.Select(a => EvalExpr(a, scope)).ToList();
            return factory.Construct(args);
        }

        throw new RuntimeException($"'{typeName}' is not instantiable", ne.Line, ne.Column);
    }

    private MiloValue EvalUnary(UnaryExpr ue, RuntimeScope scope)
    {
        var operand = EvalExpr(ue.Operand, scope);

        return ue.Op.Type switch
        {
            TokenType.Bang => operand is BoolValue b
                ? (b.Value ? BoolValue.False : BoolValue.True)
                : throw new RuntimeException("'!' requires a Bool", ue.Line, ue.Column),

            TokenType.Minus => operand switch
            {
                IntValue   iv => new IntValue(-iv.Value),
                FloatValue fv => new FloatValue(-fv.Value),
                _ => throw new RuntimeException("Unary '-' requires Int or Float", ue.Line, ue.Column)
            },

            _ => throw new RuntimeException($"Unknown unary operator '{ue.Op.Lexeme}'", ue.Line, ue.Column)
        };
    }

    private MiloValue EvalBinary(BinaryExpr be, RuntimeScope scope)
    {
        var left = EvalExpr(be.Left, scope);

        if (be.Op.Type == TokenType.And)
        {
            if (!IsTruthy(left)) return BoolValue.False;
            return IsTruthy(EvalExpr(be.Right, scope)) ? BoolValue.True : BoolValue.False;
        }
        if (be.Op.Type == TokenType.Or)
        {
            if (IsTruthy(left)) return BoolValue.True;
            return IsTruthy(EvalExpr(be.Right, scope)) ? BoolValue.True : BoolValue.False;
        }

        var right = EvalExpr(be.Right, scope);

        return be.Op.Type switch
        {
            TokenType.Plus => (left, right) switch
            {
                (IntValue    l, IntValue    r) => new IntValue(l.Value + r.Value),
                (FloatValue  l, FloatValue  r) => new FloatValue(l.Value + r.Value),
                (IntValue    l, FloatValue  r) => new FloatValue(l.Value + r.Value),
                (FloatValue  l, IntValue    r) => new FloatValue(l.Value + r.Value),
                (StringValue l, StringValue r) => new StringValue(l.Value + r.Value),
                _ => throw new RuntimeException($"Cannot add '{left.Display()}' and '{right.Display()}'", be.Line, be.Column)
            },
            TokenType.Minus        => NumericOp(left, right, (a, b) => a - b, (a, b) => a - b, be),
            TokenType.Star         => NumericOp(left, right, (a, b) => a * b, (a, b) => a * b, be),
            TokenType.Slash        => right switch
            {
                IntValue   rv when rv.Value == 0 => throw new RuntimeException("Division by zero", be.Line, be.Column),
                FloatValue fv when fv.Value == 0 => throw new RuntimeException("Division by zero", be.Line, be.Column),
                _ => NumericOp(left, right, (a, b) => a / b, (a, b) => a / b, be)
            },
            TokenType.Percent       => NumericOp(left, right, (a, b) => a % b, (a, b) => a % b, be),
            TokenType.Less          => NumericCmp(left, right, (a, b) => a < b,  (a, b) => a < b,  be),
            TokenType.LessEquals    => NumericCmp(left, right, (a, b) => a <= b, (a, b) => a <= b, be),
            TokenType.Greater       => NumericCmp(left, right, (a, b) => a > b,  (a, b) => a > b,  be),
            TokenType.GreaterEquals => NumericCmp(left, right, (a, b) => a >= b, (a, b) => a >= b, be),
            TokenType.EqualsEquals  => ValuesEqual(left, right) ? BoolValue.True : BoolValue.False,
            TokenType.BangEquals    => !ValuesEqual(left, right) ? BoolValue.True : BoolValue.False,
            _ => throw new RuntimeException($"Unknown operator '{be.Op.Lexeme}'", be.Line, be.Column)
        };
    }

    private MiloValue EvalLambda(LambdaExpr le, RuntimeScope scope)
    {
        return new FunctionValue("<lambda>", le.Params, le.Body, scope);
    }

    private static bool IsTruthy(MiloValue value) => value switch
    {
        BoolValue b => b.Value,
        NullValue   => false,
        _           => true
    };

    private static bool ValuesEqual(MiloValue a, MiloValue b) => (a, b) switch
    {
        (NullValue,      NullValue)      => true,
        (IntValue    l,  IntValue    r)  => l.Value == r.Value,
        (FloatValue  l,  FloatValue  r)  => l.Value == r.Value,
        (IntValue    l,  FloatValue  r)  => l.Value == r.Value,
        (FloatValue  l,  IntValue    r)  => l.Value == r.Value,
        (StringValue l,  StringValue r)  => l.Value == r.Value,
        (BoolValue   l,  BoolValue   r)  => l.Value == r.Value,
        (EnumValue   l,  EnumValue   r)  => l.Type == r.Type && l.Member == r.Member,
        _ => ReferenceEquals(a, b)
    };

    private static MiloValue NumericOp(MiloValue left, MiloValue right, Func<int, int, int> intOp, Func<float, float, float> floatOp, BinaryExpr be)
    {
        return (left, right) switch
        {
            (IntValue   l, IntValue   r) => new IntValue(intOp(l.Value, r.Value)),
            (FloatValue l, FloatValue r) => new FloatValue(floatOp(l.Value, r.Value)),
            (IntValue   l, FloatValue r) => new FloatValue(floatOp(l.Value, r.Value)),
            (FloatValue l, IntValue   r) => new FloatValue(floatOp(l.Value, r.Value)),
            _ => throw new RuntimeException($"Operator '{be.Op.Lexeme}' requires numeric operands", be.Line, be.Column)
        };
    }

    private static MiloValue NumericCmp(MiloValue left, MiloValue right, Func<int, int, bool> intCmp, Func<float, float, bool> floatCmp, BinaryExpr be)
    {
        bool result = (left, right) switch
        {
            (IntValue   l, IntValue   r) => intCmp(l.Value, r.Value),
            (FloatValue l, FloatValue r) => floatCmp(l.Value, r.Value),
            (IntValue   l, FloatValue r) => floatCmp(l.Value, r.Value),
            (FloatValue l, IntValue   r) => floatCmp(l.Value, r.Value),
            _ => throw new RuntimeException($"Comparison '{be.Op.Lexeme}' requires numeric operands", be.Line, be.Column)
        };
        return result ? BoolValue.True : BoolValue.False;
    }
}

public partial class Interpreter
{
    internal void EvalHookStmt(Stmt stmt, RuntimeScope scope) => ExecStmt(stmt, scope);
}