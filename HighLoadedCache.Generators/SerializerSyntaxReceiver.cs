using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HighLoadedCache.Generators
{
    public class SerializerSyntaxReceiver : ISyntaxReceiver
    {
        public List<ClassDeclarationSyntax> Candidates { get; } = new List<ClassDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            var classNode = syntaxNode as ClassDeclarationSyntax;

            if (classNode != null && classNode.AttributeLists.Count > 0)
            {
                Candidates.Add(classNode);
            }
        }
    }
}