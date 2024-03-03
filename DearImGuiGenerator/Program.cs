using System.Globalization;
using System.Text.Json;
using DearImguiGenerator;

const string genNamespace = "DearImGuiBindings";
const string nativeClass = "ImGuiNative";

const string outDir = "../DearImGuiBindings/generated";

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

var dirInfo = new DirectoryInfo(outDir);
if (!dirInfo.Exists)
{
    dirInfo.Create();
}

// default c++ to c# conversions (some are weirdly snake_cased)
Dictionary<string, string> knownTypeConversions = new()
{
    ["int"] = "int",
    ["unsigned int"] = "uint",
    ["unsigned char"] = "byte",
    ["unsigned_char"] = "byte",
    ["unsigned_int"] = "uint",
    ["unsigned_short"] = "char",
    ["long long"] = "long",
    ["long_long"] = "long",
    ["unsigned_long_long"] = "ulong",
    ["short"] = "short",
    ["signed char"] = "sbyte",
    ["signed short"] = "short",
    ["signed int"] = "int",
    ["signed long long"] = "long",
    ["unsigned long long"] = "ulong",
    ["unsigned short"] = "char",
    ["float"] = "float",
    ["bool"] = "bool",
    ["char"] = "byte",
    ["double"] = "double",
    ["void"] = "void",
    ["va_list"] = "__arglist", // special case
    ["size_t"] = "ulong" // assume only x64 for now
};

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
var constants = WriteDefines(definitions.Defines);

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

int x = 5;

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
            bool result = true;
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
    using var writer = new StreamWriter(Path.Combine(outDir, "ImGui.Defines.cs"));

    writer.WriteLine($"namespace {genNamespace};");
    writer.WriteLine();
    writer.WriteLine($"public static partial class {nativeClass}");

    writer.WriteLine("{");

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
                var constant = WriteSingleDefine(define, writer);

                if (constant is not null)
                {
                    AttachComments(define.Comments, constant);
                    cSharpConstants.Add(constant);
                }
                else
                {
                    Console.WriteLine($"Failed to convert {define.Name} into C# definition");
                }

                writer.WriteLine();
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
                    var constant = WriteSingleDefine(define, writer);
                    if (constant is not null)
                    {
                        AttachComments(define.Comments, constant);
                        cSharpConstants.Add(constant);
                    }
                    else
                    {
                        Console.WriteLine($"Failed to convert {define.Name} into C# definition");
                    }

                    writer.WriteLine();
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
                    define.Conditionals.Skip(group.Key - 1)
                        .ToList()
                );
                if (condition)
                {
                    var constant = WriteSingleDefine(define, writer);
                    if (constant is not null)
                    {
                        AttachComments(define.Comments, constant);
                        cSharpConstants.Add(constant);
                    }
                    else
                    {
                        Console.WriteLine($"Failed to convert {define.Name} into C# definition");
                    }

                    writer.WriteLine();
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

    writer.WriteLine("}");

    return cSharpConstants;
}

CSharpConstant? WriteSingleDefine(DefineItem defineItem, StreamWriter streamWriter)
{
    if (string.IsNullOrEmpty(defineItem.Content))
    {
        // this is a bool
        streamWriter.WriteLine($"\tpublic const bool {defineItem.Name} = true;");

        return new CSharpConstant(defineItem.Name, "bool", "true");
    }
    else if (knownDefines.ContainsKey(defineItem.Content))
    {
        streamWriter.WriteLine($"\tpublic const bool {defineItem.Name} = {defineItem.Content};");
        
        return new CSharpConstant(defineItem.Name, "bool", defineItem.Content);
    }
    else if (defineItem.Content.StartsWith("0x") &&
             long.TryParse(
                 defineItem.Content.Substring(2),
                 NumberStyles.HexNumber,
                 NumberFormatInfo.InvariantInfo,
                 out var parsed
             ) ||
             long.TryParse(defineItem.Content, out parsed)
            )
    {
        // this is a number
        streamWriter.WriteLine($"\t/// <summary>");
        streamWriter.WriteLine($"\t/// Original value: {defineItem.Content}");
        streamWriter.WriteLine($"\t/// </summary>");
        streamWriter.WriteLine($"\tpublic const long {defineItem.Name} = {parsed};");
        
        return new CSharpConstant(defineItem.Name, "long", defineItem.Content);
    }
    else if (defineItem.Content.StartsWith('\"') && defineItem.Content.EndsWith('\"'))
    {
        // this is a string
        streamWriter.WriteLine($"\t/// <summary>");
        streamWriter.WriteLine($"\t/// Original value: {defineItem.Content}");
        streamWriter.WriteLine($"\t/// </summary>");
        streamWriter.WriteLine($"\tpublic const string {defineItem.Name} = {defineItem.Content};");
        
        return new CSharpConstant(defineItem.Name, "string", defineItem.Content);
    }
    else
    {
        streamWriter.WriteLine($"\t// public const string {defineItem.Name} = {defineItem.Content};");
        Console.WriteLine($"Unknown define type: {defineItem.Content}");
        
        return new CSharpConstant(defineItem.Name, "bool", "true");
    }
}

