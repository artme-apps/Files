// Copyright (c) 2024 Files Community
// Licensed under the MIT License. See the LICENSE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Files.Core.SourceGenerator
{
	internal static class SourceGeneratorHelper
	{
		internal const string AssemblyName = $"{nameof(Files)}.{nameof(SourceGenerator)}.";
		internal const string AssemblyVersion = "1.1.1";

		/// <summary>
		/// Generate the following code
		/// <code>
		/// public <paramref name="ctor" /> (...<paramref name="property" />.Type variable...)
		/// {
		///     <paramref name="property" />.Name = variable;
		/// }
		/// </code>
		/// </summary>
		/// <returns></returns>
		internal static ConstructorDeclarationSyntax GetDeclaration(IPropertySymbol property, ConstructorDeclarationSyntax ctor)
		{
			var newName = property.Name[..1].ToLower() + property.Name[1..];
			return ctor.AddParameterListParameters(
					Parameter(Identifier(newName)).WithType(property.Type.GetTypeSyntax(false)))
				.AddBodyStatements(ExpressionStatement(
					AssignmentExpression(
						SyntaxKind.SimpleAssignmentExpression,
						IdentifierName(property.Name),
						IdentifierName(newName))));
		}

		/// <summary>
		/// Generate the following code
		/// <code>
		/// partial <paramref name="symbol" /> <paramref name="name" />
		/// {
		///     <paramref name="member" />
		/// }
		/// </code>
		/// </summary>
		/// <returns>TypeDeclaration</returns>
		internal static TypeDeclarationSyntax GetDeclaration(string name, INamedTypeSymbol symbol, params MemberDeclarationSyntax[] member)
		{
			TypeDeclarationSyntax typeDeclarationTemp = symbol.TypeKind switch
			{
				TypeKind.Class when !symbol.IsRecord => ClassDeclaration(name),
				TypeKind.Struct when !symbol.IsRecord => StructDeclaration(name),
				TypeKind.Class or TypeKind.Struct when symbol.IsRecord => RecordDeclaration(Token(SyntaxKind.RecordKeyword), name),
				_ => throw new ArgumentOutOfRangeException(nameof(symbol.TypeKind))
			};
			return typeDeclarationTemp.AddModifiers(Token(SyntaxKind.PartialKeyword))
				.WithOpenBraceToken(Token(SyntaxKind.OpenBraceToken))
				.AddMembers(member)
				.WithCloseBraceToken(Token(SyntaxKind.CloseBraceToken));
		}

		/// <summary>
		/// Generate the following code
		/// <code>
		/// nameof(<paramref name="expressionSyntax" />)
		/// </code>
		/// </summary>
		/// <returns>NameOfExpression</returns>
		internal static InvocationExpressionSyntax NameOfExpression(ExpressionSyntax expressionSyntax)
		{
			return InvocationExpression(IdentifierName("nameof"), ArgumentList().AddArguments(Argument(expressionSyntax)));
		}

		/// <summary>
		/// Generate the following code
		/// <code>
		/// public static readonly DependencyProperty <paramref name="fieldName" /> = <paramref name="registration" />;
		/// </code>
		/// </summary>
		/// <returns>StaticFieldDeclaration</returns>
		internal static FieldDeclarationSyntax GetStaticFieldDeclaration(string fieldName, ExpressionSyntax registration)
		{
			return FieldDeclaration(VariableDeclaration(
				IdentifierName("global::Microsoft.UI.Xaml.DependencyProperty")))
					.AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.ReadOnlyKeyword))
					.AddDeclarationVariables(VariableDeclarator(fieldName).WithInitializer(EqualsValueClause(registration)));
		}

		/// <summary>
		/// Generate the following code
		/// <code>
		/// get => (<paramref name="type" />&lt;<paramref name="isNullable" />&gt;)GetValue(<paramref name="fieldName" />);
		/// </code>
		/// </summary>
		/// <returns>Getter</returns>
		internal static AccessorDeclarationSyntax GetGetter(string fieldName, bool isNullable, ITypeSymbol type)
		{
			ExpressionSyntax getProperty = InvocationExpression(GetThisMemberAccessExpression("GetValue"))
				.AddArgumentListArguments(Argument(IdentifierName(fieldName)));
			if (type.SpecialType != SpecialType.System_Object)
				getProperty = CastExpression(type.GetTypeSyntax(isNullable), getProperty);

			return AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
				.WithExpressionBody(ArrowExpressionClause(getProperty))
				.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
		}

		/// <summary>
		/// Generate the following code
		/// <code>
		/// &lt;<paramref name="isSetterPrivate" />&gt; set => SetValue(<paramref name="fieldName" />, value);
		/// </code>
		/// </summary>
		/// <returns>Setter</returns>
		internal static AccessorDeclarationSyntax GetSetter(string fieldName, bool isSetterPrivate)
		{
			ExpressionSyntax setProperty = InvocationExpression(GetThisMemberAccessExpression("SetValue"))
				.AddArgumentListArguments(Argument(IdentifierName(fieldName)), Argument(IdentifierName("value")));

			var setter = AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
				.WithExpressionBody(ArrowExpressionClause(setProperty))
				.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));

			return isSetterPrivate ? setter.AddModifiers(Token(SyntaxKind.PrivateKeyword)) : setter;
		}

		/// <summary>
		/// Generate the following code
		/// <code>
		/// public <paramref name="type" />&lt;<paramref name="isNullable" />&gt; <paramref name="propertyName" /> { <paramref name="getter" />; <paramref name="setter" />; }
		/// </code>
		/// </summary>
		/// <returns>PropertyDeclaration</returns>
		internal static PropertyDeclarationSyntax GetPropertyDeclaration(string propertyName, bool isNullable, ITypeSymbol type, AccessorDeclarationSyntax getter, AccessorDeclarationSyntax setter)
		{
			return PropertyDeclaration(type.GetTypeSyntax(isNullable), propertyName)
				.AddModifiers(Token(SyntaxKind.PublicKeyword))
				.AddAccessorListAccessors(getter, setter);
		}

		internal static AttributeListSyntax[] GetAttributeForField(string generatorName)
		{
			return
			[
				AttributeList().AddAttributes(Attribute(IdentifierName("global::System.CodeDom.Compiler.GeneratedCode"))
				.AddArgumentListArguments(
					AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(AssemblyName + generatorName))),
					AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(AssemblyVersion)))
				))
			];
		}

		internal static AttributeListSyntax[] GetAttributeForEvent(string generatorName)
		{
			return
			[
				AttributeList().AddAttributes(
					Attribute(IdentifierName("global::System.CodeDom.Compiler.GeneratedCode")).AddArgumentListArguments(
						AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(AssemblyName + generatorName))),
						AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(AssemblyVersion))))),
				AttributeList().AddAttributes(
					Attribute(IdentifierName("global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage")))
			];
		}

		internal static AttributeListSyntax[] GetAttributeForMethod(string generatorName)
		{
			return
			[
				AttributeList().AddAttributes(Attribute(IdentifierName("global::System.CodeDom.Compiler.GeneratedCode"))
					.AddArgumentListArguments(
						AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(AssemblyName + generatorName))),
						AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(AssemblyVersion)))
					)),
				AttributeList().AddAttributes(Attribute(IdentifierName("global::System.Diagnostics.DebuggerNonUserCode"))),
				AttributeList().AddAttributes(Attribute(IdentifierName("global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage")))
			];
		}

		/// <summary>
		/// Generate the following code
		/// <code>
		/// partial class <paramref name="specificType" /><br/>
		/// {
		///     <paramref name="members" /><br/>
		/// }
		/// </code>
		/// </summary>
		/// <returns>ClassDeclaration</returns>
		internal static ClassDeclarationSyntax GetClassDeclaration(ISymbol specificType, IList<MemberDeclarationSyntax> members)
		{
			for (var i = 0; i < members.Count - 1; i++)
				members[i] = members[i].WithTrailingTrivia(SyntaxTrivia(SyntaxKind.EndOfLineTrivia, "\n"));

			return ClassDeclaration(specificType.Name)
				.AddModifiers(Token(SyntaxKind.PartialKeyword))
				.AddMembers([.. members]);
		}

		/// <summary>
		/// Generate the following code
		/// <code>
		/// #nullable enable
		/// namespace <paramref name="specificClass" />.ContainingNamespace;<br/>
		/// <paramref name="generatedClass" />
		/// </code>
		/// </summary>
		/// <returns>FileScopedNamespaceDeclaration</returns>
		internal static FileScopedNamespaceDeclarationSyntax GetFileScopedNamespaceDeclaration(ISymbol specificClass, MemberDeclarationSyntax generatedClass)
		{
			return FileScopedNamespaceDeclaration(ParseName(specificClass.ContainingNamespace.ToDisplayString()))
				.AddMembers(generatedClass)
				.WithNamespaceKeyword(Token(SyntaxKind.NamespaceKeyword)
					.WithLeadingTrivia(Trivia(NullableDirectiveTrivia(Token(SyntaxKind.EnableKeyword), true))));
		}

		/// <summary>
		/// Generate the following code
		/// <code>
		/// using Microsoft.UI.Xaml;
		/// ...
		/// <br/><paramref name="generatedNamespace" />
		/// </code>
		/// </summary>
		/// <returns>CompilationUnit</returns>
		internal static CompilationUnitSyntax GetCompilationUnitWithUsings(MemberDeclarationSyntax generatedNamespace, IEnumerable<string> namespaces)
		{
			return CompilationUnit()
				.AddMembers(generatedNamespace)
				.AddUsings(namespaces.Select(ns => UsingDirective(ParseName(ns))).ToArray())
				.NormalizeWhitespace();
		}

		/// <summary>
		/// Generate the following code
		/// <code>
		/// using Microsoft.UI.Xaml;
		/// ...
		/// <br/><paramref name="generatedNamespace" />
		/// </code>
		/// </summary>
		/// <returns>CompilationUnit</returns>
		internal static CompilationUnitSyntax GetCompilationUnit(MemberDeclarationSyntax generatedNamespace)
		{
			return CompilationUnit()
				.AddMembers(generatedNamespace)
				.NormalizeWhitespace();
		}

		/// <summary>
		/// Generate the following code
		/// <code>
		/// <paramref name="typeSymbol" />&lt;<paramref name="isNullable" />&gt;
		/// </code>
		/// </summary>
		/// <returns>CompilationUnit</returns>
		public static TypeSyntax GetTypeSyntax(this ITypeSymbol typeSymbol, bool isNullable)
		{
			var typeName = ParseTypeName(typeSymbol.ToDisplayString());
			return isNullable ? NullableType(typeName) : typeName;
		}

		private static MemberAccessExpressionSyntax GetThisMemberAccessExpression(string name)
		{
			return MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				ThisExpression(),
				IdentifierName(name));
		}

		private static MemberAccessExpressionSyntax GetStaticMemberAccessExpression(this ITypeSymbol typeSymbol, string name)
		{
			return MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				IdentifierName(typeSymbol.ToDisplayString()),
				IdentifierName(name));
		}

		/// <summary>
		/// intent
		/// </summary>
		/// <param name="n">tab</param>
		/// <returns>4n*space</returns>
		internal static string Spacing(int n)
		{
			var temp = "";
			for (var i = 0; i < n; ++i)
				temp += "    ";
			return temp;
		}

		internal static IEnumerable<IPropertySymbol> GetProperties(this ITypeSymbol typeSymbol, INamedTypeSymbol attribute)
		{
			foreach (var member in typeSymbol.GetMembers())
			{
				if (member is not IPropertySymbol { Name: not "EqualityContract" } property)
					continue;

				yield return property;
			}
		}

		/// <summary>
		/// Fetch a namespace of <paramref name="symbol"/> and add to <paramref name="namespaces"/> collection
		/// </summary>
		/// <param name="namespaces">namespaces set</param>
		/// <param name="usedTypes">types that have been judged</param>
		/// <param name="contextType">The class in which the context resides</param>
		/// <param name="symbol">type's symbol</param>
		internal static void UseNamespace(this HashSet<string> namespaces, HashSet<ITypeSymbol> usedTypes, INamedTypeSymbol contextType, ITypeSymbol symbol)
		{
			if (usedTypes.Contains(symbol))
				return;

			_ = usedTypes.Add(symbol);

			if (symbol.ContainingNamespace is not { } ns)
				return;

			if (!SymbolEqualityComparer.Default.Equals(ns, contextType.ContainingNamespace))
				_ = namespaces.Add(ns.ToDisplayString());

			if (symbol is INamedTypeSymbol { IsGenericType: true } genericSymbol)
				foreach (var a in genericSymbol.TypeArguments)
					namespaces.UseNamespace(usedTypes, contextType, a);
		}

		/// <summary>
		/// Generate nullable prepared statements and reference namespaces
		/// <br/>#nullable enable
		/// <br/><see langword="using"/> ...;
		/// <br/><see langword="using"/> ...;
		/// <br/>...
		/// </summary>
		/// <param name="namespaces">namespaces set</param>
		internal static StringBuilder GenerateFileHeader(this HashSet<string> namespaces)
		{
			var stringBuilder = new StringBuilder().AppendLine("#nullable enable\n");
			foreach (var s in namespaces)
				_ = stringBuilder.AppendLine($"using {s};");
			return stringBuilder;
		}
	}
}
