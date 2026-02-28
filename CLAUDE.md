# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build entire solution
dotnet build DocExtractor.sln

# Build specific project
dotnet build DocExtractor.Core/DocExtractor.Core.csproj

# Clean + rebuild
dotnet clean DocExtractor.sln && dotnet build DocExtractor.sln

# Publish UI as self-contained Windows app
dotnet publish DocExtractor.UI/DocExtractor.UI.csproj -c Release -o ./publish
```

No test project exists yet. When creating tests, target `netstandard2.0` and reference the appropriate library project.

## Project Structure and Dependencies

Five projects in dependency order (lowest to highest):

```
DocExtractor.Core      (netstandard2.0)  — interfaces, models, pipeline, splitters
DocExtractor.Parsing   (netstandard2.0)  — Word/Excel parsers; depends on Core
DocExtractor.ML        (netstandard2.0)  — ML.NET models; depends on Core
DocExtractor.Data      (netstandard2.0)  — SQLite + export; depends on Core + ML
DocExtractor.UI        (net48, WinExe)   — WinForms app; depends on all four
```

## Data Flow

```
File → IDocumentParser → RawTable[]
     → Table filter (by index/keyword/all)
     → IColumnNormalizer → column→fieldName map
     → Row iteration → ExtractedRecord[]
     → IRecordSplitter chain (ordered by Priority)
     → ExcelExporter / JSON
