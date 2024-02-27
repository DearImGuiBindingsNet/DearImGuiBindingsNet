namespace DearImguiGenerator;

public class CSharpCodeWriter
{
    private int _currentIndentLevel;

    private string _currentIndent = "";
    
    private readonly List<string> _lines = [];
    
    public void WriteLine(string line)
    {
        _lines.Add(_currentIndent + line);
    }

    public void PushBlock()
    {
        WriteLine("{");
        _currentIndentLevel++;
        _currentIndent = new string('\t', _currentIndentLevel);
    }

    public void PopBlock()
    {
        WriteLine("}");
        _currentIndentLevel--;
        _currentIndent = new string('\t', _currentIndentLevel);
    }
}