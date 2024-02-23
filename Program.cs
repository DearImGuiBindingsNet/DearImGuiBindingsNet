using System.Globalization;
using System.Text.Json;

// var context = TestImGuiNative.ImGui_CreateContext();
// TestImGuiNative.ImGui_SetCurrentContext(context);

const string genNamespace = "DearImGuiBindings";
const string nativeClass = "ImGuiNative";

Dictionary<string, string> knownDefines = new()
{
    ["IMGUI_IMPL_API"] = "extern \"C\" __declspec(dllexport)",
    ["IMGUI_DISABLE_OBSOLETE_FUNCTIONS"] = "",
    ["IMGUI_DISABLE_OBSOLETE_KEYIO"] = ""
};

using var fs = new FileStream("./cimgui/cimgui.json", FileMode.Open);

var definitions = JsonSerializer.Deserialize<Definitions>(
    fs,
    new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    }
);

var dirInfo = new DirectoryInfo("generated");
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
    ["unsigned_short"] = "ushort",
    ["long long"] = "long",
    ["long_long"] = "long",
    ["unsigned_long_long"] = "ulong",
    ["short"] = "short",
    ["signed char"] = "char",
    ["signed short"] = "short",
    ["signed int"] = "int",
    ["signed long long"] = "long",
    ["unsigned long long"] = "ulong",
    ["unsigned short"] = "ushort",
    ["float"] = "float",
    ["bool"] = "bool",
    ["char"] = "char",
    ["double"] = "double",
    ["void"] = "void",
    ["size_t"] = "ulong" // assume only x64 for now
};

Dictionary<string, string> customTypeConversions = new();

Console.WriteLine("---------------------");
Console.WriteLine("Writing Defines");
Console.WriteLine("---------------------");
WriteDefines(definitions.Defines);

Console.WriteLine("---------------------");
Console.WriteLine("Writing EnumsRaw");
Console.WriteLine("---------------------");
WriteEnumsRaw(definitions.Enums);

Console.WriteLine("---------------------");
Console.WriteLine("Writing Enums");
Console.WriteLine("---------------------");
WriteEnums(definitions.Enums);
Console.WriteLine("---------------------");
Console.WriteLine("Writing Typedefs");
Console.WriteLine("---------------------");
WriteTypedefs2(definitions.Typedefs);
Console.WriteLine("---------------------");
Console.WriteLine("Writing Structs");
Console.WriteLine("---------------------");
WriteStructs(definitions.Structs);

Console.WriteLine("---------------------");
Console.WriteLine("Done");
Console.WriteLine("---------------------");
int x = 5;

