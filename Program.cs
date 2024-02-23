using System.Globalization;
using System.Text.Json;

// var context = TestImGuiNative.ImGui_CreateContext();
// TestImGuiNative.ImGui_SetCurrentContext(context);

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
WriteTypedefs(definitions.Typedefs);
Console.WriteLine("---------------------");
Console.WriteLine("Writing Structs");
Console.WriteLine("---------------------");
WriteStructs(definitions.Structs);

Console.WriteLine("---------------------");
Console.WriteLine("Done");
Console.WriteLine("---------------------");
int x = 5;

bool EvalConditionals(List<ConditionalItem> conditionals)
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

string FlattenConditionals(List<ConditionalItem> conditionals)
{
    if (conditionals is {Count: > 0})
    {
        return string.Join(
            "\n",
            conditionals.Select(
                    x => (x.Condition,
                        x.Expression,
                        Value: (x.Condition == "ifdef" && knownDefines.ContainsKey(x.Expression)) ||
                               (x.Condition == "ifndef" && !knownDefines.ContainsKey(x.Expression)))
                )
                .Select(x => $"{x.Condition} {x.Expression} = {x.Value}")
        );
    }
    else
    {
        return "";
    }
}

void WriteDefines(List<DefineItem> defines)
{
    using var writer = new StreamWriter("generated/ImGui.Defines.cs");

    writer.WriteLine("namespace EgopImgui;");
    writer.WriteLine();
    writer.WriteLine("public static partial class ImGui");

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
                var condition = EvalConditionals(define.Conditionals.Skip(group.Key - 1).ToList());
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

    writer.WriteLine("namespace EgopImgui;");
    writer.WriteLine();

    foreach (var enumDecl in enums)
    {
        if (enumDecl.Conditionals is {Count: > 0} && !EvalConditionals(enumDecl.Conditionals))
        {
            Console.WriteLine($"Skipped enum {enumDecl.Name} because it's covered with falsy conditional");
            continue;
        }

        var name = enumDecl.Name.TrimEnd('_');

        if (enumDecl.IsFlagsEnum)
        {
            writer.WriteLine($"[Flags]");
        }

        writer.WriteLine($"public enum {name}");
        writer.WriteLine("{");
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
            writer.Write($"\t{enumElementName}");

            // write element value (reroute to a constant)
            writer.Write($" = ImGui.{enumElementName}");

            // separator
            writer.WriteLine(",");

            // newline between elements
            if (index != enumDecl.Elements.Count - 1)
            {
                writer.WriteLine();
            }
        }

        writer.WriteLine("}");
        writer.WriteLine();
    }
}

void WriteEnumsRaw(List<EnumItem> enums)
{
    using var writer = new StreamWriter("generated/ImGui.Enums.Raw.cs");

    writer.WriteLine("namespace EgopImgui;");
    writer.WriteLine();
    writer.WriteLine("public static partial class ImGui");

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

        return $"{prefix}/// <summary>\n" +
               $"{prefix}/// {modified}\n" +
               $"{prefix}/// </summary>";
    }
}

void WriteTypedefs(List<TypedefItem> typedefs)
{
    using var writer = new StreamWriter("generated/ImGui.Typedefs.cs");

    foreach (var (typedef, index) in typedefs.Select((x, i) => (x, i)))
    {
        if (typedef.Conditionals is {Count: > 0} && !EvalConditionals(typedef.Conditionals))
        {
            Console.WriteLine($"Skipping typedef {typedef.Name} because it's covered with falsy conditional");
            continue;
        }

        var declType = typedef.Type.Declaration;
        var typeDescription = typedef.Type.Description;

        var (csharpType, type) = GetCSharpTypeOfCppTypeDescription(typeDescription);

        switch (type)
        {
            case "Builtin":
            {
                writer.WriteLine($"global using {typedef.Name} = {csharpType}; // Original Type: {declType}");
                break;
            }
            case "Normal":
            {
                writer.WriteLine($"global using {typedef.Name} = {csharpType}; // Original Type: {declType}");
                knownTypeConversions[typedef.Name] = csharpType;
                break;
            }
            case "UserDefine":
            {
                writer.WriteLine($"global using {typedef.Name} = {csharpType}; // Original Type: {declType}");
                customTypeConversions[typedef.Name] = csharpType;
                break;
            }
            case "Pointer":
            {
                writer.WriteLine($"global using unsafe {typedef.Name} = {csharpType}; // Original Type: {declType}");
                break;
            }
            case "Function":
            {
                writer.WriteLine($"{csharpType}; // Original Type: {declType}");
                break;
            }
            case "Unknown":
            {
                writer.WriteLine($"global using unsafe {typedef.Name} = {csharpType}; // Original Type: {declType}");
                break;
            }
        }
        
        if (index != typedefs.Count - 1)
        {
            writer.WriteLine();
        }
    }
}

