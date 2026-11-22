using CommunityToolkit.Mvvm.ComponentModel;

using MsSql.Mimic.Domain.Models;

namespace MsSql.Mimic.Ui.ViewModel;

/// <summary>
/// Represents an observable view model for a database column used when configuring generated property metadata.
/// </summary>
/// <remarks>
/// Provides editable column metadata for the UI and supports conversion to and from a domain property definition.
/// </remarks>
public sealed partial class ColumnViewModel : ObservableObject
{
    /// <summary>
    /// Gets or sets a value indicating whether the column is included.
    /// </summary>
    [ObservableProperty]
    public partial bool IsIncluded { get; set; } = true;

    /// <summary>
    /// Gets or sets the name of the database column.
    /// </summary>
    [ObservableProperty]
    public partial string ColumnName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the generated property for the column.
    /// </summary>
    [ObservableProperty]
    public partial string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the C# type name used for the generated property.
    /// </summary>
    [ObservableProperty]
    public partial string CSharpTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the column allows null values.
    /// </summary>
    [ObservableProperty]
    public partial bool IsNullable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is part of the primary key.
    /// </summary>
    [ObservableProperty]
    public partial bool IsPrimaryKey { get; set; }

    /// <summary>
    /// Gets or sets the maximum length defined for the column, or <see langword="null"/> when no maximum length is specified.
    /// </summary>
    [ObservableProperty]
    public partial int? MaxLength { get; set; }

    /// <summary>
    /// Gets or sets the numeric precision of the column.
    /// </summary>
    [ObservableProperty]
    public partial int? Precision { get; set; }

    /// <summary>
    /// Gets or sets the numeric scale of the column.
    /// </summary>
    [ObservableProperty]
    public partial int? Scale { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is a row version column.
    /// </summary>
    [ObservableProperty]
    public partial bool IsRowVersion { get; set; }

    /// <summary>
    /// Converts this column view model into a property definition.
    /// </summary>
    /// <returns>A property definition populated with the column metadata from this view model.</returns>
    public PropertyDefinition ToPropertyDefinition()
    {
        return new PropertyDefinition
        {
            ColumnName = ColumnName,
            PropertyName = PropertyName,
            CSharpTypeName = CSharpTypeName,
            IsNullable = IsNullable,
            IsPrimaryKey = IsPrimaryKey,
            MaxLength = MaxLength,
            Precision = Precision,
            Scale = Scale,
            IsRowVersion = IsRowVersion
        };
    }

    /// <summary>
    /// Creates a column view model from the specified property definition.
    /// </summary>
    /// <param name="property">The property definition containing the column metadata to copy.</param>
    /// <returns>A column view model populated with values from the specified property definition.</returns>
    public static ColumnViewModel FromPropertyDefinition(PropertyDefinition property)
    {
        var columnViewModel = new ColumnViewModel
        {
            ColumnName = property.ColumnName,
            PropertyName = property.PropertyName,
            CSharpTypeName = property.CSharpTypeName,
            IsNullable = property.IsNullable,
            IsPrimaryKey = property.IsPrimaryKey,
            MaxLength = property.MaxLength,
            Precision = property.Precision,
            Scale = property.Scale,
            IsRowVersion = property.IsRowVersion
        };

        return columnViewModel;
    }
}