bool EvalConditionals(List<ConditionalItem>? conditionals)
{
    if (conditionals is {Count: > 0})
    {
        if (conditionals.Count == 1)
        {
            var condition = conditionals[0];
            return ((condition.Condition == "ifdef" && knownDefines.ContainsKey(condition.Expression)) ||
                    (condition.Condition == "ifndef" && !knownDefines.ContainsKey(condition.Expression)));
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

void WriteDefines(List<DefineItem> defines)
{
    using var writer = new StreamWriter("generated/ImGui.Defines.cs");

    writer.WriteLine($"namespace {genNamespace};");
    writer.WriteLine();
    writer.WriteLine($"public static partial class {nativeClass}");

    writer.WriteLine("{");

    // dear_bindings writes defines in a strange manner, producing redefines, so when we group them by count, we can produce more accurate result
    var defineGroups = defines.GroupBy(x => x.Conditionals?.Count ?? 0);

    foreach (var group in defineGroups)
    {
        if (group.Key == 0)
        {
            foreach (var define in group)
            {
                WriteSingleDefine(define, writer);
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
                    WriteSingleDefine(define, writer);
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
                    WriteSingleDefine(define, writer);
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
}

void WriteSingleDefine(DefineItem defineItem, StreamWriter streamWriter)
{
    if (string.IsNullOrEmpty(defineItem.Content))
    {
        // this is a bool
        streamWriter.WriteLine($"\tpublic const bool {defineItem.Name} = true;");
    }
    else if (knownDefines.ContainsKey(defineItem.Content))
    {
        streamWriter.WriteLine($"\tpublic const bool {defineItem.Name} = {defineItem.Content};");
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
    }
    else if (defineItem.Content.StartsWith('\"') && defineItem.Content.EndsWith('\"'))
    {
        // this is a string
        streamWriter.WriteLine($"\t/// <summary>");
        streamWriter.WriteLine($"\t/// Original value: {defineItem.Content}");
        streamWriter.WriteLine($"\t/// </summary>");
        streamWriter.WriteLine($"\tpublic const string {defineItem.Name} = {defineItem.Content};");
    }
    else
    {
        streamWriter.WriteLine($"\t// public const string {defineItem.Name} = {defineItem.Content};");
        Console.WriteLine($"Unknown define type: {defineItem.Content}");
    }
}

void WriteEnums(List<EnumItem> enums)
{
    using var writer = new StreamWriter("generated/ImGui.Enums.cs");

    writer.WriteLine($"namespace {genNamespace};");
    writer.WriteLine();
    writer.WriteLine($"public static partial class {nativeClass}");

    writer.WriteLine("{");

    foreach (var enumDecl in enums)
    {
        if (!EvalConditionals(enumDecl.Conditionals))
        {
            Console.WriteLine($"Skipped enum {enumDecl.Name} because it's covered with falsy conditional");
            continue;
        }

        var name = enumDecl.Name.TrimEnd('_');

        if (enumDecl.IsFlagsEnum)
        {
            writer.WriteLine($"\t[Flags]");
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
        }

        writer.WriteLine("\t}");
        writer.WriteLine();
    }

    writer.WriteLine("}");
}

void WriteEnumsRaw(List<EnumItem> enums)
{
    using var writer = new StreamWriter("generated/ImGui.Enums.Raw.cs");

    writer.WriteLine($"namespace {genNamespace};");
    writer.WriteLine();
    writer.WriteLine($"public static partial class {nativeClass}");

    writer.WriteLine("{");

    foreach (var enumDecl in enums)
    {
        if (enumDecl.Conditionals is {Count: > 0} && !EvalConditionals(enumDecl.Conditionals))
        {
            Console.WriteLine($"Skipped enum {enumDecl.Name} because it's covered with falsy conditional");
            continue;
        }

        var name = enumDecl.Name.TrimEnd('_');

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
        }
    }

    writer.WriteLine("}");
}

void WriteTypedefs2(List<TypedefItem> typedefs)
{
    using var writer = new StreamWriter("generated/ImGui.Typedefs.cs");
    writer.WriteLine("using System.Runtime.InteropServices;");
    writer.WriteLine();
    writer.WriteLine($"namespace {genNamespace};");
    writer.WriteLine();
    writer.WriteLine($"public static partial class {nativeClass}");

    writer.WriteLine("{");
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

        SaveTypeConversion(
            writer,
            typeDescription,
            typedef.Name,
            summary
        );
    }

    writer.WriteLine("}");
}

void SaveTypeConversion(StreamWriter writer, TypeDescription typeDescription, string sourceType, string? summary)
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

            break;
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

            break;
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

                var delegateCode = UnwrapFunctionTypeDescriptionToDelegate(innerType, name);

                writer.WriteLine(summary);
                writer.WriteLine("\t[UnmanagedFunctionPointer(CallingConvention.Cdecl)]");
                writer.WriteLine($"\tpublic unsafe {delegateCode};");

                Console.WriteLine($"Unwrapped function pointer {name}");
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
}

string UnwrapFunctionTypeDescriptionToDelegate(TypeDescription description, string name)
{
    if (!TryGetTypeConversionFromDescription(description.ReturnType!, out var returnType))
    {
        returnType = "unknown";
    }

    List<string> parameters = new();
    foreach (var parameter in description.Parameters!)
    {
        var argumentName = parameter.Name;
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
    }

    return $"delegate {returnType} {name}({string.Join(", ", parameters)})";
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

void WriteStructs(List<StructItem> structs)
{
    using var writer = new StreamWriter("generated/ImGui.Structs.cs");

    writer.WriteLine($"namespace {genNamespace};");
    writer.WriteLine();

    writer.WriteLine($"public static partial class {nativeClass}");

    writer.WriteLine("{");

    foreach (var structItem in structs)
    {
        if (structItem.Comments?.Attached is not null)
        {
            var summary = ConvertAttachedToSummary(structItem.Comments.Attached, "\t");

            writer.WriteLine(summary);
        }

        writer.WriteLine($"\tpublic struct {structItem.Name}");
        writer.WriteLine("\t{");
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
            }

            writer.WriteLine();
        }

        knownTypeConversions[structItem.Name] = structItem.Name;

        writer.WriteLine("\t}");
        writer.WriteLine();
    }

    writer.WriteLine("}");
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


record Definitions(
    List<DefineItem> Defines,
    List<EnumItem> Enums,
    List<TypedefItem> Typedefs,
    List<StructItem> Structs
);

record DefineItem(
    string Name,
    string? Content,
    List<ConditionalItem>? Conditionals
);

record StructItem(
    string Name,
    string OriginalFullyQualifiedName,
    string Kind,
    bool ByValue,
    bool ForwardDeclaration,
    bool IsAnonymous,
    List<StructItemField> Fields,
    Comments? Comments,
    bool IsInternal
);

record StructItemField(
    string Name,
    bool IsArray,
    bool IsAnonymous,
    string? ArrayBounds,
    Comments? Comments,
    StructItemFieldType Type,
    List<ConditionalItem> Conditionals,
    bool IsInternal
);

record StructItemFieldType(
    string Declaration,
    TypeDescription Description
);

record TypeDescription(
    string Kind,
    string? Name,
    string? BuiltinType,
    TypeDescription? ReturnType,
    TypeDescription? InnerType,
    List<TypeDescription>? Parameters
);

record TypedefItem(
    string Name,
    TypedefType Type,
    Comments Comments,
    List<ConditionalItem>? Conditionals);

record TypedefType(
    string Declaration,
    TypeDescription Description,
    TypedefTypeDetails TypeDetails
);

record TypedefTypeDetails(
    string Flavour,
    TypedefTypeDetailsReturnType ReturnType
);

record TypedefTypeDetailsReturnType(
    string Declaration,
    TypedefTypeDetailsReturnTypeDescription Description
);

record TypedefTypeDetailsReturnTypeDescription();

record TypedefTypeDescription(
    string Kind,
    string BuiltinType);

record EnumItem(
    string Name,
    string OriginalFullyQualifiedName,
    bool IsFlagsEnum,
    List<EnumElement> Elements,
    List<ConditionalItem> Conditionals
);

record EnumElement(
    string Name,
    string ValueExpression,
    int Value,
    bool IsCount,
    bool IsInternal,
    Comments Comments,
    List<ConditionalItem> Conditionals
);

record Comments(string? Attached, string[]? Preceding);

record ConditionalItem(
    string Condition,
    string Expression);