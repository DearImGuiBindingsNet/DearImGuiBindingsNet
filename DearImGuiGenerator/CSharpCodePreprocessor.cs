namespace DearImguiGenerator;

public class CSharpCodePreprocessor
{
    private readonly List<CSharpConstant> _globalConstants;
    private readonly List<CSharpConstant> _enumConstants;
    private readonly List<CSharpEnum> _enums;
    private readonly List<CSharpDefinition> _typedefs;
    private readonly List<CSharpStruct> _structs;
    private readonly List<CSharpFunction> _functions;
    private readonly List<CSharpDelegate> _delegates;

    private readonly List<CSharpConstant> _resultConstants = [];
    private readonly List<CSharpDelegate> _resultDelegates = [];
    private readonly List<CSharpEnum> _resultEnums = [];
    private readonly List<CSharpStruct> _resultStructs = [];
    private readonly List<CSharpFunction> _resultFunctions = [];

    private readonly List<CSharpTypeReassignment> _typeReassignments = [];

    public List<CSharpStruct> InlineArrays = [];

    public Dictionary<string, List<string>> GeneratedTypeMapping = new();

    public CSharpCodePreprocessor(
        List<CSharpConstant> globalConstants,
        List<CSharpConstant> enumConstants,
        List<CSharpEnum> enums,
        List<CSharpDefinition> typedefs,
        List<CSharpStruct> structs,
        List<CSharpFunction> functions,
        List<CSharpDelegate> delegates
    )
    {
        _globalConstants = globalConstants;
        _enumConstants = enumConstants;
        _enums = enums;
        _typedefs = typedefs;
        _structs = structs;
        _functions = functions;
        _delegates = delegates;

        _typeReassignments = _typedefs
            .Where(x => x.Kind == CSharpDefinitionKind.TypeReassignment)
            .Cast<CSharpTypeReassignment>()
            .ToList();
    }

    private CSharpType? RecursiveTryGetTypeReassignment(CSharpType type)
    {
        if (type.IsPointer)
        {
            var reassign = RecursiveTryGetTypeReassignment(type.InnerType);

            if (reassign is not null)
            {
                return new CSharpPointerType(reassign);
            }
            else
            {
                return reassign;
            }
        }

        var reassignment = _typeReassignments.FirstOrDefault(x => x.Type.GetPrimitiveType() == type.GetPrimitiveType());
        if (reassignment is not null)
        {
            return RecursiveTryGetTypeReassignment(reassignment.AnotherType);
        }
        else
        {
            return type;
        }
    }

