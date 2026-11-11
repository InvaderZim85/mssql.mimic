using MsSql.Mimic.Application.Options;
using MsSql.Mimic.Domain.Models;
using MsSql.Mimic.Domain.Services;

using System.Text;

namespace MsSql.Mimic.Application.Services;

/// <summary>
/// Generates C# class source code from table metadata and class generation options.
/// </summary>
/// <param name="nameService">
/// The service used to convert database identifiers into valid C# names.
/// </param>
public sealed class CSharpClassGenerator(CSharpNameService nameService)
{
    /// <summary>
    /// Represents the indentation text used when generating class member source lines.
    /// </summary>
    private readonly string _spacer = new(' ', 4);

    /// <summary>
    /// Generates C# source code for a class based on the specified class generation options.
    /// </summary>
    /// <param name="options">The options that define the namespace, class name, table metadata, attributes, summaries, and properties to include in the generated class.</param>
    /// <returns>The generated C# source code.</returns>
    public string Generate(ClassGenerationOptions options)
    {
        var builder = new StringBuilder();

        if (options.UseEfCoreAttributes)
        {
            builder.AppendLine("using System.ComponentModel.DataAnnotations;");
            builder.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
        }

        builder.AppendLine();
        builder.AppendLine($"namespace {options.NamespaceName};");
        builder.AppendLine();

        if (options.GenerateSummary)
        {
            builder.AppendLine("/// <summary>");
            builder.AppendLine($"/// Represents the database table <c>[{options.SchemaName}].[{options.TableName}]</c>.");
            builder.AppendLine("/// </summary>");
        }
        
        if (options.UseEfCoreAttributes)
        {
            builder.AppendLine($"""[Table("{options.TableName}", Schema = "{options.SchemaName}")]""");

            if (options.Properties.Count(c => c.IsPrimaryKey) > 1)
                AppendPrimaryKeyAttribute(builder, options.Properties.Where(w => w.IsPrimaryKey));
        }

        builder.AppendLine($"internal sealed class {options.ClassName}");
        builder.AppendLine("{"); // Opening bracket

        foreach (var property in options.Properties)
            AppendProperty(builder, property, options.UseEfCoreAttributes, options.GenerateSummary);
        
        builder.AppendLine("}"); // Closing bracket

        return builder.ToString();
    }

    private void AppendProperty(
        StringBuilder builder,
        PropertyDefinition property,
        bool useEfCoreAttributes,
        bool generateSummary)
    {
        builder.AppendLine();

        if (generateSummary)
        {
            builder.AppendLine($"{_spacer}/// <summary>");
            builder.AppendLine($"{_spacer}/// Maps to column <c>[{property.ColumnName}]</c>.");
            builder.AppendLine($"{_spacer}/// </summary>");
        }
        
        if (useEfCoreAttributes)
        {
            if (property.IsPrimaryKey)
                builder.AppendLine($"{_spacer}[Key]");
            
            builder.AppendLine($"""{_spacer}[Column("{property.ColumnName}")]""");

            if (property is { MaxLength: > 0, CSharpTypeName: "string" or "string?" })
                builder.AppendLine($"{_spacer}[MaxLength({property.MaxLength.Value})]");

            if (property is { IsNullable: false, CSharpTypeName: "string" or "byte[]" })
                builder.AppendLine($"{_spacer}[Required]");
        }

        var accessorText = property.AccessorKind switch
        {
            AccessorKind.GetSet => "get; set;",
            AccessorKind.GetInit => "get; init;",
            _ => "get; set;"
        };

        var defaultValue = GetDefaultValue(property.CSharpTypeName);

        var propertyName = string.IsNullOrWhiteSpace(property.PropertyName)
            ? nameService.ToPropertyName(property.ColumnName)
            : property.PropertyName;
        
        builder.AppendLine($"{_spacer}public {property.CSharpTypeName} {propertyName} {{ {accessorText} }}{defaultValue}");
    }
    
    private static string GetDefaultValue(string csharpType)
    {
        return csharpType switch
        {
            "string" => " = string.Empty;",
            "byte[]" => " = [];",
            _ => string.Empty
        };
    }

    private void AppendPrimaryKeyAttribute(StringBuilder builder, IEnumerable<PropertyDefinition> keyProperties)
    {
        builder.Append("[PrimaryKey(");

        builder.Append(string.Join(", ", from property in keyProperties
            let propertyName = string.IsNullOrWhiteSpace(property.PropertyName)
                ? nameService.ToPropertyName(property.ColumnName)
                : property.PropertyName
            select propertyName));
        
        builder.AppendLine(")]");
    }
}