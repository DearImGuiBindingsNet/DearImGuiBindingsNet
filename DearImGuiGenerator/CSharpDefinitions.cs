using System.Diagnostics.CodeAnalysis;

namespace DearImguiGenerator;

public abstract record CSharpDefinition(string Name, CSharpDefinitionKind Kind)
{
    public string Name { get; set; } = Name;
    
    /// <summary>
    /// Raw modifiers ex: public, static, unsafe etc.
    /// </summary>
    public List<string> Modifiers { get; set; } = [];
    
    /// <summary>
    /// Raw attributes ex: Flags, DllImport(CallingConvention.Cdecl) etc.
    /// </summary>
    public List<string> Attributes { get; set; } = [];

    // something like "hello" -> "// hello"
    public string? TrailingComment { get; set; }
    
    // something like:
    // "// hello"
    // "{this_definition}"
    public string[]? PrecedingComment { get; set; }
}

public record CSharpEnum(string Name) : CSharpDefinition(Name, CSharpDefinitionKind.Enum)
{
    public List<CSharpNamedValue> Values { get; private set; } = [];

    public override string ToString()
    {
        return $"enum {Name}";
    }

    public void Write(CSharpCodeWriter writer)
    {
        writer.WriteLine($"enum {Name}");
        writer.PushBlock();
        foreach (var value in Values)
        {
            value.Write(writer);
        }
        writer.PopBlock();
    }
}

public abstract record CSharpContainerType(string Name, CSharpDefinitionKind Kind) : CSharpDefinition(Name, Kind)
{
    public List<CSharpTypedVariable> Fields { get; private set; } = [];

    public List<CSharpDefinition> InnerDeclarations { get; private set; } = [];
}

public record CSharpStruct(string Name) : CSharpContainerType(Name, CSharpDefinitionKind.Struct)
{
    public override string ToString()
    {
        return $"struct {Name}";
    }
}

public record CSharpClass(string Name) : CSharpContainerType(Name, CSharpDefinitionKind.Class)
{
    public override string ToString()
    {
        return $"class {Name}";
    }
}

public record CSharpFunction(string Name, CSharpType ReturnType) : CSharpDefinition(Name, CSharpDefinitionKind.Function)
{
    public List<CSharpArgument> Arguments { get; private set; } = [];

    public CSharpType ReturnType { get; set; } = ReturnType;
    
    public override string ToString()
    {
        return $"{ReturnType} {Name}({string.Join(", ", Arguments)})";
    }
}

public record CSharpDelegate(string Name, CSharpType ReturnType) : CSharpDefinition(Name, CSharpDefinitionKind.Delegate)
{
    public List<CSharpArgument> Arguments { get; private set; } = [];
    
    public override string ToString()
    {
        return $"delegate {ReturnType} {Name}({string.Join(", ", Arguments)})";
    }
}

public record CSharpTypedVariable(string Name, CSharpType Type, bool IsArray = false, string ArrayBound = "") : CSharpDefinition(Name, CSharpDefinitionKind.Variable)
{
    public CSharpType Type { get; set; } = Type;
    public bool IsArray { get; set; } = IsArray;

    public string ArrayBound { get; set; } = ArrayBound;
    
    public override string ToString()
    {
        return $"{Type} {Name}{(IsArray ? $"[{ArrayBound}]" : "")}";
    }
}

public record CSharpArgument(string Name, CSharpType Type, bool IsArray = false, string ArrayBound = "") : CSharpDefinition(Name, CSharpDefinitionKind.Variable)
{
    public CSharpType Type { get; set; } = Type;
    public bool IsArray { get; set; } = IsArray;

    public string ArrayBound { get; set; } = ArrayBound;
    
    public override string ToString()
    {
        return $"{Type} {Name}{(IsArray ? $"[{ArrayBound}]" : "")}";
    }
}

public record CSharpNamedValue(string Name, string Value) : CSharpDefinition(Name, CSharpDefinitionKind.NamedValue)
{
    public override string ToString()
    {
        return $"{Name} = {Value}";
    }

    public void Write(CSharpCodeWriter writer)
    {
        writer.WriteLine($"{Name} = {Value},");
    }
}

public record CSharpConstant(string Name, CSharpType Type, string Value) : CSharpTypedVariable(Name, Type)
{
    public override string ToString()
    {
        return $"{Type} {Name} = {Value}";
    }
}

public record CSharpTypeReassignment(CSharpType Type, CSharpType AnotherType) : CSharpDefinition("reassignment", CSharpDefinitionKind.TypeReassignment)
{
    public override string ToString()
    {
        return $"{Name} = {AnotherType}";
    }
}

public abstract record CSharpType()
{
    public abstract string GetPrimitiveType();

    public abstract string ToCSharpCode();
    
    public abstract bool IsPointer { get; }

    public abstract CSharpType InnerType { get; protected set; }
};

public record CSharpPrimitiveType(string Type) : CSharpType()
{
    public override string GetPrimitiveType()
    {
        return Type;
    }

    public override string ToCSharpCode()
    {
        return Type;
    }

    public override bool IsPointer { get; } = false;
    public override CSharpType InnerType
    {
        get => throw new InvalidOperationException("No inner type on primitive");
        protected set => throw new InvalidOperationException("No inner type can be set to a primitive");
    }
}

public record CSharpPointerType : CSharpType
{
    public override CSharpType InnerType { get; protected set; }

    public override string ToCSharpCode()
    {
        return InnerType.ToCSharpCode() + "*";
    }

    public override bool IsPointer { get; } = true;

    public CSharpPointerType(CSharpType innerType)
    {
        InnerType = innerType;
    }

    public override string GetPrimitiveType()
    {
        return InnerType.GetPrimitiveType();
    }
}

public enum CSharpDefinitionKind
{
    Namespace,
    Class,
    Struct,
    Field,
    Enum,
    Function,
    Variable,
    NamedValue,
    TypeReassignment,
    Delegate
}