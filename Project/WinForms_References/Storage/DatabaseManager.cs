using System.Data;
using Microsoft.Data.Sqlite;
using System.Data.SqlClient;

namespace TEST_101.Storage
{
    /// <summary>
    /// 数据库管理器 —— 支持 SQLite 和 SQL Server
    ///
    /// 面试考点：
    /// 1. 为什么支持两种数据库？开发用 SQLite，生产用 SQL Server
    /// 2. 如何切换？配置文件或编译时常量
    /// 3. 参数化查询防 SQL 注入
    /// </summary>
    public class DatabaseManager : IDisposable
    {
        // 数据库类型枚举
        public enum DatabaseType { SQLite, SqlServer }

        private readonly DatabaseType _dbType;
        private readonly string _connectionString;
        private IDbConnection? _connection;
        private bool _disposed;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="dbType">数据库类型</param>
        /// <param name="connectionString">连接字符串</param>
        public DatabaseManager(DatabaseType dbType = DatabaseType.SQLite,
            string connectionString = "Data Source=monitor.db")
        {
            _dbType = dbType;
            _connectionString = connectionString;
            InitializeDatabase();
        }

        /// <summary>
        /// 创建 SQL Server 实例（工厂方法）
        /// </summary>
        public static DatabaseManager CreateSqlServer(string server, string database,
            string user = "sa", string password = "YourPassword123")
        {
            var connStr = $"Server={server};Database={database};User Id={user};Password={password};TrustServerCertificate=true;";
            return new DatabaseManager(DatabaseType.SqlServer, connStr);
        }

        /// <summary>
        /// 创建 SQLite 实例（工厂方法）
        /// </summary>
        public static DatabaseManager CreateSQLite(string dbPath = "monitor.db")
        {
            return new DatabaseManager(DatabaseType.SQLite, $"Data Source={dbPath}");
        }

        /// <summary>
        /// 初始化数据库连接和表结构
        /// </summary>
        private void InitializeDatabase()
        {
            _connection = _dbType switch
            {
                DatabaseType.SQLite => new SqliteConnection(_connectionString),
                DatabaseType.SqlServer => new SqlConnection(_connectionString),
                _ => throw new NotSupportedException($"不支持的数据库类型: {_dbType}")
            };

            _connection.Open();

            // SQLite 特有：启用 WAL 模式
            if (_dbType == DatabaseType.SQLite)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "PRAGMA journal_mode=WAL;";
                cmd.ExecuteNonQuery();
            }

            // 创建表结构
            CreateTables();
        }

        /// <summary>
        /// 创建所有必要的表
        /// </summary>
        private void CreateTables()
        {
            // 根据数据库类型选择 SQL 语法
            var sql = _dbType == DatabaseType.SQLite ? GetSQLiteCreateTableSql() : GetSqlServerCreateTableSql();

            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// SQLite 建表语句
        /// </summary>
        private string GetSQLiteCreateTableSql() => @"
            -- 生产数据表
            CREATE TABLE IF NOT EXISTS production_data (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                device_id TEXT NOT NULL,
                address INTEGER NOT NULL,
                raw_value INTEGER NOT NULL,
                actual_value REAL,
                unit TEXT,
                name TEXT
            );

            -- 报警历史表
            CREATE TABLE IF NOT EXISTS alarm_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                rule_name TEXT NOT NULL,
                device_id TEXT NOT NULL,
                address INTEGER,
                current_value REAL,
                threshold REAL,
                level TEXT NOT NULL,
                status TEXT DEFAULT '未确认',
                confirmed_at TEXT,
                confirmed_by TEXT
            );

