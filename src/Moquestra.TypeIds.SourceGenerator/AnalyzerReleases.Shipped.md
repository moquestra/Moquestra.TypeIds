; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MQTID001 | Moquestra.TypeIds | Warning | Type not accessible to the generated lookup
MQTID002 | Moquestra.TypeIds | Error | Alias cannot be null or empty
MQTID003 | Moquestra.TypeIds | Error | ID mapped to more than one type
MQTID004 | Moquestra.TypeIds | Error | Generated lookup type conflicts with an existing type
MQTID005 | Moquestra.TypeIds | Error | Generic types are not supported
