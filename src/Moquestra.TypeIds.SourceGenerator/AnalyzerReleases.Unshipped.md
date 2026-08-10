; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MQTID001 | Moquestra.TypeIds | Warning | Type not accessible to the generated lookup
MQTID002 | Moquestra.TypeIds | Error | Alias cannot be null or empty
MQTID003 | Moquestra.TypeIds | Error | ID mapped to more than one type
MQTID004 | Moquestra.TypeIds | Error | Generated lookup type conflicts with an existing type
