// This file has been moved to FileGroupBuilder/FileGroupBuilderService.cs
// with a new, more flexible architecture.
//
// The service registration in Program.cs should now reference:
// PdfConversion.Services.FileGroupBuilderService
//
// See FileGroupBuilder/UsageExamples.cs for migration patterns from old to new API.
//
// The new service maintains backward compatibility through [Obsolete] methods
// while providing a powerful fluent query builder for new implementations.