(string Type, string Kind) GetCSharpTypeOfCppTypeDescription(TypeDescription description, string? typeName = null)
{
    if (description.Kind == "Builtin")
    {
        if (knownTypeConversions.TryGetValue(description.BuiltinType!, out var matchedType))
        {
            return (matchedType, "Normal");
        }
        else
        {
            return (description.BuiltinType!, "Normal");
        }
    }
    else if (description.Kind == "User")
    {
        if (customTypeConversions.TryGetValue(description.Name!, out var type))
        {
            return (type, "Normal");
        }
        else
        {
            return (description.Name!, "UserDefine");
        }
    }
    else if (description.Kind == "Pointer")
    {
        var (innerType, innerKind) = GetCSharpTypeOfCppTypeDescription(description.InnerType!, typeName);
        if (innerKind == "Function")
        {
            return (innerType, innerKind);
        }
        return ($"{innerType}*", "Pointer");
    }
    else if (description.Kind == "Function")
    {
        var (returnType, returnKind) = GetCSharpTypeOfCppTypeDescription(description.ReturnType!, typeName);

        List<string> parameters = new();
        foreach (var parameter in description.Parameters!)
        {
            var name = parameter.Name;
            var (paramType, kind) = GetCSharpTypeOfCppTypeDescription(parameter.InnerType!, typeName);
            
            parameters.Add($"{paramType} {name}");
        }

        return ($"delegate {returnType} {typeName}({string.Join(", ", parameters)})", "Function");
    }
    else if (description.Kind == "Type")
    {
        return GetCSharpTypeOfCppTypeDescription(description.InnerType!, description.Name);
    }
    else
    {
        return ("", "Unknown");
    }
}

void WriteStructs(List<StructItem> structs)
{
    using var writer = new StreamWriter("generated/ImGui.Structs.cs");

    writer.WriteLine("namespace EgopImgui;");
    writer.WriteLine();

    writer.WriteLine("public static partial class ImGui");

    writer.WriteLine("{");

    foreach (var structItem in structs)
    {
        if (structItem.Comments is not null)
        {
            if (structItem.Comments.Attached is not null)
            {
                var summary = ConvertAttachedToSummary(structItem.Comments.Attached, "\t");

                writer.WriteLine(summary);
            }
        }
        
        writer.WriteLine($"\tpublic struct {structItem.Name}");
        writer.WriteLine("\t{");
        foreach (var field in structItem.Fields)
        {
            if (field.Conditionals is {Count: > 0} && !EvalConditionals(field.Conditionals))
            {
                Console.WriteLine($"Skipped field {field.Name} of {structItem.Name} because it's covered with falsy conditional");
                continue;
            }

            if (field.Comments is not null)
            {
                if (field.Comments.Attached is not null)
                {
                    var summary = ConvertAttachedToSummary(field.Comments.Attached, "\t\t");

                    writer.WriteLine(summary);
                }
            }

            if (field.IsArray)
            {
                var type = field.Type.Description.InnerType.Kind == "Builtin"
                    ? field.Type.Description.InnerType.BuiltinType
                    : field.Type.Description.InnerType.Name;

                if (knownTypeConversions.TryGetValue(type, out var knownType))
                {
                    writer.WriteLine($"\t\tpublic unsafe fixed {knownType} {field.Name}[{field.ArrayBounds}];");
                }
                else
                {
                    writer.WriteLine($"\t\tpublic unsafe fixed {type} {field.Name}[{field.ArrayBounds}];");
                    Console.WriteLine($"Unknown type {type} of array field {field.Name} in {structItem.Name}");
                }
            }
            else if (field.Type.Description.Kind == "Pointer")
            {
                var innerType = field.Type.Description.InnerType;
                var type = "";
                if (innerType.Kind == "Builtin")
                {
                    type = innerType.BuiltinType;
                }
                else if (innerType.Kind == "User")
                {
                    type = innerType.Name;
                }
                else if (innerType.Kind == "Pointer")
                {
                    type = innerType.InnerType.Kind == "Builtin"
                        ? innerType.InnerType.BuiltinType
                        : innerType.InnerType.Name;

                    type += "*";
                }

                if (knownTypeConversions.TryGetValue(type, out var knownType))
                {
                    writer.WriteLine($"\t\tpublic unsafe {knownType}* {field.Name};");
                }
                else
                {
                    writer.WriteLine($"\t\tpublic unsafe {type}* {field.Name};");
                    Console.WriteLine($"Unknown type {type} of pointer field {field.Name} in {structItem.Name}");
                }
            }
            else
            {
                var type = field.Type.Declaration;
                if (knownTypeConversions.TryGetValue(type, out var knownType))
                {
                    writer.WriteLine($"\t\tpublic {knownType} {field.Name};");
                }
                else
                {
                    writer.WriteLine($"\t\tpublic {type} {field.Name};");
                    Console.WriteLine($"Unknown type {type} of normal field {field.Name} in {structItem.Name}");
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