    public void Preprocess()
    {
        var typedefDelegates = _typedefs.Where(x => x.Kind == CSharpDefinitionKind.Delegate)
            .Cast<CSharpDelegate>();

        _delegates.AddRange(typedefDelegates);

        _typedefs.RemoveAll(x => x.Kind == CSharpDefinitionKind.Delegate);

        foreach (var sEnum in _enums)
        {
            sEnum.Modifiers.Add("public");
            if (sEnum.Name.EndsWith('_'))
            {
                sEnum.Name = sEnum.Name[..^1];
            }
        }

        foreach (var sConst in _enumConstants)
        {
            sConst.Modifiers.Add("public");
        }

        // flatten inner declarations in structs
        foreach (var sStruct in _structs)
        {
            sStruct.Modifiers.Add("public");
            foreach (var cSharpDefinition in sStruct.InnerDeclarations)
            {
                if (cSharpDefinition.Kind == CSharpDefinitionKind.Delegate)
                {
                    _delegates.Add((CSharpDelegate) cSharpDefinition);
                }
            }

            sStruct.InnerDeclarations.RemoveAll(x => x.Kind == CSharpDefinitionKind.Delegate);

            foreach (var sField in sStruct.Fields)
            {
                sField.Modifiers.Add("public");

                var redefinedType = RecursiveTryGetTypeReassignment(sField.Type);
                if (redefinedType is not null && redefinedType != sField.Type)
                {
                    sField.PrecedingComment ??= [..sField.PrecedingComment ?? [], $"Original type: {sField.Type.ToCSharpCode()}"];
                    sField.Type = redefinedType;
                }

                if (sField.Type.IsPointer)
                {
                    sField.Modifiers.Add("unsafe");
                }

                if (sField.IsArray)
                {
                    if (IsSimpleType(sField.Type.GetPrimitiveType()))
                    {
                        sField.Modifiers.Add("unsafe");
                        sField.Modifiers.Add("fixed");
                        
                        var globalConstant = _globalConstants.FirstOrDefault(x => x.Name == sField.ArrayBound);
                        var enumConstant = _enumConstants.FirstOrDefault(x => x.Name == sField.ArrayBound);

                        // if there is no global constant with the provided name - then it's not an int
                        // if there is a global constant with the provided name, but it's type is not int - then it's not an int
                        bool needsCastToInt = globalConstant is null || globalConstant.Type.GetPrimitiveType() != "int";

                        // if there is a enum constant (e.g. enum value) with the given name - then the bound doesn't require a cast
                        if (enumConstant is not null)
                        {
                            needsCastToInt = false;
                        }

                        // if it's a number - just use it as is
                        if (long.TryParse(sField.ArrayBound, out _))
                        {
                            needsCastToInt = false;
                        }

                        string bound;
                        if (needsCastToInt)
                        {
                            bound = "(int)(" + sField.ArrayBound + ")";
                        }
                        else
                        {
                            bound = sField.ArrayBound;
                        }

                        sField.ArrayBound = bound;
                    }
                    else
                    {
                        var inlineArrayType = sStruct.Name + "_" + sField.Name + "InlineArray";

                        var inlineArray = new CSharpStruct(inlineArrayType);

                        if (long.TryParse(sField.ArrayBound, out _))
                        {
                            inlineArray.Attributes.Add($"InlineArray({sField.ArrayBound})");
                        }
                        else
                        {
                            var globalConstant = _globalConstants.FirstOrDefault(x => x.Name == sField.ArrayBound);
                            var enumConstant = _enumConstants.FirstOrDefault(x => x.Name == sField.ArrayBound);

                            // if there is no global constant with the provided name - then it's not an int
                            // if there is a global constant with the provided name, but it's type is not int - then it's not an int
                            bool needsCastToInt = globalConstant is null || globalConstant.Type.GetPrimitiveType() != "int";

                            // if there is a enum constant (e.g. enum value) with the given name - then the bound doesn't require a cast
                            if (enumConstant is not null)
                            {
                                needsCastToInt = false;
                            }

                            string bound;
                            if (needsCastToInt)
                            {
                                bound = "(int)(" + sField.ArrayBound + ")";
                            }
                            else
                            {
                                bound = sField.ArrayBound;
                            }
                            inlineArray.Attributes.Add($"InlineArray({bound})");
                        }

                        inlineArray.Modifiers.Add("public");
                        inlineArray.Fields.Add(new CSharpTypedVariable("Element", sField.Type));
                        inlineArray.PrecedingComment = [$"InlineArray of {sStruct.Name}'s field \"{sField.Name}\" of {sField.ArrayBound} elements"];

                        InlineArrays.Add(inlineArray);

                        sField.Type = new CSharpPrimitiveType(inlineArrayType);
                        sField.IsArray = false;
                    }
                }
            }
        }

        foreach (var sDelegate in _delegates)
        {
            sDelegate.Attributes.Add("UnmanagedFunctionPointer(CallingConvention.Cdecl)");
            sDelegate.Modifiers.Add("public");
            if (sDelegate.ReturnType.IsPointer || sDelegate.Arguments.Any(x => x.Type.IsPointer))
            {
                sDelegate.Modifiers.Add("unsafe");
            }
        }

        _functions.RemoveAll(x => x.Arguments.Any(y => y.Type.GetPrimitiveType() == "__arglist"));
        
        foreach (var sFunc in _functions)
        {
            sFunc.Modifiers.Add("public");
            sFunc.Modifiers.Add("static");
            sFunc.Modifiers.Add("extern");
            
            var returnTypeRedefinedType = RecursiveTryGetTypeReassignment(sFunc.ReturnType);
            if (returnTypeRedefinedType is not null && returnTypeRedefinedType != sFunc.ReturnType)
            {
                sFunc.PrecedingComment = [..sFunc.PrecedingComment ?? [], $"ReturnType original type: {sFunc.ReturnType.ToCSharpCode()}"];
                sFunc.ReturnType = returnTypeRedefinedType;
            }
            
            if (sFunc.ReturnType.IsPointer)
            {
                sFunc.Modifiers.Add("unsafe");
            }
            
            sFunc.Attributes.Add("DllImport(\"cimgui\", CallingConvention = CallingConvention.Cdecl)");
            
            foreach (var sArg in sFunc.Arguments)
            {
                var redefinedType = RecursiveTryGetTypeReassignment(sArg.Type);
                if (redefinedType is not null && redefinedType != sArg.Type)
                {
                    sFunc.PrecedingComment = [..sFunc.PrecedingComment ?? [], $"Param {sArg.Name} original type: {sArg.Type.ToCSharpCode()}"];
                    sArg.Type = redefinedType;
                }

                if (sArg.Type.IsPointer && sFunc.Modifiers[^1] != "unsafe")
                {
                    sFunc.Modifiers.Add("unsafe");
                }
            }
        }
    }

    private static bool IsSimpleType(string type)
    {
        return type switch {
            "int" or "long" or "uint" or "byte" or "ushort" or "short" or "sbyte" or "ulong" or "float" or "bool" or "double" => true,
            _ when type.EndsWith('*') => true,
            _ => false
        };
    }
}