# FastMPH

[![NuGet](https://img.shields.io/nuget/v/Genbox.FastMPH.svg?style=flat-square&label=nuget)](https://www.nuget.org/packages/Genbox.FastMPH/)
[![License](https://img.shields.io/github/license/Genbox/FastMPH)](https://github.com/Genbox/FastMPH/blob/master/LICENSE.txt)

### Description
A C# port of the minimal perfect hash function library [CMPH](https://cmph.sourceforge.net/).

### Features

Supports the following algorithms:
* BDZ (MPH + PH)
* BMZ (MPH)
* CHD (MPH + PH)
* CHM (MPH)
* FCH (MPH)

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

As can be seen, both "elephant" and "cow" gets the same index. That is what we call a hash collision. In a hash table/set this has to be addressed, usually done via [chaining or open addressing](https://en.wikipedia.org/wiki/Hash_table#Collision_resolution).

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

### Benchmarks
Benchmarks are sorted from fastest to slowest.
* `Dict` is the .NET Dictionary implementation.
* `_M` means it is the minimal variant of the hash function.

```
| Method    | name  | Mean              | Allocated |
|---------- |------ |------------------:|----------:|
| Query     | Dict  |          8.807 ns |         - |
| Query     | BDZ   |         20.659 ns |         - |
| Query     | BMZ_M |         21.738 ns |         - |
| Query     | CHM_M |         22.436 ns |         - |
| Query     | FCH_M |         23.851 ns |         - |
| Query     | BDZ_M |         29.129 ns |         - |
| Query     | CHD   |         30.247 ns |      40 B |
| Query     | CHD_M |         63.735 ns |      40 B |
| Construct | Dict  |      8,749.216 ns |   31016 B |
| Construct | CHD   |     71,271.997 ns |  163385 B |
| Construct | CHD_M |     95,512.809 ns |  172069 B |
| Construct | BDZ_M |    159,941.805 ns |   80591 B |
| Construct | BDZ   |    172,580.868 ns |  253063 B |
| Construct | BDZ_M |    173,580.322 ns |  246738 B |
| Construct | CHM_M |    251,787.630 ns |   83839 B |
| Construct | FCH_M | 28,029,897.917 ns | 1208516 B |
```