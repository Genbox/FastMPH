# FastMPH

[![NuGet](https://img.shields.io/nuget/v/Genbox.FastMPH.svg?style=flat-square&label=nuget)](https://www.nuget.org/packages/Genbox.FastMPH/)
[![License](https://img.shields.io/github/license/Genbox/FastMPH)](https://github.com/Genbox/FastMPH/blob/master/LICENSE.txt)

### Description

A C# port of the minimal perfect hash function library [CMPH](https://cmph.sourceforge.net/).

### Features

Supports the following algorithms:

* [BDZ](https://cmph.sourceforge.net/bdz.html) (MPH + PH)
* [BMZ](https://cmph.sourceforge.net/bmz.html) (MPH)
* [CHD](https://cmph.sourceforge.net/chd.html) (MPH + PH)
* [CHM](https://cmph.sourceforge.net/chm.html) (MPH)
* [FCH](https://cmph.sourceforge.net/fch.html) (MPH)

Other features:

* Pack/unpack each hash function to a `Span<byte>`
* Logging is supported via [Microsoft.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Extensions.Logging/)

### Example usage

```csharp
ChdBuilder<string> builder = new ChdBuilder<string>(NullLogger<ChdBuilder<string>>.Instance);

string[] data =
[
    "elephant",
    "goat",
    "horse",
    "cow"
];

if (!builder.TryCreate(data, out var state))
{
    Console.WriteLine("Unable to create perfect hash function");
    return;
}

foreach (string item in data)
{
    Console.WriteLine($"Hashcode for {item}: {state.Search(item)}");
}
```

Output:

```
Hashcode for elephant: 9
Hashcode for goat: 10
Hashcode for horse: 6
Hashcode for cow: 1
```

### FAQ

#### What is a Perfect Hash (PH) function?

Before diving into perfect hash functions, let me explain the challenges with a normal hash function.
A normal hash function takes in, for example, a string and outputs an integer in the range [0, 2^32-1].

Let's say `hash("goat")` gives us `4197513`

In order to use that hash function in a hash table/set, we need to modulo the hash output with the number of items in the table/set.

```csharp
var items = ["elephant", "goat", "horse", "cow"]
```

If we hash each of them and modulo with 4, we get the following values:

```csharp
hash("elephant") % 4 = 1
hash("goat") % 4 = 0
hash("horse") % 4 = 2
hash("cow") % 4 = 1
```

As can be seen, both "elephant" and "cow" gets the same index. That is what we call a hash collision. In a hash table/set this has to be addressed, usually done
via [chaining or open addressing](https://en.wikipedia.org/wiki/Hash_table#Collision_resolution).

A Perfect Hash is a hash function that maps a set of `n` keys to `n` unique integers with no collisions. Therefore there is no need for collision resolution.

#### What is a Minimal Perfect Hash (MPH) function?

A Minimal Perfect Hash is a perfect hash function that has the added benefit of hashing to a range of [0, n-1].

There are usually "holes" in the output of a perfect hash:

```csharp
PH("elephant") = 2
PH("goat") = 1
PH("horse") = 5
PH("cow") = 6
```

There are no holes in a minimal perfect hash:

```csharp
MPH("elephant") = 3
MPH("goat") = 1
MPH("horse") = 0
MPH("cow") = 2
```

#### What are the differences compared to CMPH?

All:

* Moving large allocations out of loops
* Lazy loading lookup tables to reduce memory usage
* Some implementations had their number of iterations hardcoded. I've made them configurable.
* Some implementations used modulus to reduce the keyspace of the seed, but the hash function don't care, so I've removed the reduction.

BDZ:

* It did 100 iterations with the same 16 hash functions. It now does `n` iterations with random hash functions.

BMZ:

* Use 2 seeds instead of 3. The third seed was never used.

#### What can I use it for?

This library implements several PH/MPH functions intended to be used for mapping a value to an integer.
Its primary use case is for mapping values in hash tables/sets.

It only benefits situations when:

- Data is completely static
- Your dataset is too big for other perfect hash functions
- You are using a mapping table and want to reduce memory usage

### Benchmarks

Benchmarks are sorted from fastest to slowest.

* `Dict` is the .NET Dictionary implementation.
* `_M` means it is the minimal variant of the hash function.

```
| Method    | name  | Mean              | Allocated |
|---------- |------ |------------------:|----------:|
| Query     | Dict  |          8.708 ns |         - |
| Query     | BMZ_M |         18.199 ns |         - |
| Query     | BDZ   |         19.355 ns |         - |
| Query     | CHM_M |         19.770 ns |         - |
| Query     | FCH_M |         19.973 ns |         - |
| Query     | CHD   |         30.335 ns |         - |
| Query     | BDZ_M |         31.963 ns |         - |
| Query     | CHD_M |         44.504 ns |         - |
| Construct | Dict  |      8,638.064 ns |   31016 B |
| Construct | CHD   |     49,194.324 ns |   39156 B |
| Construct | CHD_M |     67,581.087 ns |   47831 B |
| Construct | BDZ_M |    159,898.128 ns |   80575 B |
| Construct | BDZ   |    164,998.722 ns |  250414 B |
| Construct | BDZ_M |    165,906.307 ns |  250334 B |
| Construct | CHM_M |    243,532.397 ns |   83903 B |
| Construct | FCH_M | 21,347,443.750 ns | 1321044 B |

```