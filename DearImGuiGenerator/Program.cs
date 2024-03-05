using System.Globalization;
using System.Text.Json;
using DearImguiGenerator;

Dictionary<string, string> knownDefines = new()
{
    // ["CIMGUI_API"] = "extern \"C\" __declspec(dllexport)",
    ["IMGUI_DISABLE_OBSOLETE_FUNCTIONS"] = "",
    ["IMGUI_DISABLE_OBSOLETE_KEYIO"] = ""
};

using var fs = new FileStream("../cimgui/cimgui.json", FileMode.Open);

var definitions = JsonSerializer.Deserialize<Definitions>(
    fs,
    new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    }
);

Dictionary<string, string> cppToSharpKnownConversions = new()
{
    ["int"] = "int",
    ["unsigned int"] = "uint",
    ["unsigned char"] = "byte",
    ["unsigned_char"] = "byte",
    ["unsigned_int"] = "uint",
    ["unsigned_short"] = "ushort",
    ["long long"] = "long",
    ["long_long"] = "long",
    ["unsigned_long_long"] = "ulong",
    ["short"] = "short",
    ["signed char"] = "sbyte",
    ["signed short"] = "short",
    ["signed int"] = "int",
    ["signed long long"] = "long",
    ["unsigned long long"] = "ulong",
    ["unsigned short"] = "ushort",
    ["float"] = "float",
    ["bool"] = "bool",
    ["char"] = "byte",
    ["double"] = "double",
    ["void"] = "void",
    ["va_list"] = "__arglist", // special case
    ["size_t"] = "ulong" // assume only x64 for now
};

Console.WriteLine("---------------------");
Console.WriteLine("Writing Defines");
Console.WriteLine("---------------------");
var constants = WriteDefines(definitions!.Defines);

Console.WriteLine("---------------------");
Console.WriteLine("Writing EnumsRaw");
Console.WriteLine("---------------------");
var enumsConstants = WriteEnumsRaw(definitions.Enums);

Console.WriteLine("---------------------");
Console.WriteLine("Writing Enums");
Console.WriteLine("---------------------");
var enums = WriteEnums(definitions.Enums);

Console.WriteLine("---------------------");
Console.WriteLine("Writing Typedefs");
Console.WriteLine("---------------------");
var typedefs = WriteTypedefs2(definitions.Typedefs);

Console.WriteLine("---------------------");
Console.WriteLine("Writing Structs");
Console.WriteLine("---------------------");
var structs = WriteStructs(definitions.Structs);

Console.WriteLine("---------------------");
Console.WriteLine("Writing Functions");
Console.WriteLine("---------------------");
var (functions, delegates) = WriteFunctions(definitions.Functions);

Console.WriteLine("---------------------");
Console.WriteLine("Done");
Console.WriteLine("---------------------");

var preprocessor = new CSharpCodePreprocessor(
    constants,
    enumsConstants,
    enums,
    typedefs,
    structs,
    functions,
    delegates
);

preprocessor.Preprocess();

var codeWriter = new CSharpCodeWriter();

codeWriter.WriteConsts(constants.Concat(enumsConstants));

codeWriter.Flush();

codeWriter.WriteEnums(enums);

codeWriter.Flush();

codeWriter.WriteStructs(structs);

codeWriter.Flush();

codeWriter.WriteDelegates(delegates);

codeWriter.Flush();

codeWriter.WriteInlineArrays(preprocessor.InlineArrays);

codeWriter.Flush();

codeWriter.WriteFunctions(functions);

codeWriter.Flush();

void AttachComments(Comments? comments, CSharpDefinition definition)
{
    string? trailingComment = null;
    string[]? precedingComment = null;
    if (comments?.Attached is not null)
    {
        trailingComment = RemovePrecedingSlashes(comments.Attached);
    }

    if (comments?.Preceding is not null)
    {
        precedingComment = TrimPreceding(comments.Preceding);
    }

    definition.TrailingComment = trailingComment;
    definition.PrecedingComment = precedingComment;
}

