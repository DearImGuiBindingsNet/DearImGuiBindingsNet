namespace DearImguiGenerator;

public class CSharpCodeWriter
{
    private int _currentIndentLevel;

    private string _currentIndent = "";

    const string genNamespace = "ImGuiNative";
    const string constantsClass = "ImGuiConsts";
    const string functionsClass = "ImGuiNative";
    const string outDir = "../DearImGuiBindings/generated";

    private StreamWriter _writer = null!;

    public CSharpCodeWriter()
    {
        var dirInfo = new DirectoryInfo(outDir);
        if (!dirInfo.Exists)
        {
            dirInfo.Create();
        }
    }

    public void WriteLine(string line)
    {
        if (line.Length == 0)
        {
            _writer.WriteLine();
        }
        else
        {
            _writer.WriteLine(_currentIndent + line);
        }
    }

    public void WriteLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            WriteLine(line);
        }
    }

    public void PushBlock()
    {
        WriteLine("{");
        _currentIndentLevel++;
        _currentIndent = new string('\t', _currentIndentLevel);
    }

    public void PopBlock()
    {
        _currentIndentLevel--;
        _currentIndent = new string('\t', _currentIndentLevel);
        WriteLine("}");
    }

    public void Flush()
    {
        _writer.Flush();
    }

    public void WriteConsts(IEnumerable<CSharpConstant> constants)
    {
        _writer = new StreamWriter(Path.Combine(outDir, "ImGui.Constants.cs"));

        WriteLine($"namespace {genNamespace};");
        WriteLine("");

        WriteLine($"public static partial class {constantsClass}");

        PushBlock();

        foreach (var sharpConstant in constants)
        {
            WriteSummaries(sharpConstant);

            WriteLine($"public const {sharpConstant.Type} {sharpConstant.Name} = {sharpConstant.Value};");
            WriteLine("");
        }

        PopBlock();
    }

    private IEnumerable<string> GenSummary(string comment)
    {
        yield return "<summary>";
        yield return new System.Xml.Linq.XText(comment).ToString();
        yield return "</summary>";
    }

    private IEnumerable<string> GenSummary(IEnumerable<string> comments)
    {
        yield return "<summary>";
        foreach (var comment in comments)
        {
            yield return $"<para>{new System.Xml.Linq.XText(comment)}</para>";
        }

        yield return "</summary>";
    }

    public void WriteSummaries(CSharpDefinition definition)
    {
        bool hasPreceding = definition.PrecedingComment is not null;
        if (hasPreceding)
        {
            WriteLines(
                GenSummary(definition.PrecedingComment!)
                    .Select(x => $"/// {x}")
            );
        }

        if (definition.TrailingComment is not null)
        {
            WriteLines(
                GenSummary(definition.TrailingComment)
                    .Select((x, i) => $"/// {(hasPreceding && i == 0 ? "<para/>" : "")}{x}")
            );
        }
    }

    private string JoinModifiers(CSharpDefinition def)
    {
        if (def.Modifiers.Count == 0)
        {
            return "";
        }
        else
        {
            return string.Join(" ", def.Modifiers) + " ";
        }
    }

    private string JoinArguments(CSharpDelegate def)
    {
        if (def.Arguments.Count == 0)
        {
            return "";
        }
        else
        {
            return string.Join(", ", def.Arguments);
        }
    }

    private string JoinArguments(CSharpFunction def)
    {
        if (def.Arguments.Count == 0)
        {
            return "";
        }
        else
        {
            return string.Join(", ", def.Arguments);
        }
    }

    public void WriteEnums(IEnumerable<CSharpEnum> enums)
    {
        _writer.Dispose();

        _writer = new StreamWriter(Path.Combine(outDir, "ImGui.Enums.cs"));

        WriteLine($"namespace {genNamespace};");
        WriteLine("");

        foreach (var e in enums)
        {
            WriteSummaries(e);

            WriteLine($"{JoinModifiers(e)}enum {e.Name.TrimEnd('_')}");
            PushBlock();

            foreach (var eValue in e.Values)
            {
                WriteSummaries(eValue);

                WriteLine($"{JoinModifiers(eValue)}{eValue.Name} = {eValue.Value},");
                WriteLine("");
            }

            PopBlock();
        }
    }

    public void WriteStructs(List<CSharpStruct> structs)
    {
        _writer.Dispose();

        _writer = new StreamWriter(Path.Combine(outDir, "ImGui.Structs.cs"));

        WriteLine($"namespace {genNamespace};");
        WriteLine($"using static {constantsClass};");
        WriteLine("");

        foreach (var s in structs)
        {
            WriteSummaries(s);

            WriteLine($"{JoinModifiers(s)}struct {s.Name}");
            PushBlock();

            foreach (var sField in s.Fields)
            {
                WriteSummaries(sField);

                if (sField.IsArray)
                {
                    WriteLine($"{JoinModifiers(sField)}{sField.Type} {sField.Name}[{sField.ArrayBound}];");
                }
                else
                {
                    WriteLine($"{JoinModifiers(sField)}{sField.Type} {sField.Name};");
                }

                WriteLine("");
            }

            PopBlock();
        }
    }

    public void WriteDelegates(List<CSharpDelegate> delegates)
    {
        _writer.Dispose();

        _writer = new StreamWriter(Path.Combine(outDir, "ImGui.Delegates.cs"));

        WriteLine("using System.Runtime.InteropServices;");
        WriteLine("");
        WriteLine($"namespace {genNamespace};");
        WriteLine("");

        foreach (var cSharpDelegate in delegates)
        {
            WriteSummaries(cSharpDelegate);

            WriteLines(cSharpDelegate.Attributes.Select(x => $"[{x}]"));
            WriteLine($"{JoinModifiers(cSharpDelegate)}delegate {cSharpDelegate.ReturnType} {cSharpDelegate.Name}({JoinArguments(cSharpDelegate)});");

            WriteLine("");
        }
    }

    public void WriteInlineArrays(List<CSharpStruct> inlineArrays)
    {
        _writer.Dispose();

        _writer = new StreamWriter(Path.Combine(outDir, "ImGui.InlineArrays.cs"));

        WriteLine("using System.Runtime.CompilerServices;");
        WriteLine("");

        WriteLine($"namespace {genNamespace};");
        WriteLine($"using static {constantsClass};");
        WriteLine("");

        foreach (var arr in inlineArrays)
        {
            WriteSummaries(arr);

            WriteLines(arr.Attributes.Select(x => $"[{x}]"));
            WriteLine($"{JoinModifiers(arr)}struct {arr.Name}");
            PushBlock();

            foreach (var sField in arr.Fields)
            {
                WriteSummaries(sField);

                WriteLine($"{JoinModifiers(sField)}{sField.Type} {sField.Name};");

                WriteLine("");
            }

            PopBlock();
        }
    }

    public void WriteFunctions(List<CSharpFunction> functions)
    {
        _writer.Dispose();

        _writer = new StreamWriter(Path.Combine(outDir, "ImGui.Functions.cs"));

        WriteLine("using System.Runtime.InteropServices;");
        WriteLine("");

        WriteLine($"namespace {genNamespace};");
        WriteLine($"using static {constantsClass};");
        WriteLine("");

        WriteLine($"public static class {functionsClass}");

        PushBlock();

        foreach (var func in functions)
        {
            WriteSummaries(func);

            WriteLines(func.Attributes.Select(x => $"[{x}]"));
            WriteLine($"{JoinModifiers(func)}{func.ReturnType} {func.Name}({JoinArguments(func)});");
            WriteLine("");
        }

        PopBlock();
    }
}