List<CSharpEnum> WriteEnums(List<EnumItem> enums)
{
    using var writer = new StreamWriter(Path.Combine(outDir, "ImGui.Enums.cs"));

    writer.WriteLine($"namespace {genNamespace};");
    writer.WriteLine();
    writer.WriteLine($"public static partial class {nativeClass}");

    writer.WriteLine("{");

    List<CSharpEnum> cSharpEnums = [];
    
    foreach (var enumDecl in enums)
    {
        if (!EvalConditionals(enumDecl.Conditionals))
        {
            Console.WriteLine($"Skipped enum {enumDecl.Name} because it's covered with falsy conditional");
            continue;
        }

        var name = enumDecl.Name.TrimEnd('_');

        var cSharpEnum = new CSharpEnum(enumDecl.Name);
        
        AttachComments(enumDecl.Comments, cSharpEnum);

        if (enumDecl.IsFlagsEnum)
        {
            writer.WriteLine($"\t[Flags]");
            cSharpEnum.Attributes.Add("Flags");
        }

        writer.WriteLine($"\tpublic enum {name}");
        writer.WriteLine("\t{");
        foreach (var (enumElement, index) in enumDecl.Elements.Select((x, i) => (x, i)))
        {
            if (!EvalConditionals(enumElement.Conditionals))
            {
                Console.WriteLine($"Skipped enum value {enumElement.Name} of {enumDecl.Name} because it's covered with falsy conditional");
                continue;
            }

            var enumElementName = enumElement.Name;

            // write <summary> with a comment
            var comment = ConvertAttachedToSummary(enumElement.Comments?.Attached ?? "", "\t\t");
            if (!string.IsNullOrEmpty(comment))
            {
                writer.WriteLine($"{comment}");
            }

            // write <remarks> with original value expression
            if (!string.IsNullOrEmpty(enumElement.ValueExpression))
            {
                var escaped = new System.Xml.Linq.XText(enumElement.ValueExpression).ToString();
                writer.WriteLine($"\t\t/// <remarks>");
                writer.WriteLine($"\t\t/// Original value: {escaped}");
                writer.WriteLine($"\t\t/// </remarks>");
            }

            // write the element itself
            writer.Write($"\t\t{enumElementName}");

            // write element value (reroute to a constant)
            writer.Write($" = {nativeClass}.{enumElementName}");

            // separator
            writer.WriteLine(",");

            // newline between elements
            if (index != enumDecl.Elements.Count - 1)
            {
                writer.WriteLine();
            }

            var cSharpEnumValue = new CSharpNamedValue(enumElementName, enumElement.Value.ToString());

            AttachComments(enumElement.Comments, cSharpEnumValue);
            cSharpEnum.Values.Add(cSharpEnumValue);
        }

        cSharpEnums.Add(cSharpEnum);

        writer.WriteLine("\t}");
        writer.WriteLine();
    }

    writer.WriteLine("}");
    
    return cSharpEnums;
}