bool EvalConditionals(List<ConditionalItem>? conditionals)
{
    if (conditionals is {Count: > 0})
    {
        if (conditionals.Count == 1)
        {
            var condition = conditionals[0];
            return ((condition.Condition == "ifdef" && knownDefines.ContainsKey(condition.Expression)) ||
                    (condition.Condition == "ifndef" && !knownDefines.ContainsKey(condition.Expression)) ||
                    (condition.Condition == "if" && condition.Expression.StartsWith("defined") && !condition.Expression.StartsWith("&&") && 
                     knownDefines.ContainsKey(condition.Expression.Substring(8, condition.Expression.Length - 8 - 1))));
        }
        else
        {
            var condition = conditionals[1];
            return ((condition.Condition == "ifdef" && knownDefines.ContainsKey(condition.Expression)) ||
                    (condition.Condition == "ifndef" && !knownDefines.ContainsKey(condition.Expression)));
        }
    }
    else
    {
        return true;
    }
}

List<CSharpConstant> WriteDefines(List<DefineItem> defines)
{
    // dear_bindings writes defines in a strange manner, producing redefines, so when we group them by count, we can produce more accurate result
    var defineGroups = defines.GroupBy(x => x.Conditionals?.Count ?? 0);

    List<CSharpConstant> cSharpConstants = [];
    
    foreach (var key in knownDefines.Keys)
    {
        cSharpConstants.Add(new CSharpConstant(key, "string", $"\"{knownDefines[key].Replace("\"", "\\\"")}\""));
    }
    
    foreach (var group in defineGroups)
    {
        if (group.Key == 0)
        {
            foreach (var define in group)
            {
                var constant = WriteSingleDefine(define);

                AttachComments(define.Comments, constant);
                cSharpConstants.Add(constant);

                knownDefines[define.Name] = define.Content ?? "";
            }
        }
        else if (group.Key == 1)
        {
            foreach (var define in group)
            {
                var condition = EvalConditionals(define.Conditionals);
                if (condition)
                {
                    var constant = WriteSingleDefine(define);
                    AttachComments(define.Comments, constant);
                    cSharpConstants.Add(constant);

                    knownDefines[define.Name] = define.Content ?? "";
                }
                else
                {
                    Console.WriteLine($"Skipped define {define.Name} because it's covered with false conditional");
                }
            }
        }
        else
        {
            Dictionary<string, string> newDefines = new();
            foreach (var define in group)
            {
                var condition = EvalConditionals(
                    define.Conditionals!.Skip(group.Key - 1)
                        .ToList()
                );
                if (condition)
                {
                    var constant = WriteSingleDefine(define);
                    AttachComments(define.Comments, constant);
                    cSharpConstants.Add(constant);

                    newDefines[define.Name] = define.Content ?? "";
                }
                else
                {
                    Console.WriteLine($"Skipped define {define.Name} because it's covered with false conditional");
                }
            }

            foreach (var (key, value) in newDefines)
            {
                knownDefines[key] = value;
            }
        }
    }

    return cSharpConstants;
}

CSharpConstant WriteSingleDefine(DefineItem defineItem)
{
    if (string.IsNullOrEmpty(defineItem.Content))
    {
        return new CSharpConstant(defineItem.Name, "bool", "true");
    }
    else if (knownDefines.ContainsKey(defineItem.Content))
    {
        return new CSharpConstant(defineItem.Name, "bool", defineItem.Content);
    }
    else if (defineItem.Content.StartsWith("0x") &&
             long.TryParse(
                 defineItem.Content.Substring(2),
                 NumberStyles.HexNumber,
                 NumberFormatInfo.InvariantInfo,
                 out _
             ) ||
             long.TryParse(defineItem.Content, out _)
            )
    {
        return new CSharpConstant(defineItem.Name, "long", defineItem.Content);
    }
    else if (defineItem.Content.StartsWith('\"') && defineItem.Content.EndsWith('\"'))
    {
        return new CSharpConstant(defineItem.Name, "string", defineItem.Content);
    }
    else
    {
        return new CSharpConstant(defineItem.Name, "bool", "true");
    }
}

