using System.Data;
using System.Text.Json;
using TEST_101.Storage.Models;

namespace TEST_101.Storage.Repositories
{
    /// <summary>
    /// 配方仓储
    /// </summary>
    public class RecipeRepository
    {
        private readonly DatabaseManager _db;

        public RecipeRepository(DatabaseManager db)
        {
            _db = db;
        }

        /// <summary>
        /// 保存配方（新增或更新）
        /// </summary>
        public void Save(RecipeRecord recipe)
        {
            if (recipe.Id == 0)
            {
                // 新增
                const string sql = @"
                    INSERT INTO recipes (name, description, parameters_json, version, created_at)
                    VALUES (@name, @description, @parameters_json, @version, @created_at)";

                _db.ExecuteNonQuery(sql, new Dictionary<string, object>
                {
                    ["@name"] = recipe.Name,
                    ["@description"] = recipe.Description ?? "",
                    ["@parameters_json"] = recipe.ParametersJson,
                    ["@version"] = recipe.Version,
                    ["@created_at"] = DateTime.Now.ToString("o")
                });
            }
            else
            {
                // 更新
                const string sql = @"
                    UPDATE recipes
                    SET name = @name, description = @description, parameters_json = @parameters_json,
                        version = version + 1, updated_at = @updated_at
                    WHERE id = @id";

                _db.ExecuteNonQuery(sql, new Dictionary<string, object>
                {
                    ["@id"] = recipe.Id,
                    ["@name"] = recipe.Name,
                    ["@description"] = recipe.Description ?? "",
                    ["@parameters_json"] = recipe.ParametersJson,
                    ["@updated_at"] = DateTime.Now.ToString("o")
                });
            }
        }

        /// <summary>
        /// 获取所有配方
        /// </summary>
        public List<RecipeRecord> GetAll()
        {
            var dt = _db.ExecuteQuery("SELECT * FROM recipes ORDER BY updated_at DESC, created_at DESC");
            return DataTableToList(dt);
        }

        /// <summary>
        /// 按名称获取配方
        /// </summary>
        public RecipeRecord? GetByName(string name)
        {
            var dt = _db.ExecuteQuery(
                "SELECT * FROM recipes WHERE name = @name",
                new Dictionary<string, object> { ["@name"] = name });

            var list = DataTableToList(dt);
            return list.Count > 0 ? list[0] : null;
        }

        /// <summary>
        /// 删除配方
        /// </summary>
        public void Delete(long id)
        {
            _db.ExecuteNonQuery("DELETE FROM recipes WHERE id = @id",
                new Dictionary<string, object> { ["@id"] = id });
        }

        /// <summary>
        /// 复制配方
        /// </summary>
        public RecipeRecord Copy(long sourceId, string newName)
        {
            var source = GetById(sourceId);
            if (source == null) throw new Exception("源配方不存在");

            var copy = new RecipeRecord
            {
                Name = newName,
                Description = $"复制自 {source.Name}",
                ParametersJson = source.ParametersJson,
                Version = 1
            };
            Save(copy);
            return copy;
        }

        private RecipeRecord? GetById(long id)
        {
            var dt = _db.ExecuteQuery(
                "SELECT * FROM recipes WHERE id = @id",
                new Dictionary<string, object> { ["@id"] = id });

            var list = DataTableToList(dt);
            return list.Count > 0 ? list[0] : null;
        }

        private static List<RecipeRecord> DataTableToList(DataTable dt)
        {
            var list = new List<RecipeRecord>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new RecipeRecord
                {
                    Id = (long)row["id"],
                    Name = (string)row["name"],
                    Description = row["description"] != DBNull.Value ? (string)row["description"] : null,
                    ParametersJson = (string)row["parameters_json"],
                    Version = (int)(long)row["version"],
                    CreatedAt = DateTime.Parse((string)row["created_at"]),
                    UpdatedAt = row["updated_at"] != DBNull.Value ? DateTime.Parse((string)row["updated_at"]) : null
                });
            }
            return list;
        }
    }
}
