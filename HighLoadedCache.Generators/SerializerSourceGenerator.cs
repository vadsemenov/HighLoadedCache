using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace HighLoadedCache.Generators
{
    [Generator]
    public class SerializerSourceGenerator : ISourceGenerator
    {
        private static readonly DiagnosticDescriptor UnsupportedTypeRule =
            new DiagnosticDescriptor(
                "SG0001",
                "Unsupported property type",
                "Property '{0}' in type '{1}' has unsupported type '{2}' for binary serialization",
                "GenerateSerializer",
                DiagnosticSeverity.Error,
                true);

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SerializerSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var receiver = context.SyntaxReceiver as SerializerSyntaxReceiver;
            if (receiver == null)
                return;

            var compilation = context.Compilation;
            var serializableTypes = new List<SerializableType>();

            foreach (var classDecl in receiver.Candidates)
            {
                var semanticModel = compilation.GetSemanticModel(classDecl.SyntaxTree);
                var classSymbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
                if (classSymbol == null)
                    continue;

                if (!HasGenerateSerializerAttribute(classSymbol))
                    continue;

                var serializableType = BuildSerializableType(context, classSymbol);
                if (serializableType != null)
                {
                    serializableTypes.Add(serializableType);
                }
            }

            foreach (var type in serializableTypes)
            {
                var source = GenerateSerializerClass(type);

                context.AddSource(
                    type.TypeName + ".Serializer.g.cs",
                    SourceText.From(source, Encoding.UTF8));
            }
        }

        private static bool HasGenerateSerializerAttribute(INamedTypeSymbol classSymbol)
        {
            foreach (var attr in classSymbol.GetAttributes())
            {
                var attrClass = attr.AttributeClass;
                if (attrClass == null)
                    continue;

                var name = attrClass.Name;
                var fullName = attrClass.ToDisplayString();

                if (name == "GenerateBinarySerializerAttribute" ||
                    fullName == "HighLoadedCache.Domain.Dto.GenerateBinarySerializerAttribute")
                {
                    return true;
                }
            }

            return false;
        }

        private static SerializableType BuildSerializableType(
            GeneratorExecutionContext context,
            INamedTypeSymbol classSymbol)
        {
            var props = new List<SerializableProperty>();

            foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
            {
                if (member.DeclaredAccessibility != Accessibility.Public)
                    continue;
                if (member.GetMethod == null)
                    continue;

                var type = member.Type;
                if (!IsSupportedType(type))
                {
                    ReportUnsupportedProperty(context, member, classSymbol);
                    continue;
                }

                var canonicalTypeName = GetCanonicalTypeName(type);
                props.Add(new SerializableProperty(member.Name, canonicalTypeName));
            }

            var ns = classSymbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : classSymbol.ContainingNamespace.ToDisplayString();

            return new SerializableType(ns, classSymbol.Name, props);
        }

        private static bool IsSupportedType(ITypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Int32:
                case SpecialType.System_Int64:
                case SpecialType.System_Double:
                case SpecialType.System_Boolean:
                case SpecialType.System_String:
                case SpecialType.System_DateTime:
                    return true;
                default:
                    return false;
            }
        }

        private static void ReportUnsupportedProperty(
            GeneratorExecutionContext context,
            IPropertySymbol property,
            INamedTypeSymbol classSymbol)
        {
            var location = property.Locations.FirstOrDefault();

            var diagnostic = Diagnostic.Create(
                UnsupportedTypeRule,
                location,
                property.Name,
                classSymbol.Name,
                property.Type.ToDisplayString());

            context.ReportDiagnostic(diagnostic);
        }

        private static string GetCanonicalTypeName(ITypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Int32:
                    return "int";
                case SpecialType.System_Int64:
                    return "long";
                case SpecialType.System_Double:
                    return "double";
                case SpecialType.System_Boolean:
                    return "bool";
                case SpecialType.System_String:
                    return "string";
                case SpecialType.System_DateTime:
                    return "DateTime";
                default:
                    return type.ToDisplayString();
            }
        }

        private static string GenerateSerializerClass(SerializableType type)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(type.Namespace))
            {
                sb.Append("namespace ").Append(type.Namespace).AppendLine(";");
                sb.AppendLine();
            }

            sb.Append("public partial class ").Append(type.TypeName).AppendLine();
            sb.AppendLine("{");
            sb.AppendLine("    public void SerializeToBinary(Stream stream)");
            sb.AppendLine("    {");
            sb.AppendLine("        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))");
            sb.AppendLine("        {");

            foreach (var prop in type.Properties)
            {
                AppendWriteForProperty(sb, prop);
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void AppendWriteForProperty(StringBuilder sb, SerializableProperty prop)
        {
            switch (prop.TypeName)
            {
                case "int":
                    sb.Append("            writer.Write(this.")
                        .Append(prop.Name)
                        .AppendLine(");");
                    break;

                case "long":
                    sb.Append("            writer.Write(this.")
                        .Append(prop.Name)
                        .AppendLine(");");
                    break;

                case "double":
                    sb.Append("            writer.Write(this.")
                        .Append(prop.Name)
                        .AppendLine(");");
                    break;

                case "bool":
                    sb.Append("            writer.Write(this.")
                        .Append(prop.Name)
                        .AppendLine(");");
                    break;

                case "string":
                    sb.AppendLine("            if (this." + prop.Name + " == null)");
                    sb.AppendLine("            {");
                    sb.AppendLine("                writer.Write(-1);");
                    sb.AppendLine("            }");
                    sb.AppendLine("            else");
                    sb.AppendLine("            {");
                    sb.AppendLine("                var bytes = System.Text.Encoding.UTF8.GetBytes(this." + prop.Name + ");");
                    sb.AppendLine("                writer.Write(bytes.Length);");
                    sb.AppendLine("                writer.Write(bytes);");
                    sb.AppendLine("            }");
                    break;

                case "DateTime":
                    sb.Append("            writer.Write(this.")
                        .Append(prop.Name)
                        .AppendLine(".ToBinary());");
                    break;

                default:
                    sb.Append("            // Unsupported type: ")
                        .Append(prop.TypeName)
                        .AppendLine();
                    break;
            }
        }
    }
}