List<CSharpEnum> WriteEnums(List<EnumItem> enums)
{
    List<CSharpEnum> cSharpEnums = [];
    
    foreach (var enumDecl in enums)
    {
        if (!EvalConditionals(enumDecl.Conditionals))
        {
            Console.WriteLine($"Skipped enum {enumDecl.Name} because it's covered with falsy conditional");
            continue;
        }

        var cSharpEnum = new CSharpEnum(enumDecl.Name);
        
        AttachComments(enumDecl.Comments, cSharpEnum);

        if (enumDecl.IsFlagsEnum)
        {
            cSharpEnum.Attributes.Add("Flags");
        }

        foreach (var enumElement in enumDecl.Elements)
        {
            if (!EvalConditionals(enumElement.Conditionals))
            {
                Console.WriteLine($"Skipped enum value {enumElement.Name} of {enumDecl.Name} because it's covered with falsy conditional");
                continue;
            }

            var enumElementName = enumElement.Name;

            var cSharpEnumValue = new CSharpNamedValue(enumElementName, enumElement.Value.ToString());

            AttachComments(enumElement.Comments, cSharpEnumValue);
            cSharpEnum.Values.Add(cSharpEnumValue);
        }

        cSharpEnums.Add(cSharpEnum);
    }
    
    return cSharpEnums;
}

List<CSharpConstant> WriteEnumsRaw(List<EnumItem> enums)
{
    List<CSharpConstant> cSharpConstants = [];
    
    foreach (var enumDecl in enums)
    {
        if (enumDecl.Conditionals is {Count: > 0} && !EvalConditionals(enumDecl.Conditionals))
        {
            Console.WriteLine($"Skipped enum {enumDecl.Name} because it's covered with falsy conditional");
            continue;
        }

        foreach (var enumElement in enumDecl.Elements)
        {
            if (enumElement.Conditionals is {Count: > 0} && !EvalConditionals(enumElement.Conditionals))
            {
                Console.WriteLine($"Skipped enum value {enumElement.Name} of {enumDecl.Name} because it's covered with falsy conditional");
                continue;
            }

            var enumElementName = enumElement.Name;

            var cSharpConstant = new CSharpConstant(enumElementName, "int", enumElement.Value.ToString());
            AttachComments(enumElement.Comments, cSharpConstant);
            
            cSharpConstants.Add(cSharpConstant);
        }
    }

    return cSharpConstants;
}

List<CSharpDefinition> WriteTypedefs2(List<TypedefItem> typedefs)
{
    List<CSharpDefinition> cSharpDefinitions = [];
    foreach (var typedef in typedefs)
    {
        if (!EvalConditionals(typedef.Conditionals))
        {
            Console.WriteLine($"Skipping typedef {typedef.Name}, because it's conditionals evaluated to false");
            continue;
        }

        var typeDescription = typedef.Type.Description;

        var cSharpDefinition = SaveTypeConversion(
            typeDescription,
            typedef.Name
        );

        if (cSharpDefinition is not null)
        {
            AttachComments(typedef.Comments, cSharpDefinition);
            cSharpDefinitions.Add(cSharpDefinition);
        }
        else
        {
            Console.WriteLine($"Failed to write C# definition for {typedef.Name}");
        }
    }

    return cSharpDefinitions;
}

string GetCSharpTypeOfDescription(TypeDescription typeDescription)
{
    switch (typeDescription.Kind)
    {
        case "Builtin":
        {
            var type = typeDescription.BuiltinType!;

            return cppToSharpKnownConversions.GetValueOrDefault(type, "unknown");
        }
        case "User":
        {
            var type = typeDescription.Name!;

            // try to find the conversion, or fallback to whats actually declared
            return cppToSharpKnownConversions.GetValueOrDefault(type, type);
        }
        case "Pointer":
        {
            var innerType = typeDescription.InnerType!;

            return GetCSharpTypeOfDescription(innerType) + "*";
        }
        case "Type":
        {
            var innerType = typeDescription.InnerType!;

            var name = typeDescription.Name;

            if (innerType.Kind == "Pointer" && innerType.InnerType!.Kind == "Function")
            {
                return name + "Delegate";
            }

            return "unknown";
        }
    }

    return "unknown";
}