```

**Key models:**
- `RawTable` — 2D grid of `TableCell`, preserves merge spans. `GetValue(r,c)` auto-follows shadow cells to their master.
- `ExtractedRecord.Fields` — `Dictionary<FieldName, Value>` after column mapping.
- `ExtractionConfig` — schema (fields + split rules) passed through the whole pipeline.

## Column Normalization (HybridColumnNormalizer)

Three-tier fallback in `HybridMlFirst` mode (default):
1. **Exact match** — compare against `FieldDefinition.FieldName` and `DisplayName` (confidence 1.0)
2. **ML model** — `ColumnClassifierModel.Predict()` if loaded and confidence ≥ 0.6
3. **Variant rules** — check `FieldDefinition.KnownColumnVariants`; exact variant = 0.95, contains = 0.75

The ML model file is `{AppPath}/models/column_classifier.zip`. If the file is absent the model is simply not loaded and the system falls back to rules only.

## ML.NET Constraints (.NET Standard 2.0)

These patterns cause compile errors in `netstandard2.0` — avoid them in library projects:
- `str[..n]` — use `str.Substring(0, n)`
- `list[^1]` — use `list[list.Count - 1]`
- `.ToHashSet()` — use `new HashSet<T>(...)`
- `TextFeaturizingEstimator.CaseNormalizationMode` — does not exist in ML.NET 2.0; omit the `Norm` option
- `LightGbmMulticlassTrainer.Options` — requires `using Microsoft.ML.Trainers.LightGbm;` and `Microsoft.ML.LightGbm` NuGet package (separate from `Microsoft.ML.FastTree`)

## Splitters

All four `IRecordSplitter` implementations live in `DocExtractor.Core/Splitting/`. They are instantiated directly in `ExtractionPipeline` (not registered via DI). To add a new splitter: implement `IRecordSplitter`, add it to the array in `ExtractionPipeline`'s constructor.

`SplitRule.Priority` controls execution order (lower = first). Rules are chained — the output of one splitter feeds the next.

## ML Models

Three models live under `{AppPath}/models/`:

| Model | File | Backend | Min samples |
|-------|------|---------|-------------|
| Column classifier | `column_classifier.zip` | TorchSharp NAS-BERT (`TextClassification`) | 10 |
| NER | `ner_model.zip` | TorchSharp NAS-BERT (`NamedEntityRecognition`) | 20 |
| Section classifier | `section_classifier.zip` | ML.NET FastTree binary classifier | 20 |

**`UnifiedDocModel`** — container that holds all three model instances and exposes `LoadAll(modelsDir)` to silently skip missing files.

**`UnifiedModelTrainer.TrainAll()`** — three-stage sequential trainer (Column → NER → Section) with unified `IProgress<(string stage, string detail, int pct)>` and `CancellationToken`.

**`TrainingParameters`** has three factory presets: `Fast()` (2 epochs, 50 trees), `Standard()` (4 epochs, 200 trees, 5-fold CV), `Fine()` (8 epochs, 500 trees, augmentation). Pass to `UnifiedModelTrainer.TrainAll()`.

## Training Workflow

1. Column annotation CSV (format: `raw column name,canonical field name`) → import via UI → stored in `ColumnTrainingData` SQLite table.
2. NER samples stored in `NerTrainingData`; section heading samples in `SectionTrainingData` (lazy-created via `EnsureSectionTable()` — not in `schema.sql`).
3. Trigger via UI → calls `UnifiedModelTrainer.TrainAll()` → saves all three `.zip` files.
4. `UnifiedDocModel.LoadAll()` hot-reloads all models without restarting the app.

## UI Development

### UI Library — AntdUI (C# Ant Design)

All WinForms UI must be built with **AntdUI** (the C# port of Ant Design for WinForms).
NuGet: `AntdUI` — add to `DocExtractor.UI.csproj`.

Use AntdUI components exclusively. Do not mix with raw WinForms controls where an AntdUI equivalent exists:

| Need | AntdUI component |
|------|-----------------|
| Button | `AntdUI.Button` |
| Input / TextBox | `AntdUI.Input` |
| Table / Grid | `AntdUI.Table` |
| Tree / List | `AntdUI.Tree`, `AntdUI.List` |
| Progress | `AntdUI.Progress` |
| Tabs | `AntdUI.Tabs` |
| Notification / Status | `AntdUI.Message`, `AntdUI.Badge` |
| Toolbar | `AntdUI.ToolStrip` / `AntdUI.Panel` with Button row |
| Menu | `AntdUI.Menu` |
| Select / ComboBox | `AntdUI.Select` |
| Tag / Chip | `AntdUI.Tag` |
| Loading | `AntdUI.Spin` |

### UX Principles

Every UI change requires upfront design thinking — consider layout, information hierarchy, user workflow, and feedback before writing code.

**Layout standard** — all windows follow this top-to-bottom structure:
```
┌─────────────────────────────────┐
│  Menu bar (AntdUI.Menu)         │
├─────────────────────────────────┤
│  Toolbar (icon buttons + labels)│
├─────────────────────────────────┤
│                                 │
│  Main content area              │
│  (Splitter / Tabs / Table)      │
│                                 │
├─────────────────────────────────┤
│  Status bar / log strip         │
└─────────────────────────────────┘
```

**No excessive dialogs** — replace `MessageBox.Show` with:
- `AntdUI.Message.info/success/warn/error` (toast notifications, auto-dismiss) for status feedback
- `AntdUI.Notification` for persistent alerts
- Inline validation messages inside the form for input errors
- Confirmation actions in a collapsible panel or inline popconfirm (`AntdUI.Popconfirm`), not a separate window

Reserve modal dialogs only for destructive, irreversible operations (e.g., delete all training data).

### Form File Structure (3-file convention)

Every WinForms window must use the standard three-file pattern — do not put all code in a single `.cs` file:

```
Forms/
├── MainForm.cs           ← event handlers, business logic wiring
├── MainForm.Designer.cs  ← all InitializeComponent() and control declarations
└── MainForm.resx         ← embedded resources (icons, images, strings)
```

Rules:
- `*.Designer.cs` — contains only `InitializeComponent()`, field declarations, and `Dispose()`. Never add business logic here.
- `*.cs` — constructor calls `InitializeComponent()`, then wires up event handlers and loads initial data.
- `*.resx` — store all icons and localizable strings here; reference via `Properties.Resources.*`.
- Control fields declared in `*.Designer.cs` must be `private` (Visual Studio designer default).

### EPPlus License

All `ExcelPackage` usage must set `ExcelPackage.LicenseContext = LicenseContext.NonCommercial;` before opening any file. This is already done in `ExcelDocumentParser` and `ExcelExporter` constructors — do not remove it.

## Recommendation Engine

`GroupItemRecommender` (in `DocExtractor.ML.Recommendation`) recommends child items for a group name using **character bigram Jaccard similarity** (threshold = 0.25) against all known group names in `GroupKnowledgeRepository`.

`GroupKnowledgeRepository` manages the `GroupItemKnowledge` SQLite table (auto-created via `EnsureTable()` with an `ALTER TABLE` guard for the `SourceFileNormalized` column — this table is **not** in `schema.sql`).

## Built-in Telemetry Config

`MainForm.CreateTelemetryConfig()` contains a hardcoded 15-field schema for satellite telemetry/telecommand tables (序号, APID, 起始字节, 波道名称, 遥测代号, 公式系数 A/B, 量纲, 枚举解译, etc.) with their Chinese column name variants. Use this as the reference when adding or modifying domain-specific field definitions.