List<CSharpConstant> WriteEnumsRaw(List<EnumItem> enums)
{
    using var writer = new StreamWriter(Path.Combine(outDir, "ImGui.Enums.Raw.cs"));

    writer.WriteLine($"namespace {genNamespace};");
    writer.WriteLine();
    writer.WriteLine($"public static partial class {nativeClass}");

    writer.WriteLine("{");

    List<CSharpConstant> cSharpConstants = [];
    
    foreach (var enumDecl in enums)
    {
        if (enumDecl.Conditionals is {Count: > 0} && !EvalConditionals(enumDecl.Conditionals))
        {
            Console.WriteLine($"Skipped enum {enumDecl.Name} because it's covered with falsy conditional");
            continue;
        }

        foreach (var (enumElement, index) in enumDecl.Elements.Select((x, i) => (x, i)))
        {
            if (enumElement.Conditionals is {Count: > 0} && !EvalConditionals(enumElement.Conditionals))
            {
                Console.WriteLine($"Skipped enum value {enumElement.Name} of {enumDecl.Name} because it's covered with falsy conditional");
                continue;
            }

            var enumElementName = enumElement.Name;

            // write <summary> with a comment
            var comment = ConvertAttachedToSummary(enumElement.Comments?.Attached ?? "", "\t");
            if (!string.IsNullOrEmpty(comment))
            {
                writer.WriteLine($"{comment}");
            }

            // write <remarks> with original value expression
            if (!string.IsNullOrEmpty(enumElement.ValueExpression))
            {
                var escaped = new System.Xml.Linq.XText(enumElement.ValueExpression).ToString();
                writer.WriteLine($"\t/// <remarks>");
                writer.WriteLine($"\t/// Original value: {escaped}");
                writer.WriteLine($"\t/// </remarks>");
            }

            // write the element itself
            writer.Write($"\tpublic const int {enumElementName}");

            // write element value
            writer.Write($" = {enumElement.Value}");

            // separator
            writer.WriteLine(";");
            writer.WriteLine();

            var cSharpConstant = new CSharpConstant(enumElementName, "int", enumElement.Value.ToString());
            AttachComments(enumElement.Comments, cSharpConstant);
            
            cSharpConstants.Add(cSharpConstant);
        }
    }

    writer.WriteLine("}");

    return cSharpConstants;
}

