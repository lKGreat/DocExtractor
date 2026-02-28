# AGENTS.md

Guidance for coding agents working in this repository.

## 1) Quick Start

- Solution: `DocExtractor.sln`
- Main build command:

```bash
dotnet build DocExtractor.sln
```

- Build a specific project:

```bash
dotnet build DocExtractor.Core/DocExtractor.Core.csproj
```

- Clean + rebuild:

```bash
dotnet clean DocExtractor.sln && dotnet build DocExtractor.sln
```

- Publish UI (Windows self-contained):

```bash
dotnet publish DocExtractor.UI/DocExtractor.UI.csproj -c Release -o ./publish
```

There is currently no dedicated test project.

## 2) Project Layout

Dependency order (low to high):

1. `DocExtractor.Core` (`netstandard2.0`) - interfaces, models, pipeline, splitters
2. `DocExtractor.Parsing` (`netstandard2.0`) - Word/Excel parsing, depends on Core
3. `DocExtractor.ML` (`netstandard2.0`) - ML.NET models, depends on Core
4. `DocExtractor.Data` (`netstandard2.0`) - SQLite + export, depends on Core + ML
5. `DocExtractor.UI` (`net48`, WinExe) - WinForms app, depends on all above

## 3) Data Pipeline

Core flow:

`File -> IDocumentParser -> RawTable[] -> table filtering -> IColumnNormalizer -> ExtractedRecord[] -> IRecordSplitter chain -> Excel/JSON export`

Important models:

- `RawTable`: merge-aware cell grid (`GetValue(r,c)` follows shadow cells)
- `ExtractedRecord.Fields`: dictionary of normalized field values
- `ExtractionConfig`: extraction schema + split rules throughout the pipeline

## 4) Column Normalization Behavior

Default mode is `HybridMlFirst`, with fallback order:

1. Exact match on `FieldName`/`DisplayName` (confidence `1.0`)
2. ML prediction (`column_classifier.zip`) when confidence >= `0.6`
3. Variant matching via `KnownColumnVariants`
   - exact variant: `0.95`
   - contains: `0.75`

If model file is missing, behavior falls back to rule-based matching.

## 5) .NET Standard 2.0 Safety Rules

Avoid language/API usages that break `netstandard2.0` library builds:

- Avoid range/index syntax (`str[..n]`, `list[^1]`), use `Substring`/index arithmetic
- Avoid `.ToHashSet()`, use `new HashSet<T>(...)`
- Do not use `TextFeaturizingEstimator.CaseNormalizationMode` (ML.NET 2.0 limitation)
- `LightGbmMulticlassTrainer.Options` requires:
  - `using Microsoft.ML.Trainers.LightGbm;`
  - NuGet package `Microsoft.ML.LightGbm`

## 6) Splitter Extension Rules

All splitters implement `IRecordSplitter` and live in:

- `DocExtractor.Core/Splitting/`

When adding a splitter:

1. Implement `IRecordSplitter`
2. Add it to the splitter array in `ExtractionPipeline` constructor
3. Respect `SplitRule.Priority` (lower runs earlier)

Splitters are chained; each splitter consumes the previous output.

## 7) ML Model Files and Training

Model directory: `{AppPath}/models/`

- Column classifier: `column_classifier.zip`
- NER: `ner_model.zip`
- Section classifier: `section_classifier.zip`

`UnifiedDocModel.LoadAll(modelsDir)` should safely skip missing files.

`UnifiedModelTrainer.TrainAll(...)` runs a staged sequence:

1. Column
2. NER
3. Section

`TrainingParameters` presets:

- `Fast()`
- `Standard()`
- `Fine()`

## 8) UI Architecture (DocExtractor.UI)

### Overall Structure

The UI follows a **MainForm + UserControl** architecture. MainForm is a thin shell; all feature logic lives in dedicated UserControl panels.

```
DocExtractor.UI/
├── Forms/          ← MainForm + standalone dialog Forms
├── Controls/       ← UserControl panels (one per feature area)
├── Context/        ← DocExtractorContext (shared state container)
├── Services/       ← Internal workflow services
├── Helpers/        ← MessageHelper etc.
└── Logging/        ← UiFileLoggerProvider
```

