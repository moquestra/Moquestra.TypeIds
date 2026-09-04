; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.2.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MQTID012 | Moquestra.TypeIds | Warning | Domains differing only by casing
MQTID013 | Moquestra.TypeIds | Warning | Generated constant name collision

## Release 1.1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MQTID006 | Moquestra.TypeIds | Warning | Generated namespace was sanitized
MQTID007 | Moquestra.TypeIds | Error | Invalid domain name
MQTID008 | Moquestra.TypeIds | Error | Invalid map name
MQTID009 | Moquestra.TypeIds | Error | Duplicate map name designation
MQTID010 | Moquestra.TypeIds | Error | Generated map name collision
MQTID011 | Moquestra.TypeIds | Warning | Map name for an unknown domain

## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MQTID001 | Moquestra.TypeIds | Warning | Type not accessible to the generated lookup
MQTID002 | Moquestra.TypeIds | Error | Alias cannot be null or empty
MQTID003 | Moquestra.TypeIds | Error | ID mapped to more than one type
MQTID004 | Moquestra.TypeIds | Error | Generated lookup type conflicts with an existing type
MQTID005 | Moquestra.TypeIds | Error | Generic types are not supported
