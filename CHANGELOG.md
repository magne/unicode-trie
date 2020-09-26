# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.0.2] - 2020-09-27 (Not released)

### Changed
- Minimize exposed API from auxillary classes.
- Change to C# documentation for public API.
- Rename inconsistent naming to C# convention.

### Removed

The `RangeIterator` class and the "java" `Iterable` and `Iterator` interfaces have been removed, replaced with the
standard .Net `IEnumerable` interface.

> Note that this is an unreleased version, since the API will change to avoid exposing "Java" types.

## [0.0.1] - 2020-09-20 (Not released)

This is the initial port of the ICU CodePointTrie, based on commit `release-64-rc2-681-g1baf0ea9b9`
(`1baf0ea9b9f20b2f30d82e612dd26525751cadfb`). The initial port changes as little as possible of the
ICU4J code, even porting some code from the OpenJDK codebase to make it run. All tests from the
original codebase, except two testcases requiring the Unicode database, run green.

The code under `unicode-trie/java` is a port of code that is copyright Free Software Foundation (FSF) under the
GLP v2 + classpath exception. The ICU code (other code) is a port of code that is copyright Unicode, Inc. and others,
under the [Unicode License & terms of use](http://www.unicode.org/copyright.html).

> Note that this is an unreleased version, since the API will change later on to conform to C# naming standards.