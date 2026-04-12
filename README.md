# FastMPH

[![NuGet](https://img.shields.io/nuget/v/Genbox.FastMPH.svg?style=flat-square&label=nuget)](https://www.nuget.org/packages/Genbox.FastMPH/)
[![License](https://img.shields.io/github/license/Genbox/FastMPH)](https://github.com/Genbox/FastMPH/blob/main/LICENSE.txt)

### Description

A library of (minimal) perfect hash functions.

### Features

Supports the following algorithms:

| Algorithm | Supports | Author(s)                                                         | Source                                                 | Paper                                                  |
|-----------|----------|-------------------------------------------------------------------|--------------------------------------------------------|--------------------------------------------------------|
| BDZ       | PH / MPH | Fabiano C. Botelho, Rasmus Pagh, Nivio Ziviani                    | [Link](https://cmph.sourceforge.net/bdz.html)          | [Link](https://cmph.sourceforge.net/papers/wads07.pdf) |
| BBHash    | MPH      | Antoine Limasset, Guillaume Rizk, Rayan Chikhi, Pierre Peterlongo | [Link](https://github.com/rizkg/BBHash)                | [Link](https://arxiv.org/abs/1702.03154)               |
| BMZ       | MPH      | Fabiano C. Botelho, Yoshiharu Kohayakawa, Nivio Ziviani           | [Link](https://cmph.sourceforge.net/bmz.html)          | [Link](https://cmph.sourceforge.net/papers/wea05.pdf)  |
| CHD       | PH / MPH | Djamal Belazzougui, Fabiano C. Botelho, Martin Dietzfelbinge      | [Link](https://cmph.sourceforge.net/chd.html)          | [Link](https://cmph.sourceforge.net/papers/esa09.pdf)  |
| CHM       | MPH      | Zbigniew J. Czech, George Havas, Bohdan S. Majwski                | [Link](https://cmph.sourceforge.net/chm.html)          | [Link](https://cmph.sourceforge.net/papers/chm92.pdf)  |
| FCH       | MPH      | Edward A. Fox, Qi Fan Chen, Lenwood S. Heath                      | [Link](https://cmph.sourceforge.net/fch.html)          | [Link](https://cmph.sourceforge.net/papers/fch92.pdf)  |
| Hyble     | PH       | Alisa Sireneva                                                    | [Link](https://github.com/purplesyringa/h)             | -                                                      |
| PtrHash   | MPH      | Ragnar Groot Koerkamp                                             | [Link](https://github.com/RagnarGrootKoerkamp/ptrhash) | [Link](https://arxiv.org/abs/2502.15539)               |

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

if (!builder.TryCreateWithRetry(data, value => unchecked((ulong)value.GetHashCode()), out var state))
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

A Perfect Hash is a hash function that maps a set of `n` keys to `m` unique integers with no collisions. Therefore, there is no need for collision resolution.

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

#### What can I use it for?

This library implements several PH/MPH functions intended to be used for mapping a value to an integer.
Its primary use case is for mapping values in hash tables/sets.

It only benefits situations when:

- Data is completely static
- Your dataset is too big for other perfect hash functions
- You are using a mapping table and want to reduce memory usage

### Benchmarks

Benchmarks are sorted from fastest to slowest. It is testing construction on 100k 32bit integers, and the query time is for just one integer.

* `Dict` is the .NET Dictionary.
* 'FrozenDict' is the .NET FrozenDictionary.
* `_M` means it is the minimal variant of the hash function.

| Method    | Name       | BitsPerItem |                Mean |  Allocated |
|-----------|------------|-------------|--------------------:|-----------:|
| Query     | FrozenDict | 237.05      |           0.7450 ns |          - |
| Query     | Hyble      | 5.24        |           1.5592 ns |          - |
| Query     | Dict       | 173.82      |           1.5849 ns |          - |
| Query     | BMZ_M      | 36.8        |           4.1600 ns |          - |
| Query     | PTR_M      | 2.99        |           4.8370 ns |          - |
| Query     | CHM_M      | 66.88       |           5.5118 ns |          - |
| Query     | FCH_M      | 4.73        |           7.1922 ns |          - |
| Query     | BDZ        | 1.97        |           9.6764 ns |          - |
| Query     | BB_M       | 3.52        |          12.5208 ns |          - |
| Query     | BDZ_M      | 2.77        |          13.1758 ns |          - |
| Query     | CHD        | 2.98        |          20.3073 ns |          - |
| Query     | CHD_M      | 6.24        |          47.0493 ns |          - |
| Construct | Dict       | 173.82      |     962,823.2642 ns |  2172848 B |
| Construct | BB_M       | 3.52        |   2,808,516.6146 ns |   612712 B |
| Construct | FrozenDict | 237.05      |   3,350,931.3337 ns |  5936491 B |
| Construct | CHD        | 2.99        |   4,308,704.6875 ns |  3838711 B |
| Construct | Hyble      | 5.24        |   4,420,141.0156 ns |  1811418 B |
| Construct | CHD_M      | 6.25        |   6,337,032.0312 ns |  4679316 B |
| Construct | BMZ_M      | 36.8        |  16,668,686.8750 ns |  7123774 B |
| Construct | PTR_M      | 2.99        |  21,343,941.4583 ns |  2646940 B |
| Construct | BDZ        | 1.97        |  24,401,915.8967 ns | 13099011 B |
| Construct | BDZ_M      | 2.77        |  25,208,954.4062 ns | 13078269 B |
| Construct | CHM_M      | 66.88       |  27,335,151.4583 ns |  5961067 B |
| Construct | FCH_M      | 4.73        | 470,084,043.6170 ns | 76979208 B |
