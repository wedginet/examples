using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dapper.Contrib.Extensions;
using Serilog;
using SQLite;

namespace YourNamespace
{
    public class SqliteRepository<T> : ISqliteRepository<T> where T : Entity, new()
    {
        protected readonly SQLiteConnection _db;
        protected readonly SemaphoreSlim _writeSemaphore = new(1, 1);
        protected readonly ISessionService _session;

        public SqliteRepository(SQLiteConnection db, ISessionService session)
        {
            _db = db;
            _session = session;
        }

        public event EventHandler<EventArgs<T>>? DataUpdated;

        protected virtual void OnDataUpdated(T entity)
        {
            DataUpdated?.Invoke(this, new EventArgs<T>(entity));
        }

        public async Task<T?> GetById(Guid id)
        {
            try
            {
                T result = _db.Get<T>(id);
                await Task.CompletedTask;
                return result;
            }
            catch (Exception ex)
            {
                Log.Information($"{nameof(SqliteRepository<T>)} exception - Message: {ex.Message}");
                string formattedDate = DateTime.UtcNow.ToString("yyyyMMdd HHmm");
                _session.LastErrors.Add($"{formattedDate}:SqliteRepository:GetById:{ex.Message}");
                return null;
            }
        }

        public async Task<bool?> DoesEntityExist(Guid id)
        {
            try
            {
                string sql = $"SELECT EXISTS(SELECT 1 FROM {TableName()} WHERE Id = ?)";
                bool result = _db.ExecuteScalar<bool>(sql, id);
                await Task.CompletedTask;
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"DoesEntityExist failed: {typeof(T)}");
                string formattedDate = DateTime.UtcNow.ToString("yyyyMMdd HHmm");
                _session.LastErrors.Add($"{formattedDate}:SqliteRepository:DoesEntityExist:{ex.Message}");
                return null;
            }
        }

        public async Task<bool?> Add(T entity)
        {
            await _writeSemaphore.WaitAsync();
            try
            {
                long result = _db.Insert(entity);
                if (result > 0)
                    OnDataUpdated(entity);
                return result > 0;
            }
            catch (SQLiteException ex)
            {
                Log.Error(ex, $"{nameof(SqliteRepository<T>)} Add Error");
                string formattedDate = DateTime.UtcNow.ToString("yyyyMMdd HHmm");
                _session.LastErrors.Add($"{formattedDate}:SqliteRepository:Add:{ex.Message}");
                return false;
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        public async Task<bool?> Update(T entity)
        {
            await _writeSemaphore.WaitAsync();
            try
            {
                int result = _db.Update(entity);
                if (result > 0)
                    OnDataUpdated(entity);
                return result > 0;
            }
            catch (SQLiteException ex)
            {
                Log.Error(ex, $"Update failed: {typeof(T)}");
                string formattedDate = DateTime.UtcNow.ToString("yyyyMMdd HHmm");
                _session.LastErrors.Add($"{formattedDate}:SqliteRepository:Update:{ex.Message}");
                return null;
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        public async Task<int?> Upsert(System.Collections.Generic.List<T> entities)
        {
            int updatedNum = 0;
            foreach (var entity in entities)
            {
                var exists = await DoesEntityExist(entity.Id);
                if (exists is null)
                    continue;

                bool? result;
                if (exists.Value)
                {
                    if (entity.DeletedOn is not null)
                    {
                        await HardDelete(entity);
                        result = true;
                    }
                    else
                    {
                        result = await Update(entity);
                    }
                }
                else
                {
                    if (entity.DeletedOn is not null)
                        continue;
                    result = await Add(entity);
                }
                if (result is not null && result.Value)
                    updatedNum++;
            }
            return updatedNum;
        }

        public async Task<bool?> SoftDelete(T entity)
        {
            try
            {
                var now = DateTime.UtcNow;
                entity.DeletedOn = now;
                entity.LastUpdatedOn = now;
                var result = await Update(entity);
                if (result ?? false)
                    OnDataUpdated(entity);
                return result;
            }
            catch (SQLiteException ex)
            {
                Log.Error(ex, $"SoftDelete failed: {typeof(T)}");
                return null;
            }
        }

        public async Task<bool?> HardDelete(T entity)
        {
            await _writeSemaphore.WaitAsync();
            try
            {
                var entityToDelete = await GetById(entity.Id);
                int result = _db.Delete<T>(entityToDelete?.Id);
                if (result > 0)
                    OnDataUpdated(entity);
                return result > 0;
            }
            catch (SQLiteException ex)
            {
                Log.Error(ex, $"HardDelete failed: {typeof(T)}");
                return null;
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        /// <summary>
        /// Returns the table name for this entity using the [Table] attribute.
        /// </summary>
        protected static string TableName()
        {
            // Get the Dapper.Contrib [Table] attribute on type T (if any)
            var tableAttr = typeof(T).GetCustomAttribute<TableAttribute>(inherit: false);
            return tableAttr != null && !string.IsNullOrWhiteSpace(tableAttr.Name) 
                ? tableAttr.Name 
                : typeof(T).Name;
        }
    }
}
