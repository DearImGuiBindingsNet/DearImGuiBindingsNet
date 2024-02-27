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

        _resultDelegates.AddRange(typedefDelegates.Where(x => !x.Arguments.Any(y => y.Type == "__arglist")));
        _resultDelegates.AddRange(_delegates.Where(x => !x.Arguments.Any(y => y.Type == "__arglist")));
        
        _resultConstants.AddRange(_globalConstants);
        _resultConstants.AddRange(_enumConstants);
        
        _resultEnums.AddRange(_enums);
        _resultStructs.AddRange(_structs);
        _resultFunctions.AddRange(_functions);
    }
}