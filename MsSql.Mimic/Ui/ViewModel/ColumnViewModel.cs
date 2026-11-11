using CommunityToolkit.Mvvm.ComponentModel;

using MsSql.Mimic.Domain.Models;

namespace MsSql.Mimic.Ui.ViewModel;

public sealed partial class ColumnViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsIncluded { get; set; } = true;

    [ObservableProperty]
    public partial string ColumnName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PropertyName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CSharpTypeName { get; set; } = string.Empty;
    
    [ObservableProperty]
    public partial bool IsNullable { get; set; }
    
    [ObservableProperty]
    public partial bool IsPrimaryKey { get; set; }
    
    [ObservableProperty]
    public partial int? MaxLength { get; set; }
    
    [ObservableProperty]
    public partial AccessorKind AccessorKind { get; set; }

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
            AccessorKind = AccessorKind
        };
    }

    public static ColumnViewModel FromPropertyDefinition(PropertyDefinition property)
    {
        return new ColumnViewModel
        {
            ColumnName = property.ColumnName,
            PropertyName = property.PropertyName,
            CSharpTypeName = property.CSharpTypeName,
            IsNullable = property.IsNullable,
            IsPrimaryKey = property.IsPrimaryKey,
            MaxLength = property.MaxLength,
            AccessorKind = property.AccessorKind
        };
    }
}