            -- 配方表
            CREATE TABLE IF NOT EXISTS recipes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                description TEXT,
                parameters_json TEXT NOT NULL,
                version INTEGER DEFAULT 1,
                created_at TEXT NOT NULL,
                updated_at TEXT
            );

            -- 操作日志表
            CREATE TABLE IF NOT EXISTS operation_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                operator TEXT,
                action TEXT NOT NULL,
                details TEXT
            );

            -- 创建索引
            CREATE INDEX IF NOT EXISTS idx_production_timestamp ON production_data(timestamp);
            CREATE INDEX IF NOT EXISTS idx_production_device ON production_data(device_id);
            CREATE INDEX IF NOT EXISTS idx_alarm_timestamp ON alarm_history(timestamp);
            CREATE INDEX IF NOT EXISTS idx_alarm_status ON alarm_history(status);
        ";

        /// <summary>
        /// SQL Server 建表语句
        /// </summary>
        private string GetSqlServerCreateTableSql() => @"
            -- 生产数据表
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'production_data')
            CREATE TABLE production_data (
                id BIGINT IDENTITY(1,1) PRIMARY KEY,
                timestamp DATETIME2 NOT NULL,
                device_id NVARCHAR(50) NOT NULL,
                address INT NOT NULL,
                raw_value INT NOT NULL,
                actual_value FLOAT,
                unit NVARCHAR(20),
                name NVARCHAR(100)
            );

            -- 报警历史表
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'alarm_history')
            CREATE TABLE alarm_history (
                id BIGINT IDENTITY(1,1) PRIMARY KEY,
                timestamp DATETIME2 NOT NULL,
                rule_name NVARCHAR(100) NOT NULL,
                device_id NVARCHAR(50) NOT NULL,
                address INT,
                current_value FLOAT,
                threshold FLOAT,
                level NVARCHAR(20) NOT NULL,
                status NVARCHAR(20) DEFAULT '未确认',
                confirmed_at DATETIME2,
                confirmed_by NVARCHAR(50)
            );

            -- 配方表
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'recipes')
            CREATE TABLE recipes (
                id BIGINT IDENTITY(1,1) PRIMARY KEY,
                name NVARCHAR(100) NOT NULL UNIQUE,
                description NVARCHAR(500),
                parameters_json NVARCHAR(MAX) NOT NULL,
                version INT DEFAULT 1,
                created_at DATETIME2 NOT NULL,
                updated_at DATETIME2
            );

            -- 操作日志表
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'operation_logs')
            CREATE TABLE operation_logs (
                id BIGINT IDENTITY(1,1) PRIMARY KEY,
                timestamp DATETIME2 NOT NULL,
                operator NVARCHAR(50),
                action NVARCHAR(100) NOT NULL,
                details NVARCHAR(MAX)
            );

            -- 创建索引
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_production_timestamp')
                CREATE INDEX idx_production_timestamp ON production_data(timestamp);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_production_device')
                CREATE INDEX idx_production_device ON production_data(device_id);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_alarm_timestamp')
                CREATE INDEX idx_alarm_timestamp ON alarm_history(timestamp);
            IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_alarm_status')
                CREATE INDEX idx_alarm_status ON alarm_history(status);
        ";

        /// <summary>
        /// 获取连接
        /// </summary>
        public IDbConnection GetConnection()
        {
            return _connection ?? throw new ObjectDisposedException(nameof(DatabaseManager));
        }

        /// <summary>
        /// 执行非查询 SQL
        /// </summary>
        public int ExecuteNonQuery(string sql, Dictionary<string, object>? parameters = null)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = sql;
            if (parameters != null)
            {
                foreach (var (key, value) in parameters)
                {
                    var param = cmd.CreateParameter();
                    param.ParameterName = key;
                    param.Value = value ?? DBNull.Value;
                    cmd.Parameters.Add(param);
                }
            }
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// 执行查询，返回 DataTable
        /// </summary>
        public DataTable ExecuteQuery(string sql, Dictionary<string, object>? parameters = null)
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = sql;
            if (parameters != null)
            {
                foreach (var (key, value) in parameters)
                {
                    var param = cmd.CreateParameter();
                    param.ParameterName = key;
                    param.Value = value ?? DBNull.Value;
                    cmd.Parameters.Add(param);
                }
            }

            var dt = new DataTable();
            using var reader = cmd.ExecuteReader();
            dt.Load(reader);
            return dt;
        }

        /// <summary>
        /// 批量插入（高性能）
        /// </summary>
        public void BulkInsert(string tableName, DataTable data)
        {
            if (data.Rows.Count == 0) return;

            if (_dbType == DatabaseType.SqlServer)
            {
                // SQL Server 使用 SqlBulkCopy 高性能插入
                using var bulkCopy = new SqlBulkCopy((SqlConnection)_connection!);
                bulkCopy.DestinationTableName = tableName;
                foreach (DataColumn col in data.Columns)
                {
                    bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                }
                bulkCopy.WriteToServer(data);
            }
            else
            {
                // SQLite 使用事务批量插入
                using var transaction = ((SqliteConnection)_connection!).BeginTransaction();
                try
                {
                    var columns = string.Join(", ", data.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
                    var placeholders = string.Join(", ", data.Columns.Cast<DataColumn>().Select(c => $"@{c.ColumnName}"));
                    var sql = $"INSERT INTO {tableName} ({columns}) VALUES ({placeholders})";

                    using var cmd = _connection!.CreateCommand();
                    cmd.CommandText = sql;
                    cmd.Transaction = transaction;

                    foreach (DataColumn col in data.Columns)
                    {
                        var param = cmd.CreateParameter();
                        param.ParameterName = $"@{col.ColumnName}";
                        cmd.Parameters.Add(param);
                    }

                    foreach (DataRow row in data.Rows)
                    {
                        foreach (DataColumn col in data.Columns)
                        {
                            ((IDbDataParameter)cmd.Parameters[$"@{col.ColumnName}"]!).Value = row[col];
                        }
                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _connection?.Close();
            _connection?.Dispose();
        }
    }
}
