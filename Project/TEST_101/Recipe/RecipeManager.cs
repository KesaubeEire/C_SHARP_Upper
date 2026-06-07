using System.Text.Json;
using TEST_101.Core;
using TEST_101.Storage;
using TEST_101.Storage.Models;
using TEST_101.Storage.Repositories;

namespace TEST_101.Recipe
{
    /// <summary>
    /// 配方管理器
    ///
    /// 面试考点：
    /// 1. 配方是什么？一组工艺参数的集合
    /// 2. 如何下发配方？通过 Modbus 写寄存器
    /// 3. 配方版本管理？每次修改版本号+1
    /// </summary>
    public class RecipeManager : IDisposable
    {
        private readonly RecipeRepository _repository;
        private bool _disposed;

        // 事件：配方已下发
        public event Action<string, int>? OnRecipeDownloaded;

        public RecipeManager(DatabaseManager db)
        {
            _repository = new RecipeRepository(db);
        }

        /// <summary>
        /// 获取所有配方
        /// </summary>
        public List<RecipeRecord> GetAllRecipes()
        {
            return _repository.GetAll();
        }

        /// <summary>
        /// 获取配方参数
        /// </summary>
        public List<RecipeParameter>? GetRecipeParameters(string recipeName)
        {
            var recipe = _repository.GetByName(recipeName);
            if (recipe == null) return null;

            return JsonSerializer.Deserialize<List<RecipeParameter>>(recipe.ParametersJson);
        }

        /// <summary>
        /// 保存配方
        /// </summary>
        public void SaveRecipe(string name, string? description, List<RecipeParameter> parameters)
        {
            var recipe = new RecipeRecord
            {
                Name = name,
                Description = description,
                ParametersJson = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
                {
                    WriteIndented = true
                })
            };

            _repository.Save(recipe);
        }

        /// <summary>
        /// 更新配方
        /// </summary>
        public void UpdateRecipe(long id, string name, string? description, List<RecipeParameter> parameters)
        {
            var recipe = new RecipeRecord
            {
                Id = id,
                Name = name,
                Description = description,
                ParametersJson = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
                {
                    WriteIndented = true
                })
            };

            _repository.Save(recipe);
        }

        /// <summary>
        /// 删除配方
        /// </summary>
        public void DeleteRecipe(long id)
        {
            _repository.Delete(id);
        }

        /// <summary>
        /// 复制配方
        /// </summary>
        public RecipeRecord CopyRecipe(long sourceId, string newName)
        {
            return _repository.Copy(sourceId, newName);
        }

        /// <summary>
        /// 下发配方到 PLC
        /// </summary>
        public async Task DownloadRecipeAsync(string recipeName, Func<ushort, ushort, Task> writeRegister)
        {
            var parameters = GetRecipeParameters(recipeName);
            if (parameters == null)
                throw new Exception($"配方 {recipeName} 不存在");

            foreach (var param in parameters)
            {
                // 将实际值转换为原始值
                var rawValue = (ushort)((param.Value - param.Offset) / param.Scale);
                await writeRegister(param.Address, rawValue);
            }

            OnRecipeDownloaded?.Invoke(recipeName, parameters.Count);
        }

        /// <summary>
        /// 从 PLC 读取当前值作为配方
        /// </summary>
        public async Task<List<RecipeParameter>> ReadRecipeFromPlcAsync(
            List<RecipeParameter> template, Func<ushort, Task<ushort>> readRegister)
        {
            var result = new List<RecipeParameter>();

            foreach (var param in template)
            {
                var rawValue = await readRegister(param.Address);
                result.Add(new RecipeParameter
                {
                    Name = param.Name,
                    Address = param.Address,
                    Value = rawValue * param.Scale + param.Offset,
                    Scale = param.Scale,
                    Offset = param.Offset,
                    Unit = param.Unit
                });
            }

            return result;
        }

        /// <summary>
        /// 导出配方为 JSON 文件
        /// </summary>
        public void ExportToFile(string recipeName, string filePath)
        {
            var parameters = GetRecipeParameters(recipeName);
            if (parameters == null)
                throw new Exception($"配方 {recipeName} 不存在");

            var json = JsonSerializer.Serialize(parameters, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// 从 JSON 文件导入配方
        /// </summary>
        public void ImportFromFile(string filePath, string recipeName)
        {
            var json = File.ReadAllText(filePath);
            var parameters = JsonSerializer.Deserialize<List<RecipeParameter>>(json);
            if (parameters == null)
                throw new Exception("无效的配方文件");

            SaveRecipe(recipeName, $"导入自 {Path.GetFileName(filePath)}", parameters);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }

    /// <summary>
    /// 配方参数
    /// </summary>
    public class RecipeParameter
    {
        /// <summary>参数名称</summary>
        public string Name { get; set; } = "";

        /// <summary>PLC 地址</summary>
        public ushort Address { get; set; }

        /// <summary>实际值</summary>
        public double Value { get; set; }

        /// <summary>缩放系数</summary>
        public double Scale { get; set; } = 1.0;

        /// <summary>偏移量</summary>
        public double Offset { get; set; } = 0.0;

        /// <summary>单位</summary>
        public string Unit { get; set; } = "";
    }
}
