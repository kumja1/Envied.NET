# Envied.NET

[![NuGet](https://img.shields.io/nuget/v/Envied.NET.svg)](https://www.nuget.org/packages/Envied.NET)
[![License: MIT](https://img.shields.io/github/license/kumja1/Envied.NET)](https://opensource.org/licenses/MIT)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Envied.NET.svg)](https://www.nuget.org/stats/packages/Envied.NET?groupby=Version)

A cleaner way to handle your environment variables in C#.

(C# port of the Dart/Flutter library [Envied](https://pub.dev/packages/envied))

## Table of Contents

- [Overview](#overview)
  - [Why Envied](#why-envied)
  - [Getting Started](#getting-started)
- [Installation](#installation)
- [Usage](#usage)
  - [Obfuscation/Encryption](#obfuscationencryption)
  - [**Optional Environment Variables**](#optional-environment-variables)
  - [**Environment Variable Naming Conventions**](#environment-variable-naming-conventions)
  - [Using System Environment Variables](#using-system-environment-variables)
- [Examples](#examples)
- [License](#license)

## Overview

### Why Envied
Envied, unlike `IConfiguration`, offers you the ability to encrypt your environment variables using types and metadata unique to your assembly. This ensures that encrypted values are tightly bound to your assembly. However, this encryption is only as secure as your ability to protect your assembly from unauthorized access.


### Getting Started
Using an `.env` file such as:

```
# .env

KEY=VALUE
```

or system environment variables such as:

```sh
export VAR=test
```

and a C# class:

```csharp
using Envied;

// .NET 9 and up
[Envied]
public static partial class Env
{
    [EnviedField(varName: "KEY")]
    public static partial readonly string Key { get; }
}

// .NET 8 and older
[Envied]
public static class Env
{
    [EnviedField(varName: "KEY")]
    public static readonly string Key => Env_Generated.Key;
}
```

The `Envied` library will generate the necessary code to access environment variables easily.

You can then use the `Env` class to access your environment variable:

```csharp
Console.WriteLine(Env.Key); // "VALUE"
```

## Installation

Install the `Envied` NuGet package:

```sh
dotnet add package Envied.NET
dotnet add package Envied.NET.SourceGenerator
```

## Usage

Add a `.env` file at the root of the project. The name of this file can be specified in your `Envied` attribute if you call it something else such as `.env.dev`.

```
# .env.dev

KEY1=VALUE1
KEY2=VALUE2
```

Create a class to ingest the environment variables:

```csharp
using Envied;

// .NET 9 and up
[Envied(path: ".env.dev")]
public static partial class Env
{
    [EnviedField(varName: "KEY1")]
    public static partial readonly string Key1 { get; }

    [EnviedField(varName: "KEY2")]
    public static partial readonly string Key2 { get; }
}


// .NET 8 and older
[Envied(path: ".env.dev")]
public static class Env
{
    [EnviedField(varName: "KEY1")]
    public static readonly string Key1 => Env_Generated.Key1;

    [EnviedField(varName: "KEY2")]
    public static readonly string Key2 => Env_Generated.Key2;
}
```
Then, generate the required code by running:

```sh
dotnet build
```
You can also just type in the file to see the changes immediately.

You can then use the `Env` class to access your environment variables:

```csharp
Console.WriteLine(Env.Key1); // "VALUE1"
Console.WriteLine(Env.Key2); // "VALUE2"
```

### Obfuscation/Encryption
To obfuscate an environment variable value, add the `obfuscate` argument to your `EnviedField` attribute:

```csharp
[EnviedField(obfuscate: true)]
```

This obfuscates(encrypts) the variable's value using the types and metadata in your assembly.

### Optional Environment Variables

Set `allowOptionalFields` to true to allow nullable types. When a default value is not provided and the type is nullable, the generator will assign `null` instead of throwing an exception.

Since strings are reference types in C#, you don't have to specify the nullability explicitly:

```csharp
// .NET 9 and up
[Envied(allowOptionalFields: true)]
public static partial class Env
{
    [EnviedField]
    public static partial readonly string? OptionalServiceApiKey { get; }
}

// .NET 8 and older
[Envied(allowOptionalFields: true)]
public static class Env
{
    [EnviedField]
    public static readonly string? OptionalServiceApiKey => Env_Generated.OptionalServiceApiKey;
}
```

Optional fields can also be enabled per field:

```csharp
// .NET 9 and up
[EnviedField(optional: true)]
public static partial readonly string? OptionalServiceApiKey { get; }

// .NET 8 and older 
[EnviedField(optional: true)]
public static readonly string? OptionalServiceApiKey => Env_Generated.OptionalServiceApiKey;
```


### Environment Variable Naming Conventions

Set `useConstantCase` to `true` to convert field names from `camelCase` to `CONSTANT_CASE` automatically:

```csharp
// .NET 9 and up
[Envied(useConstantCase: true)]
public static partial class Env
{
    [EnviedField]
    public static partial readonly string ApiKey { get; }
}

// .NET 8 and older
[Envied(useConstantCase: true)]
public static class Env
{
    [EnviedField]
    public static readonly string ApiKey => Env_Generated.ApiKey;
}
```

Or specify the name explicitly:

```csharp
// .NET 9 and up
[EnviedField(varName: "API_KEY")]
public static partial readonly string ApiKey { get; }

// .NET 8 and older
[EnviedField(varName: "API_KEY")]
public static readonly string ApiKey => Env_Generated.ApiKey;
```
### Using System Environment Variables

Using the `environment` option in either an `Envied` or `EnviedField` instructs the generator to use the value from the `.env` file as the key for a system environment variable read from `Enviroment`

For example, let's use the `Envied` class and the following `.env` files:

```csharp
// .NET 9 and up
[Envied(environment: true)]
public static partial class Env
{
    [EnviedField(varName: "API_KEY")]
    public static partial readonly string ApiKey { get; }
}

// .NET 8 and older
[Envied(environment: true)]
public static class Env
{
    [EnviedField(varName: "API_KEY")]
    public static readonly string ApiKey => Env_Generated.ApiKey;
}
```

... or ...

```csharp
// .NET 9 and up
[Envied]
public static partial class Env
{
    [EnviedField(environment: true, varName: "API_KEY")]
    public static partial readonly string ApiKey { get; }
}

// .NET 8 and older
[Envied]
public static class Env
{
    [EnviedField(environment: true, varName: "API_KEY")]
    public static readonly string ApiKey => Env_Generated.ApiKey;
}
```

`.env.latest`

```
API_KEY=LATEST_API_KEY
```

`.env.stage`

```
API_KEY=STAGE_API_KEY
```

Depending on which `.env` file you use to generate, the `API_KEY` can be read from either `Enviroment.GetEnvironmentVariable('LATEST_API_KEY')` or `Enviroment.GetEnvironmentVariable('STAGE_API_KEY')`. This allows for the `.env` files to be safely checked into source control, the specific values to be set at build time, and keeps the secrets safely stored in the host environment.

## Examples
More examples can be found [here](https://github.com/kumja1/Envied.NET/tree/master/examples)

## License

This project is licensed under the MIT License.