CSharpDefinition? SaveTypeConversion(TypeDescription typeDescription, string sourceType)
{
    switch (typeDescription.Kind)
    {
        case "Builtin":
        {
            var type = typeDescription.BuiltinType!;

            if (cppToSharpKnownConversions.TryGetValue(type, out var cSharpType))
            {
                return new CSharpTypeReassignment(sourceType, cSharpType);
            }

            break;
        }
        case "User":
        {
            // for user types we should write them as is, because they can define anything from int to function ptr
            var type = typeDescription.Name!;

            return new CSharpTypeReassignment(sourceType, type);
        }
        case "Pointer":
        {
            var innerType = typeDescription.InnerType!;

            var unmodifiedType = GetCSharpTypeOfDescription(innerType);

            return new CSharpTypeReassignment(sourceType, $"{unmodifiedType}*");
        }
        case "Type":
        {
            // this is most possibly a delegate
            var innerType = typeDescription.InnerType!;

            var name = typeDescription.Name!;

            if (innerType.Kind == "Pointer" && innerType.InnerType!.Kind == "Function")
            {
                // in case of a pointer to a function
                // we have to gen a [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                innerType = innerType.InnerType!;

                var cSharpDelegate = UnwrapFunctionTypeDescriptionToDelegate(innerType, name);

                return cSharpDelegate;
            }
            else
            {
                Console.WriteLine($"Unknown Type typedef {typeDescription.Name}");
            }

            break;
        }
        default:
        {
            Console.WriteLine($"Unknown typedef kind: {typeDescription.Kind}");
            break;
        }
    }
    return null;
}

CSharpDelegate UnwrapFunctionTypeDescriptionToDelegate(TypeDescription description, string name)
{
    var cSharpReturnType = GetCSharpTypeOfDescription(description.ReturnType!);

    var cSharpDelegate = new CSharpDelegate(name, cSharpReturnType);
    
    foreach (var parameter in description.Parameters!)
    {
        var argumentName = parameter.Name!;
        var argumentType = parameter;
        if (parameter.Kind == "Type")
        {
            argumentType = parameter.InnerType;
        }
        else
        {
            Console.WriteLine($"Function parameter {parameter.Name} was not of kind Type. Was {parameter.Kind}");
        }

        var cSharpType = GetCSharpTypeOfDescription(argumentType!);
        
        cSharpDelegate.Arguments.Add(new CSharpArgument(argumentName, cSharpType));
    }

    return cSharpDelegate;
}

List<CSharpStruct> WriteStructs(List<StructItem> structs)
{
    List<CSharpStruct> cSharpStructs = [];

    foreach (var structItem in structs)
    {
        var cSharpStruct = new CSharpStruct(structItem.Name);
        
        AttachComments(structItem.Comments, cSharpStruct);

        foreach (var field in structItem.Fields)
        {
            if (!EvalConditionals(field.Conditionals))
            {
                Console.WriteLine($"Skipped field {field.Name} of {structItem.Name} because it's covered with falsy conditional");
                continue;
            }

            var fieldType = field.Type.Description;

            if (field.IsArray)
            {
                fieldType = fieldType.InnerType!;

                var cSharpType = GetCSharpTypeOfDescription(fieldType);

                var cSharpField = new CSharpTypedVariable(field.Name, cSharpType)
                {
                    IsArray = true,
                    ArrayBound = field.ArrayBounds??""
                };
                AttachComments(field.Comments, cSharpField);

                cSharpStruct.Fields.Add(cSharpField);
            }
            else if (fieldType.Kind == "Type")
            {
                // this is most possibly a delegate
                var innerType = fieldType.InnerType!;

                var name = fieldType.Name;

                if (innerType.Kind == "Pointer" && innerType.InnerType!.Kind == "Function")
                {
                    // in case of a pointer to a function
                    // we have to gen a [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                    innerType = innerType.InnerType!;

                    var cSharpDelegate = UnwrapFunctionTypeDescriptionToDelegate(innerType, name + "Delegate");

                    Console.WriteLine($"Written delegate for {field.Name} of {structItem.Name}");

                    cSharpStruct.InnerDeclarations.Add(cSharpDelegate);

                    var cSharpType = GetCSharpTypeOfDescription(fieldType);
                    var cSharpField = new CSharpTypedVariable(field.Name, cSharpType + "*");
                    AttachComments(field.Comments, cSharpField);

                    cSharpStruct.Fields.Add(cSharpField);
                }
                else
                {
                    Console.WriteLine($"Unknown Type field {field.Name} of {structItem.Name}");
                }
            }
            else
            {
                var cSharpType = GetCSharpTypeOfDescription(fieldType);
                var cSharpField = new CSharpTypedVariable(field.Name, cSharpType);
                AttachComments(field.Comments, cSharpField);
                cSharpStruct.Fields.Add(cSharpField);
            }
        }
        
        cSharpStructs.Add(cSharpStruct);
    }

    return cSharpStructs;
}