### Navigation Model

MainForm has a **left navigation panel** (vertical buttons) and a **right content panel**. Switching features calls `ShowPanel(UserControl)` which:
1. Removes the active UserControl from `_contentPanel`
2. Docks the new UserControl to Fill and adds it
3. Calls `OnActivated()` on the panel for any refresh needed

Do **not** use `TabControl` for main navigation — use this content-swap pattern.

### 3-File Convention for Forms and UserControls

Every Form and UserControl uses exactly 3 files:

```
ClassName.cs           ← constructor, event wiring, thin handlers, no layout
ClassName.Designer.cs  ← InitializeComponent() only, all control declarations
ClassName.resx         ← embedded resources (leave empty if unused)
```

Rules:
- `*.Designer.cs` must be **VS designer-compatible**: include `AutoScaleDimensions`, `AutoScaleMode`, `SuspendLayout`/`ResumeLayout`, and `this.Name`. No conditional logic, no business logic.
- `*.cs` wires events in the constructor and delegates to small focused methods.
- All control fields declared in `*.Designer.cs` must be `private`.

### Code Size and Responsibility Limits

- **Max ~300 lines** per `.cs` file (designer files may run slightly longer for complex layouts)
- **Max ~40 lines** per method — single responsibility only
- One concern per method: an event handler either updates UI state **or** calls a service **or** shows a notification — never all three inline
- Extract repeated UI patterns into dedicated private helper methods

### Shared State: `DocExtractorContext`

All UserControl panels receive a `DocExtractorContext` via their constructor (dependency injection). Never instantiate services or repositories directly inside a panel.

`DocExtractorContext` exposes:
- Paths: `DbPath`, `ModelsDir`, `StartupPath`
- ML models: `ColumnModel`, `NerModel`, `SectionModel`
- Services: `ConfigService`, `ExtractionService`, `TrainingService`, `RecommendationService`
- Loggers: `Logger` (pipeline), `TrainLogger` (training)
- Events: `ConfigChanged`, `ConfigListChanged`, `ModelsReloaded`, `StatusMessage`, `LogLine`, `TrainLogLine`

### Cross-Panel Communication

Panels communicate through `DocExtractorContext` events, not direct references:
- Panel fires an event on `_ctx` → MainForm or other panels subscribe and react
- Example: `FieldConfigPanel` raises `ConfigListChanged` → MainForm reloads the config combo

### Dialog Extraction Rule

**Never use inline `new Form()`** for dialogs. Every dialog must be a proper 3-file Form class in `Forms/`. Two standard dialogs already exist: `InputDialogForm` (single text input) and `ExportFieldSelectionForm` (field checklist).

### Notifications

- `MessageHelper.Info/Success/Warn/Error(Control, string)` — accepts any Control (Form or UserControl), wraps AntdUI toast notifications, auto-dismissed
- Reserve `MessageBox.Show` only for destructive irreversible confirmations (delete config, overwrite data)

### AntdUI Component Usage

Use AntdUI equivalents instead of raw WinForms controls where available:

| Need | AntdUI component |
|------|-----------------|
| Button | `AntdUI.Button` |
| Input / TextBox | `AntdUI.Input` |
| Table / Grid | `AntdUI.Table` |
| Progress | `AntdUI.Progress` |
| Notification / Status | `AntdUI.Message`, `AntdUI.Badge` |
| Menu | `AntdUI.Menu` |
| Select / ComboBox | `AntdUI.Select` |
| Loading | `AntdUI.Spin` |

## 9) EPPlus Requirement

Before using `ExcelPackage`, set:

`ExcelPackage.LicenseContext = LicenseContext.NonCommercial;`

Do not remove this requirement from parser/exporter code.

## 10) Data/Knowledge Storage Notes

- `DocExtractor.Data/Schema/schema.sql` does not include every runtime-created table.
- Some tables are ensured lazily in code (for example section training and group knowledge support tables).

## 11) Agent Working Style

- Keep changes minimal and scoped to the request.
- Preserve current architecture unless explicitly asked to refactor.
- When adding tests in the future, target `netstandard2.0` for library-facing test projects unless requirements differ.
- Prefer clear, descriptive commit messages.
