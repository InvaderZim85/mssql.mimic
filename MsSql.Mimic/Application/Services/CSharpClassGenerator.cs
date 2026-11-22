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
            builder.AppendLine("using Microsoft.EntityFrameworkCore;");
            builder.AppendLine("using System.ComponentModel.DataAnnotations;");
            builder.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
            builder.AppendLine();
        }
        
        if (!string.IsNullOrWhiteSpace(options.NamespaceName))
        {
            builder.AppendLine($"namespace {options.NamespaceName};");
            builder.AppendLine();
        }

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

        for (var i = 0; i < options.Properties.Count; i++)
        {
            AppendProperty(
                builder: builder,
                property: options.Properties[i],
                useEfCoreAttributes: options.UseEfCoreAttributes,
                generateSummary: options.GenerateSummary,
                accessorKind: options.AccessorKind,
                isFirstProperty: i == 0);
        }
        
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
    /// <param name="isFirstProperty">Value that indicates whether the property is the first property in the list.</param>
    private void AppendProperty(
        StringBuilder builder,
        PropertyDefinition property,
        bool useEfCoreAttributes,
        bool generateSummary,
        AccessorKind accessorKind,
        bool isFirstProperty)
    {
        if (!isFirstProperty)
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

            // Add the max length attribute (if needed)
            AppendMaxLengthAttribute(builder, property);

            // Add the precision attribute (if needed)
            AppendPrecisionAttribute(builder, property);
           
            // Add the timestamp attribute (if needed)
            AppendRowVersionAttribute(builder, property);

            // Add the required attribute (if needed) 
            AppendRequiredAttribute(builder, property);
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

    /// <summary>
    /// Appends a MaxLength attribute for a string property, using the property's configured maximum length or int.MaxValue when no bounded length is defined.
    /// </summary>
    /// <param name="builder">The string builder to append the generated attribute line to.</param>
    /// <param name="property">The property metadata used to determine whether a MaxLength attribute should be emitted and which length value to apply.</param>
    private void AppendMaxLengthAttribute(StringBuilder builder, PropertyDefinition property)
    {
        if (property is not { CSharpTypeName: "string" or "string?" }) 
            return;
        
        var maxLength = property.MaxLength.HasValue
            ? property.MaxLength.Value <= 0
                ? "int.MaxValue"
                : property.MaxLength.Value.ToString()
            : "int.MaxValue";

        builder.AppendLine($"{_spacer}[MaxLength({maxLength})]");
    }

    /// <summary>
    /// Appends a precision attribute for a decimal property when precision metadata is available.
    /// </summary>
    /// <param name="builder">The string builder receiving the generated property attributes.</param>
    /// <param name="property">The property definition containing the C# type, precision, and scale metadata.</param>
    private void AppendPrecisionAttribute(StringBuilder builder, PropertyDefinition property)
    {
        if (property is not { CSharpTypeName: "decimal", Precision: not null }) 
            return;
        
        var precisionAttribute = property.Scale.HasValue
            ? $"[Precision({property.Precision.Value}, {property.Scale.Value})]"
            : $"[Precision({property.Precision.Value})]";

        builder.AppendLine($"{_spacer}{precisionAttribute}");
    }

    /// <summary>
    /// Appends a timestamp attribute to the generated property source when the property represents a row version column.
    /// </summary>
    /// <param name="builder">The string builder that receives the generated attribute line.</param>
    /// <param name="property">The property metadata used to determine whether the timestamp attribute should be added.</param>
    private void AppendRowVersionAttribute(StringBuilder builder, PropertyDefinition property)
    {
        if (property.IsRowVersion)
            builder.AppendLine($"{_spacer}[Timestamp]");
    }

    /// <summary>
    /// Appends a required attribute to the generated property when the property represents a non-nullable reference type that requires validation metadata.
    /// </summary>
    /// <param name="builder">The string builder receiving the generated attribute line.</param>
    /// <param name="property">The property metadata used to determine whether the required attribute should be added.</param>
    private void AppendRequiredAttribute(StringBuilder builder, PropertyDefinition property)
    {
        if (property is { IsNullable: false, CSharpTypeName: "string" or "byte[]" })
            builder.AppendLine($"{_spacer}[Required]");
    }
}