List<CSharpDefinition> WriteTypedefs2(List<TypedefItem> typedefs)
{
    using var writer = new StreamWriter(Path.Combine(outDir, "ImGui.Typedefs.cs"));
    writer.WriteLine("using System.Runtime.InteropServices;");
    writer.WriteLine();
    writer.WriteLine($"namespace {genNamespace};");
    writer.WriteLine();
    writer.WriteLine($"public static partial class {nativeClass}");

    writer.WriteLine("{");

    List<CSharpDefinition> cSharpDefinitions = [];
    foreach (var typedef in typedefs)
    {
        if (!EvalConditionals(typedef.Conditionals))
        {
            Console.WriteLine($"Skipping typedef {typedef.Name}, because it's conditionals evaluated to false");
            continue;
        }

        string? summary = null;

        if (typedef.Comments?.Attached is not null)
        {
            summary = ConvertAttachedToSummary(typedef.Comments.Attached, "\t");
        }

        var typeDescription = typedef.Type.Description;

        var cSharpDefinition = SaveTypeConversion(
            writer,
            typeDescription,
            typedef.Name,
            summary
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

    writer.WriteLine("}");

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

CSharpDefinition? SaveTypeConversion(StreamWriter writer, TypeDescription typeDescription, string sourceType, string? summary)
{
    switch (typeDescription.Kind)
    {
        case "Builtin":
        {
            var type = typeDescription.BuiltinType!;
            if (knownTypeConversions.TryGetValue(type, out var matchedType))
            {
                // this is a redefine e.g.
                // typedef MyInt = int
                // so we store MyInt into known conversions, which allows us to further substitute int onto MyInt
                knownTypeConversions[sourceType] = matchedType;

                Console.WriteLine($"Saved Builtin typedef: {sourceType} -> {matchedType}");
            }
            else
            {
                Console.WriteLine($"Unknown Builtin typedef: {type}");
            }

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
            if (knownTypeConversions.TryGetValue(type, out var matchedType))
            {
                // this is a redefine e.g.
                // typedef MyInt = int
                // so we store MyInt into known conversions, which allows us to further substitute int onto MyInt
                knownTypeConversions[sourceType] = matchedType;
                Console.WriteLine($"Saved User typedef: {sourceType} -> {matchedType}");
            }
            else
            {
                Console.WriteLine($"Unknown User typedef: {type}");
            }

            return new CSharpTypeReassignment(sourceType, type);
        }
        case "Pointer":
        {
            var innerType = typeDescription.InnerType!;

            if (TryGetTypeConversionFromDescription(innerType, out var matchedType))
            {
                knownTypeConversions[sourceType] = $"{matchedType}*";
                Console.WriteLine($"Saved Pointer typedef: {sourceType} -> {matchedType}*");
            }
            else
            {
                Console.WriteLine($"Failed to determine type of Pointer typedef: {typeDescription.Name}");
            }

            var unmodifiedType = GetCSharpTypeOfDescription(innerType);

            return new CSharpTypeReassignment(sourceType, $"{unmodifiedType}*");
        }
        case "Type":
        {
            // this is most possibly a delegate
            var innerType = typeDescription.InnerType!;

            var name = typeDescription.Name;

            if (innerType.Kind == "Pointer" && innerType.InnerType!.Kind == "Function")
            {
                // in case of a pointer to a function
                // we have to gen a [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                innerType = innerType.InnerType!;

                var (delegateCode, cSharpDelegate) = UnwrapFunctionTypeDescriptionToDelegate(innerType, name);

                writer.WriteLine(summary);
                writer.WriteLine("\t[UnmanagedFunctionPointer(CallingConvention.Cdecl)]");
                writer.WriteLine($"\tpublic unsafe {delegateCode};");

                Console.WriteLine($"Unwrapped function pointer {name}");
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

(string, CSharpDelegate) UnwrapFunctionTypeDescriptionToDelegate(TypeDescription description, string name)
{
    if (!TryGetTypeConversionFromDescription(description.ReturnType!, out var returnType))
    {
        returnType = "unknown";
    }

    var cSharpReturnType = GetCSharpTypeOfDescription(description.ReturnType!);

    var cSharpDelegate = new CSharpDelegate(name, cSharpReturnType);
    
    List<string> parameters = new();
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

        if (!TryGetTypeConversionFromDescription(argumentType!, out var matchedArgumentType))
        {
            matchedArgumentType = "unknown";
        }

        parameters.Add($"{matchedArgumentType} {argumentName}");

        var cSharpType = GetCSharpTypeOfDescription(argumentType!);
        
        cSharpDelegate.Arguments.Add(new CSharpTypedVariable(argumentName, cSharpType));
    }

    return ($"delegate {returnType} {name}({string.Join(", ", parameters)})", cSharpDelegate);
}

bool TryGetTypeConversionFromDescription(TypeDescription description, out string matchedType)
{
    if (description.Kind == "Builtin")
    {
        if (knownTypeConversions.TryGetValue(description.BuiltinType!, out matchedType!))
        {
            return true;
        }
        else
        {
            Console.WriteLine($"Failed getting conversion for Builtin type for {description.BuiltinType}");
        }
    }
    else if (description.Kind == "User")
    {
        if (knownTypeConversions.TryGetValue(description.Name!, out matchedType!))
        {
            return true;
        }
        else
        {
            Console.WriteLine($"Failed getting conversion for User type for {description.Name}. Using original");
            // User type may not be known at this point, just use it, as if it was known
            matchedType = description.Name!;
            return true;
        }
    }
    else if (description.Kind == "Pointer")
    {
        if (TryGetTypeConversionFromDescription(description.InnerType!, out matchedType))
        {
            matchedType = $"{matchedType}*";
            return true;
        }
        else
        {
            matchedType = "unknown*";
            return true;
        }
    }
    else
    {
        Console.WriteLine($"Failed getting conversion of kind {description.Kind}.");
    }

    matchedType = "";

    return false;
}

List<CSharpStruct> WriteStructs(List<StructItem> structs)
{
    using var writer = new StreamWriter(Path.Combine(outDir, "ImGui.Structs.cs"));

    writer.WriteLine("using System.Runtime.InteropServices;");
    writer.WriteLine($"namespace {genNamespace};");
    writer.WriteLine();

    writer.WriteLine($"public static partial class {nativeClass}");

    writer.WriteLine("{");

    List<CSharpStruct> cSharpStructs = [];

    foreach (var structItem in structs)
    {
        if (structItem.Comments?.Attached is not null)
        {
            var summary = ConvertAttachedToSummary(structItem.Comments.Attached, "\t");

            writer.WriteLine(summary);
        }

        writer.WriteLine($"\tpublic struct {structItem.Name}");
        writer.WriteLine("\t{");

        var cSharpStruct = new CSharpStruct(structItem.Name);
        
        AttachComments(structItem.Comments, cSharpStruct);

        foreach (var field in structItem.Fields)
        {
            if (!EvalConditionals(field.Conditionals))
            {
                Console.WriteLine($"Skipped field {field.Name} of {structItem.Name} because it's covered with falsy conditional");
                continue;
            }

            if (field.Comments?.Attached is not null)
            {
                var summary = ConvertAttachedToSummary(field.Comments.Attached, "\t\t");

                writer.WriteLine(summary);
            }

            if (field.Comments?.Preceding is not null)
            {
                var remarks = ConvertPrecedingToRemarks(field.Comments.Preceding, "\t\t");
                writer.WriteLine(remarks);
            }

            var fieldType = field.Type.Description;

            if (field.IsArray)
            {
                fieldType = fieldType.InnerType!;
                
                if (fieldType.Kind == "Builtin")
                {
                    if (TryGetTypeConversionFromDescription(fieldType, out var matchedType))
                    {
                        writer.WriteLine($"\t\tpublic unsafe fixed {matchedType} {field.Name}[{field.ArrayBounds}];");
                    }
                    else
                    {
                        writer.WriteLine($"\t\tpublic unsafe fixed {field.Type.Declaration} {field.Name};");
                        Console.WriteLine($"Unknown Builtin type {field.Type.Declaration} of array field {field.Name} in {structItem.Name}");
                    }
                }
                else if (fieldType.Kind == "User")
                {
                    // for user types we need to emit something like this
                    // [System.Runtime.CompilerServices.InlineArray(ImGuiKey_KeysData_SIZE)]
                    // public struct KeysDataInlineArray
                    // {
                    //      ImGuiKeyData Element;
                    // }

                    if (TryGetTypeConversionFromDescription(fieldType, out var matchedType))
                    {
                        var inlineArrayTypeName = $"{field.Name}InlineArray";
                        var bound = field.ArrayBounds!;

                        if (!long.TryParse(bound, out _))
                        {
                            // if it's not a number - then it's a constant (we store constants as long, but InlineArray expects int)
                            bound = $"(int){bound}";
                        }

                        writer.WriteLine($"\t\tpublic {inlineArrayTypeName} {field.Name};");
                        writer.WriteLine();

                        writer.WriteLine($"\t\t[System.Runtime.CompilerServices.InlineArray({bound})]");
                        writer.WriteLine($"\t\tpublic struct {inlineArrayTypeName}");
                        writer.WriteLine("\t\t{");
                        writer.WriteLine($"\t\t\tpublic {matchedType} Element;");
                        writer.WriteLine("\t\t}");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to determine User type for InlineArray {field.Name} of {structItem.Name}");
                    }
                }

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

                    var (delegateCode, cSharpDelegate) = UnwrapFunctionTypeDescriptionToDelegate(innerType, name + "Delegate");

                    writer.WriteLine($"\t\tpublic unsafe {name + "Delegate"}* {name};");
                    writer.WriteLine();

                    writer.WriteLine("\t\t[UnmanagedFunctionPointer(CallingConvention.Cdecl)]");
                    writer.WriteLine($"\t\tpublic unsafe {delegateCode};");

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
                if (TryGetTypeConversionFromDescription(fieldType, out var matchedType))
                {
                    writer.WriteLine($"\t\tpublic {(matchedType.EndsWith('*') ? "unsafe " : "")}{matchedType} {field.Name};");
                }
                else
                {
                    writer.WriteLine($"\t\t// {field.Type.Declaration}");
                    Console.WriteLine($"Failed to determine type of field {field.Name} of {structItem.Name}");
                }

                var cSharpType = GetCSharpTypeOfDescription(fieldType);
                var cSharpField = new CSharpTypedVariable(field.Name, cSharpType);
                AttachComments(field.Comments, cSharpField);
                cSharpStruct.Fields.Add(cSharpField);
            }

            writer.WriteLine();
        }

        knownTypeConversions[structItem.Name] = structItem.Name;

        writer.WriteLine("\t}");
        writer.WriteLine();
        
        cSharpStructs.Add(cSharpStruct);
    }

    writer.WriteLine("}");

    return cSharpStructs;
}

(List<CSharpFunction> Functions, List<CSharpDelegate> Delegates) WriteFunctions(List<FunctionItem> functions)
{
    using var writer = new StreamWriter(Path.Combine(outDir, "ImGui.Functions.cs"));

    writer.WriteLine("using System.Runtime.InteropServices;");
    writer.WriteLine($"namespace {genNamespace};");
    writer.WriteLine();

    writer.WriteLine($"public static partial class {nativeClass}");

    writer.WriteLine("{");

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

        bool requiresUnsafe = false;

        string returnType = functionItem.ReturnType!.Declaration;

        if (TryGetTypeConversionFromDescription(functionItem.ReturnType!.Description, out var matchedType))
        {
            returnType = matchedType;
        }
        else
        {
            Console.WriteLine($"Failed to get type conversion for return_type: {returnType}");
        }

        var cSharpReturnType = GetCSharpTypeOfDescription(functionItem.ReturnType!.Description);

        var cSharpFunction = new CSharpFunction(functionName, cSharpReturnType);
        
        AttachComments(functionItem.Comments, cSharpFunction);

        if (functionItem.ReturnType!.Description.Kind == "Pointer")
        {
            requiresUnsafe = true;
        }

        List<string> parameters = new();
        foreach (var parameter in functionItem.Arguments!)
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

            if (argumentType.Kind == "Pointer")
            {
                requiresUnsafe = true;
            }

            string finalArgumentType;

            if (argumentType.Kind == "Array")
            {
                if (!TryGetTypeConversionFromDescription(argumentType.InnerType, out finalArgumentType))
                {
                    Console.WriteLine($"Failed to get type conversion for Array argument: {parameter.Type.Declaration}");
                    finalArgumentType = "unknown";
                }
                else
                {
                    finalArgumentType = finalArgumentType + "*";
                }

                var cSharpType = GetCSharpTypeOfDescription(argumentType.InnerType);

                var cSharpArgument = new CSharpTypedVariable(argumentName, cSharpType);
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
                    var (delegateCode, cSharpDelegate) = UnwrapFunctionTypeDescriptionToDelegate(innerType, delegateName);

                    writer.WriteLine("\t[UnmanagedFunctionPointer(CallingConvention.Cdecl)]");
                    writer.WriteLine($"\tpublic unsafe {delegateCode};");

                    // delegates are always used as pointers
                    finalArgumentType = $"{delegateName}*";
                    cSharpDelegates.Add(cSharpDelegate);

                    cSharpFunction.Arguments.Add(new CSharpTypedVariable(argumentName, delegateName));
                }
                else
                {
                    finalArgumentType = "unknown_delegate";
                    Console.WriteLine($"Unknown Type argument {argumentType.Name}");
                }
            }
            else
            {
                if (!TryGetTypeConversionFromDescription(argumentType, out finalArgumentType))
                {
                    Console.WriteLine($"Failed to get type conversion for argument: {parameter.Type.Declaration}");
                    finalArgumentType = "unknown";
                }

                var cSharpType = GetCSharpTypeOfDescription(argumentType);

                cSharpFunction.Arguments.Add(new CSharpTypedVariable(argumentName, cSharpType));
            }

            if (finalArgumentType == "void*")
            {
                requiresUnsafe = true;
            }

            if (finalArgumentType == "__arglist")
            {
                parameters.Add($"{finalArgumentType}");
            }
            else
            {
                parameters.Add($"{finalArgumentType} {argumentName}");
            }
        }

        writer.WriteLine($"\t[DllImport(\"cimgui\", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]");
        writer.WriteLine($"\tpublic static extern {(requiresUnsafe ? "unsafe " : "")}{returnType} {functionName}({string.Join(", ", parameters)});");
        writer.WriteLine();
        
        cSharpFunctions.Add(cSharpFunction);
    }

    writer.WriteLine("}");

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

string ConvertAttachedToSummary(string comment, string prefix = "")
{
    if (comment == "")
    {
        return "";
    }

    var modified = comment.StartsWith("// ")
        ? comment[3..]
        : comment.StartsWith("//")
            ? comment[2..]
            : comment;
    var escaped = new System.Xml.Linq.XText(modified).ToString();
    return $"{prefix}/// <summary>\n" +
           $"{prefix}/// {escaped}\n" +
           $"{prefix}/// </summary>";
}

string ConvertPrecedingToRemarks(string[] lines, string prefix = "")
{
    if (lines.Length == 0)
    {
        return $"{prefix}/// <remarks></remarks>";
    }

    var remarks = string.Join(
        "\n",
        lines
            .Select(x => RemovePrecedingSlashes(x))
            .Select(x => new System.Xml.Linq.XText(x).ToString())
            .Select(x => $"{prefix}/// {x}")
    );

    return $"{prefix}/// <remarks>\n" +
           $"{remarks}\n" +
           $"{prefix}/// </remarks>";
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