(List<CSharpFunction> Functions, List<CSharpDelegate> Delegates) WriteFunctions(List<FunctionItem> functions)
{
    List<CSharpFunction> cSharpFunctions = [];
    List<CSharpDelegate> cSharpDelegates = [];

    foreach (var functionItem in functions)
    {
        if (!EvalConditionals(functionItem.Conditionals))
        {
            Console.WriteLine($"Skipped function {functionItem.Name} because it has falsy conditional");
            continue;
        }

        var functionName = functionItem.Name;

        var cSharpReturnType = GetCSharpTypeOfDescription(functionItem.ReturnType!.Description);

        var cSharpFunction = new CSharpFunction(functionName, cSharpReturnType);
        
        AttachComments(functionItem.Comments, cSharpFunction);

        foreach (var parameter in functionItem.Arguments)
        {
            var argumentName = parameter.Name;

            if (IsKnownCSharpKeyword(argumentName))
            {
                argumentName = $"_{argumentName}";
            }

            if (parameter.Type is null)
            {
                Console.WriteLine($"Ignored parameter: {parameter.Name} of {functionItem.Name}, because it has no type.");
                continue;
            }

            var argumentType = parameter.Type!.Description;

            if (argumentType.Kind == "Array")
            {
                var cSharpType = GetCSharpTypeOfDescription(argumentType.InnerType!);

                var cSharpArgument = new CSharpArgument(argumentName, cSharpType);
                cSharpFunction.Arguments.Add(cSharpArgument);
            }
            else if (argumentType.Kind == "Type")
            {
                // this is most possibly a delegate
                var innerType = argumentType.InnerType!;

                if (innerType.Kind == "Pointer" && innerType.InnerType!.Kind == "Function")
                {
                    // in case of a pointer to a function
                    // we have to gen a [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                    innerType = innerType.InnerType!;

                    var delegateName = functionName + argumentName + "Delegate";
                    var cSharpDelegate = UnwrapFunctionTypeDescriptionToDelegate(innerType, delegateName);

                    cSharpDelegates.Add(cSharpDelegate);

                    cSharpFunction.Arguments.Add(new CSharpArgument(argumentName, delegateName));
                }
                else
                {
                    Console.WriteLine($"Unknown Type argument {argumentType.Name}");
                }
            }
            else
            {
                var cSharpType = GetCSharpTypeOfDescription(argumentType);

                cSharpFunction.Arguments.Add(new CSharpArgument(argumentName, cSharpType));
            }
        }
        
        cSharpFunctions.Add(cSharpFunction);
    }

    return (cSharpFunctions, cSharpDelegates);
}

bool IsKnownCSharpKeyword(string name)
{
    if (name == "ref")
    {
        return true;
    }

    if (name == "out")
    {
        return true;
    }

    if (name == "var")
    {
        return true;
    }

    if (name == "in")
    {
        return true;
    }

    return false;
}

string[] TrimPreceding(string[] lines)
{
    if (lines.Length == 0)
    {
        return lines;
    }

    return lines.Select(x => RemovePrecedingSlashes(x)).ToArray();
}

string RemovePrecedingSlashes(string line)
{
    return line.StartsWith("// ")
        ? line[3..]
        : line.StartsWith("//")
            ? line[2..]
            : line;
}
