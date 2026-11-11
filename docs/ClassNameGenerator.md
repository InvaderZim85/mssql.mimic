---
tags:
  - mssql
  - classgenerator
---
Du brauchst im Grunde eine Methode, die aus einem SQL-Spaltennamen einen gültigen **C#-Property-Namen in PascalCase** macht.

Da du `CSharpNameService` bereits hast, könnte die Methode dort z. B. so aussehen:

```csharp
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MsSql.ClassGenerator.Domain.Services;

public sealed class CSharpNameService
{
    public string ToPropertyName(string columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
        {
            return "Property";
        }

        var parts = Regex.Split(columnName.Trim(), @"[^a-zA-Z0-9]+")
            .Where(part => !string.IsNullOrWhiteSpace(part));

        var builder = new StringBuilder();

        foreach (var part in parts)
        {
            builder.Append(ToPascalCasePart(part));
        }

        var propertyName = builder.Length == 0
            ? "Property"
            : builder.ToString();

        if (char.IsDigit(propertyName[0]))
        {
            propertyName = "_" + propertyName;
        }

        return propertyName;
    }

    private static string ToPascalCasePart(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Length == 1)
        {
            return value.ToUpperInvariant();
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }
}
```


Damit würden z. B. diese Spaltennamen so umgewandelt:

| SQL-Spaltenname | Property-Name |
|---|---|
| `customer_id` | `CustomerId` |
| `first_name` | `FirstName` |
| `created_at` | `CreatedAt` |
| `USER_NAME` | `USERNAME` |
| `123_name` | `_123Name` |

Wenn du auch Schreibweisen wie `USER_NAME` schöner als `UserName` erzeugen möchtest, kannst du die Hilfsmethode leicht anpassen:

```csharp
private static string ToPascalCasePart(string value)
{
    if (string.IsNullOrEmpty(value))
    {
        return string.Empty;
    }

    value = value.ToLowerInvariant();

    if (value.Length == 1)
    {
        return value.ToUpperInvariant();
    }

    return char.ToUpperInvariant(value[0]) + value[1..];
}
```


Dann wird aus:

```plain text
USER_NAME
```


korrekt:

```csharp
UserName
```


In deiner Factory verwendest du das dann bereits richtig:

```csharp
var propertyName = columnAlias?.PropertyName ?? nameService.ToPropertyName(column.Name);
```


Das bedeutet:

- Wenn ein Alias vorhanden ist, wird der gespeicherte Property-Name verwendet.
- Wenn kein Alias vorhanden ist, wird automatisch aus dem Spaltennamen ein Property-Name erzeugt.