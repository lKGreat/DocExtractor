-- DocExtractor SQLite 数据库结构
-- 存储：训练数据、抽取配置、用户标注

PRAGMA journal_mode=WAL;

-- 列名分类训练数据
CREATE TABLE IF NOT EXISTS ColumnTrainingData (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    ColumnText  TEXT NOT NULL,          -- 原始列名
    FieldName   TEXT NOT NULL,          -- 规范字段名（标签）
    Source      TEXT,                   -- 数据来源（文件名）
    CreatedAt   TEXT DEFAULT (datetime('now')),
    IsVerified  INTEGER DEFAULT 0       -- 是否已人工验证
);

-- NER 实体标注训练数据
CREATE TABLE IF NOT EXISTS NerTrainingData (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    CellText    TEXT NOT NULL,          -- 原始单元格文本
    AnnotationJson TEXT NOT NULL,       -- EntityAnnotation[] 的 JSON
    Source      TEXT,
    CreatedAt   TEXT DEFAULT (datetime('now')),
    IsVerified  INTEGER DEFAULT 0
);

-- 抽取配置（JSON 序列化存储）
CREATE TABLE IF NOT EXISTS ExtractionConfig (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    ConfigName  TEXT NOT NULL UNIQUE,
    ConfigJson  TEXT NOT NULL,          -- ExtractionConfig 的 JSON
    CreatedAt   TEXT DEFAULT (datetime('now')),
    UpdatedAt   TEXT DEFAULT (datetime('now'))
);

-- 应用设置（键值对存储，如默认配置ID）
CREATE TABLE IF NOT EXISTS AppSettings (
    Key         TEXT PRIMARY KEY,
    Value       TEXT
);

-- 抽取任务历史
CREATE TABLE IF NOT EXISTS ExtractionJob (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    JobName     TEXT,
    ConfigId    INTEGER REFERENCES ExtractionConfig(Id),
    SourceFiles TEXT NOT NULL,          -- JSON array of file paths
    StartedAt   TEXT,
    FinishedAt  TEXT,
    Status      TEXT DEFAULT 'pending', -- pending, running, completed, failed
    ResultCount INTEGER DEFAULT 0,
    ErrorMessage TEXT
);

-- 创建索引
CREATE INDEX IF NOT EXISTS idx_column_training_fieldname ON ColumnTrainingData(FieldName);
CREATE INDEX IF NOT EXISTS idx_ner_training_created ON NerTrainingData(CreatedAt);
