# MsSql.Mimic

**MsSql.Mimic** is a Windows desktop application for generating C# model classes from Microsoft SQL Server database schemas.

The application reads table and column metadata from an MS SQL Server database, allows users to customize generated class and property names, and produces clean C# code that can optionally include *Entity Framework Core* attributes.

## Overview

MsSql.Mimic is designed to simplify the process of creating C# classes from existing SQL server tables.

Instead of manually writing model classes, users can connect to a database, select a table, review its columns, adjust naming and accessor options, and generate read-to-use C# code.

The project is built as a WPF application and follows a layered architecture that separates UI, application logic, database access, persistence, and code generation.

## Features

- Connect to Microsoft SQL Server databases
- Load available database tables
- Read table column metadata
- Detect SQL data types, nullable columns, primary keys, length, precision, and scale
- Map SQL Server data types to C# types
- Generate C# classes from database tables
- Customize generated class names
- Customize generated property names
- Chose property accessors such as `get; set;` or `get; init;`
- Optionally generate *Entity Framework Core* attributes
- Store user-defined aliases and mappings locally
- Reuse previously saved naming decisions
- Syntax-highlighted code preview
- Modern WPF-based desktop interface

## Technology Stack

- .NET
- WPF
- C#
- Microsoft SQL Server
- SQLite
- Entity Framework Core
- Dapper
- CommunityToolkit.Mvvm
- AvalonEdit
- MahApps.Metro
- Serilog

## Requirements

- Windows
- .NET SDK compatible with the target framework
- Access to a Microsoft SQL Server instance
- A database user with permissions to read schema metadata

## Project structure

