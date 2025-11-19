using System.Globalization;

namespace Vilip.Utilties.EnvironmentTransformer.VariableTransformers;


public class EnvSnakeCaseToPascalTransformer : IVariableTransformer
{
    private readonly Matcher? _matcher;

    private readonly CultureInfo _cultureInfo;

    public EnvSnakeCaseToPascalTransformer(Matcher? matcher)
    {
        _matcher = matcher;
        _cultureInfo = Thread.CurrentThread.CurrentCulture;
    }

    public string Apply(string value)
    {
        if(_matcher != null && !_matcher(value)) return value;

        var sections = value
            .Split(["__"], StringSplitOptions.None)
            .Select(SnakeCaseToPascal);

        return string.Join("__", sections);
    }

    private string SnakeCaseToPascal(string value)
    {
        var words = value
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => _cultureInfo.TextInfo.ToTitleCase(w.ToLower(_cultureInfo)));

        return string.Concat(words);
    }
}
