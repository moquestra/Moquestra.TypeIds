# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-08-27

### Added

- `ExcludeFromGeneratedMap` on `[TypeId]` to keep a type out of the generated
  map while leaving runtime registration unaffected.
- Domain-scoped generated maps: types declared with
  `[TypeId(..., Domain = "Auth")]` share the Auth domain's map (`AuthTypeIdMap`
  under fallback naming). Duplicate IDs are detected per domain, and invalid
  domain names produce `MQTID007`. Domains are a source-generator concept;
  runtime registration and ID computation ignore them.
- `TypeIdMapName` assembly attributes to configure the full name of each
  generated map, including a `{Domain}` template for named domains and
  per-domain overrides, with diagnostics `MQTID008`-`MQTID011`.

### Fixed

- Automatically derived map namespaces are now sanitized when the project's
  root namespace or assembly name cannot be used directly. The generator
  reports the substitution with warning `MQTID006`; for example, Unity's
  default `Assembly-CSharp` assembly now generates maps under
  `Assembly_CSharp.Generated`.

Verified on Unity 2022.3 LTS and Unity 6 (6000.3).

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
