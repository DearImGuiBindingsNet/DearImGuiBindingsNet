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
    }

    public void Preprocess()
    {
        var typedefDelegates = _typedefs.Where(x => x.Kind == CSharpDefinitionKind.Delegate).Cast<CSharpDelegate>();

        _delegates.AddRange(typedefDelegates);

        _typedefs.RemoveAll(x => x.Kind == CSharpDefinitionKind.Delegate);

        foreach (var cSharpEnum in _enums)
        {
            cSharpEnum.Modifiers.Add("public");
        }

        foreach (var cSharpConst in _enumConstants)
        {
            cSharpConst.Modifiers.Add("public");
        }
        
        // flatten inner declarations in structs
        foreach (var cSharpStruct in _structs)
        {
            cSharpStruct.Modifiers.Add("public");
            foreach (var cSharpDefinition in cSharpStruct.InnerDeclarations)
            {
                if (cSharpDefinition.Kind == CSharpDefinitionKind.Delegate)
                {
                    _delegates.Add((CSharpDelegate)cSharpDefinition);
                }
            }

            cSharpStruct.InnerDeclarations.RemoveAll(x => x.Kind == CSharpDefinitionKind.Delegate);
            
            foreach (var sField in cSharpStruct.Fields)
            {
                sField.Modifiers.Add("public");
                
                if (sField.Type.EndsWith('*'))
                {
                    sField.Modifiers.Add("unsafe");
                }

                if (sField.IsArray)
                {
                    sField.Modifiers.Add("fixed");
                }
            }
        }
        
        foreach (var cSharpDelegate in _delegates)
        {
            cSharpDelegate.Modifiers.Add("public");
            if (cSharpDelegate.ReturnType.EndsWith('*') || cSharpDelegate.Arguments.Any(x => x.Type.EndsWith('*')))
            {
                cSharpDelegate.Modifiers.Add("unsafe");
            }
        }
    }
}