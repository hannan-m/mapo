using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Mapo.Generator.Syntax;

public class ParameterRewriter : CSharpSyntaxRewriter
{
    private readonly string _oldName;
    private readonly string _newName;
    private readonly ImmutableHashSet<string> _methodParams;
    private readonly Dictionary<string, string> _injectedRenames;

    public ParameterRewriter(
        string oldName,
        string newName,
        ImmutableHashSet<string> methodParams,
        Dictionary<string, string>? injectedRenames = null
    )
    {
        _oldName = oldName;
        _newName = newName;
        _methodParams = methodParams;
        _injectedRenames = injectedRenames ?? new Dictionary<string, string>();
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var name = node.Identifier.Text;
        if (name == _oldName)
        {
            return SyntaxFactory.IdentifierName(_newName).WithTriviaFrom(node);
        }

        if (_injectedRenames.TryGetValue(name, out var fieldName))
        {
            return SyntaxFactory.IdentifierName(fieldName).WithTriviaFrom(node);
        }

        if (_methodParams.Contains(name))
        {
            return node;
        }

        return base.VisitIdentifierName(node);
    }
}
