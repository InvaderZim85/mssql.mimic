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
            builder.AppendLine();
        }
        
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

            // Check if the table has a key. If not, attach the "Keyless" attribute.
            // If more than one key exists, add the "PrimaryKey" attribute with the key columns
            if (!options.TableHasPrimaryKey)
                builder.AppendLine("[Keyless]");
            else if (options.Properties.Count(c => c.IsPrimaryKey) > 1)
                AppendPrimaryKeyAttribute(builder, options.Properties.Where(w => w.IsPrimaryKey));
        }

        builder.AppendLine($"internal sealed class {options.ClassName}");
        builder.AppendLine("{"); // Opening bracket

        foreach (var property in options.Properties)
            AppendProperty(builder, property, options.UseEfCoreAttributes, options.GenerateSummary, options.AccessorKind);
        
        builder.AppendLine("}"); // Closing bracket

        return builder.ToString();
    }

    /// <summary>
    /// Appends a C# property declaration to the specified source builder using the provided property metadata and generation options.
    /// </summary>
    /// <param name="builder">The builder that receives the generated property source code.</param>
    /// <param name="property">The metadata that defines the column name, C# type, property name, nullability, key status, and related property details.</param>
    /// <param name="useEfCoreAttributes">A value indicating whether Entity Framework Core attributes should be emitted for the property.</param>
    /// <param name="generateSummary">A value indicating whether XML summary documentation should be emitted for the property.</param>
    /// <param name="accessorKind">The accessor style to use when generating the property declaration.</param>
    private void AppendProperty(
        StringBuilder builder,
        PropertyDefinition property,
        bool useEfCoreAttributes,
        bool generateSummary,
        AccessorKind accessorKind)
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

            if (property is { CSharpTypeName: "string" or "string?" })
            {
                var maxLength = property.MaxLength.HasValue
                    ? property.MaxLength.Value <= 0
                        ? "int.MaxValue"
                        : property.MaxLength.Value.ToString()
                    : "int.MaxValue";

                builder.AppendLine($"{_spacer}[MaxLength({maxLength})]");
            }
            
            if (property is { IsNullable: false, CSharpTypeName: "string" or "byte[]" })
                builder.AppendLine($"{_spacer}[Required]");
        }

        var accessorText = accessorKind switch
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

    /// <summary>
    /// Gets the default property initializer text for the specified C# type name.
    /// </summary>
    /// <param name="csharpType">The C# type name for which to determine the default initializer.</param>
    /// <returns>The default initializer text for the type, or an empty string when no default initializer is required.</returns>
    private static string GetDefaultValue(string csharpType)
    {
        return csharpType switch
        {
            "string" => " = string.Empty;",
            "byte[]" => " = [];",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Appends a PrimaryKey attribute declaration to the specified source builder using the provided key property definitions.
    /// </summary>
    /// <param name="builder">The builder to which the PrimaryKey attribute declaration is appended.</param>
    /// <param name="keyProperties">The property definitions that represent the primary key columns to include in the attribute.</param>
    private void AppendPrimaryKeyAttribute(StringBuilder builder, IEnumerable<PropertyDefinition> keyProperties)
    {
        builder.Append("[PrimaryKey(");

        builder.Append(string.Join(", ", from property in keyProperties
            let propertyName = string.IsNullOrWhiteSpace(property.PropertyName)
                ? nameService.ToPropertyName(property.ColumnName)
                : property.PropertyName
            select $"nameof({propertyName})"));
        
        builder.AppendLine(")]");
    }
}