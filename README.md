# unicode-trie

A data structure for fast Unicode character metadata lookup, ported from ICU.

## Background

When implementing many Unicode algorithms such as text segmentation, normalization, bidi
processing, etc., fast access to character metadata is crucial to good performance.  There
are over a million code points in the Unicode standard, many of which produce the same
result when looked up, so an array or hash table is not appropriate - those data structures
are fast but would require a lot of memory.  The data is generally grouped in ranges, so
you could do a binary search, but that is not fast enough for some applications.

The [International Components for Unicode](http://site.icu-project.org) (ICU) project
came up with a data structure based on a [Trie](http://en.wikipedia.org/wiki/Trie) that
provides fast access to Unicode metadata.  The range data is precompiled to a serialized
and flattened trie, which is then used at runtime to lookup the necessary data.

This repository contains the [CodePoint Trie](http://site.icu-project.org/design/struct/utrie),
ported from the ICU4J Java library.

## Installation

```bash
dotnet add package CodeHive.unicode-trie
```

## Building a Trie

Unicode Tries are generally precompiled from data in the Unicode database
for faster runtime performance.  To build a Unicode Trie, use the
`UnicodeTrieBuilder` class.

```c#
using CodeHive.unicode_trie

// create a trie
var t = new MutableCodePointTrie();

// optional parameters for default value, and error value
// if not provided, both are set to 0
t = new MutableCodePointTrie(10, 999);

// set individual values and ranges
t.set(0x4567, 99);
t.setRange(0x40, 0xe7, 0x1234);

// you can lookup a value if you like
t.get(0x4567); // => 99

// get a compiled trie (returns a CodePointTrie object)
var trie = t.buildImmutable(CodePointTrie.Type.FAST, CodePointTrie.ValueWidth.BITS_16);

// write compressed trie to a stream
trie.toBinary(stream)
```

## Using a precompiled Trie

Once you've built a precompiled trie, you can load it into the
`CodePointTrie` class, which is a readonly representation of the
trie.  From there, you can lookup values.

```c#
using CodeHive.unicode_trie

// load serialized trie from stream
var trie = CodePointTrie.fromBinary(null, null, stream);

// lookup a value
trie.get(0x4567); // => 99
```

## License

MIT
