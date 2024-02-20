using System.Globalization;
using System.Text.Json;

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

Dictionary<string, string> knownConversions = new()
{
    ["int"] = "int",
    ["unsigned int"] = "uint",
    ["unsigned char"] = "byte",
    ["unsigned_char"] = "byte",
    ["unsigned_int"] = "uint",
    ["long long"] = "long",
    ["short"] = "short",
    ["void*"] = "System.IntPtr",
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

    foreach (var define in defines)
    {
        if (define.Conditionals is {Count: > 0})
        {
            Console.WriteLine(FlattenConditionals(define.Conditionals));
            if (EvalConditionals(define.Conditionals))
            {
                knownDefines[define.Name] = define.Content ?? "";
                Console.WriteLine($"Added conditional define {define.Name} with value: {define.Content}");
            }
            else
            {
                Console.WriteLine($"Skipped conditional define {define.Name} with value: {define.Content}");
            }
        }
        else
        {
            knownDefines[define.Name] = define.Content ?? "";
            Console.WriteLine($"Added unconditional define {define.Name} with value: {define.Content}");
        }

        Console.WriteLine();
    }

    foreach (var define in defines)
    {
        if (!knownDefines.ContainsKey(define.Name))
        {
            Console.WriteLine($"Skipping define: {define.Name} because it's unknown");
            continue;
        }

        if (string.IsNullOrEmpty(define.Content))
        {
            // this is a bool
            writer.WriteLine($"\tpublic const bool {define.Name} = true;");
        }
        else if (knownDefines.ContainsKey(define.Content))
        {
            writer.WriteLine($"\tpublic const bool {define.Name} = {define.Content};");
        }
        else if (define.Content.StartsWith("0x") &&
                 long.TryParse(
                     define.Content.Substring(2),
                     NumberStyles.HexNumber,
                     NumberFormatInfo.InvariantInfo,
                     out var parsed
                 ) ||
                 long.TryParse(define.Content, out parsed)
                )
        {
            // this is a number
            writer.WriteLine($"\t/// <summary>");
            writer.WriteLine($"\t/// Original value: {define.Content}");
            writer.WriteLine($"\t/// </summary>");
            writer.WriteLine($"\tpublic const long {define.Name} = {parsed};");
        }
        else if (define.Content.StartsWith('\"') && define.Content.EndsWith('\"'))
        {
            // this is a string
            writer.WriteLine($"\t/// <summary>");
            writer.WriteLine($"\t/// Original value: {define.Content}");
            writer.WriteLine($"\t/// </summary>");
            writer.WriteLine($"\tpublic const string {define.Name} = {define.Content};");
        }
        else
        {
            writer.WriteLine($"\t// public const string {define.Name} = {define.Content};");
            Console.WriteLine($"Unknown define type: {define.Content}");
        }
    }

    writer.WriteLine("}");
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

        if (!knownConversions.TryGetValue(declType, out var matchedType))
        {
            Console.WriteLine($"Unknown matching type: {declType}");
        }
        else
        {
            writer.WriteLine($"global using {typedef.Name} = {matchedType}; // Original Type: {declType}");
            knownConversions[typedef.Name] = matchedType;

            if (index != typedefs.Count - 1)
            {
                writer.WriteLine();
            }
        }
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
        var requireConstructor = false;
        writer.WriteLine($"\tpublic struct {structItem.Name}");
        writer.WriteLine("\t{");
        foreach (var field in structItem.Fields)
        {
            if (field.Conditionals is {Count: > 0} && !EvalConditionals(field.Conditionals))
            {
                Console.WriteLine($"Skipped field {field.Name} of {structItem.Name} because it's covered with falsy conditional");
                continue;
            }
            
            if (field.IsArray)
            {
                var type = field.Type.Description.InnerType.Kind == "Builtin"
                    ? field.Type.Description.InnerType.BuiltinType
                    : field.Type.Description.InnerType.Name;

                if (knownConversions.TryGetValue(type, out var knownType))
                {
                    writer.WriteLine($"\t\tpublic unsafe fixed {knownType} {field.Name}[{field.ArrayBounds}];");
                }
                else
                {
                    writer.WriteLine($"\t\tpublic unsafe fixed {type} {field.Name}[{field.ArrayBounds}];");
                    Console.WriteLine($"Unknown type {type} of field {field.Name} in {structItem.Name}");
                }

                requireConstructor = true;
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

                if (knownConversions.TryGetValue(type, out var knownType))
                {
                    writer.WriteLine($"\t\tpublic unsafe {knownType}* {field.Name};");
                }
                else
                {
                    writer.WriteLine($"\t\tpublic unsafe {type}* {field.Name};");
                    Console.WriteLine($"Unknown type {type} of field {field.Name} in {structItem.Name}");
                }
            }
            else
            {
                var type = field.Type.Declaration;
                if (knownConversions.TryGetValue(type, out var knownType))
                {
                    writer.WriteLine($"\t\tpublic {knownType} {field.Name};");
                }
                else
                {
                    writer.WriteLine($"\t\tpublic {type} {field.Name};");
                    Console.WriteLine($"Unknown type {type} of field {field.Name} in {structItem.Name}");
                }
            }
        }

        if (requireConstructor)
        {
            writer.WriteLine($"\t\tpublic {structItem.Name}()");
            writer.WriteLine("\t\t{");
            writer.WriteLine("\t\t}");
        }

        knownConversions[structItem.Name] = structItem.Name;

        writer.WriteLine("\t}");
        writer.WriteLine();
    }

    writer.WriteLine("}");
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
    List<ConditionalItem> Conditionals
);

record StructItem(
    string Name,
    string OriginalFullyQualifiedName,
    string Kind,
    bool ByValue,
    bool ForwardDeclaration,
    bool IsAnonymous,
    List<StructItemField> Fields,
    Comments Comments,
    bool IsInternal
);

record StructItemField(
    string Name,
    bool IsArray,
    bool IsAnonymous,
    string? ArrayBounds,
    StructItemFieldType Type,
    List<ConditionalItem> Conditionals,
    bool IsInternal
);

record StructItemFieldType(
    string Declaration,
    StructItemFieldTypeDescription Description
);

record StructItemFieldTypeDescription(
    string Kind,
    string BuiltinType,
    StructItemFieldTypeDescriptionInnerType InnerType
);

record StructItemFieldTypeDescriptionInnerType(
    string Kind,
    string? Name,
    string? BuiltinType,
    StructItemFieldTypeDescriptionInnerType? InnerType
);

record TypedefItem(
    string Name,
    TypedefType Type,
    Comments Comments,
    List<ConditionalItem>? Conditionals);

record TypedefType(
    string Declaration,
    TypedefTypeDescription Description,
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

record Comments(string Attached, string[]? Preceding);

record ConditionalItem(
    string Condition,
    string Expression);