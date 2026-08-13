# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-08-13

### Added

- `TypeIdRegistry` for bidirectional mapping between types and integer IDs,
  with explicit registration, attribute-based registration, and assembly
  scanning.
- Computed IDs derived from a type's full name (or alias) by hashing its UTF-8
  bytes with 32-bit FNV-1a; computed IDs are always negative, leaving positive
  IDs for manual assignment. The computation is a compatibility contract for
  the 1.x line.
- `[TypeId]` attribute supporting computed IDs, explicit IDs, and aliases;
  an alias keeps the computed ID stable across type renames.
- `Moquestra.TypeIds.SourceGenerator`, a Roslyn source generator that emits a
  per-assembly `TypeIdMap` with IDs determined at compile time and
  switch-based lookup methods, and reports diagnostics `MQTID001`-`MQTID005`.
- Strong-named assemblies. The runtime library targets `netstandard2.1`; the
  source generator runs on Roslyn 4.3+ compiler hosts.
