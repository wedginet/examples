using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SQLite;
using Serilog;

namespace YourNamespace
{
    // Assuming Entity defines at least a Guid Id, DateTime? DeletedOn, and DateTime? LastUpdatedOn
    public class Entity
    {
        public Guid Id { get; set; }
        public DateTime? DeletedOn { get; set; }
        public DateTime? LastUpdatedOn { get; set; }
    }

    public interface ISqliteRepository<T>
    {
        Task<T?> GetById(Guid id);
        Task<bool?> DoesEntityExist(Guid id);
        Task<bool?> Add(T entity);
        Task<bool?> Update(T entity);
        Task<int?> Upsert(List<T> entities);
        Task<bool?> SoftDelete(T entity);
        Task<bool?> HardDelete(T entity);
        event EventHandler<EventArgs<T>> DataUpdated;
    }

    public interface ISessionService
    {
        List<string> LastErrors { get; }
    }

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

        protected virtual void OnDataUpdated(T entity) =>
            DataUpdated?.Invoke(this, new EventArgs<T>(entity));

        public async Task<T?> GetById(Guid id)
        {
            try
            {
                // SQLite-net requires that T has a primary key.
                // The Get<T> method returns the entity matching the primary key.
                var result = _db.Get<T>(id);
                await Task.CompletedTask;
                return result;
            }
            catch (Exception ex)
            {
                Log.Information($"{nameof(SqliteRepository<T>)} GetById exception: {ex.Message}");
                string formattedDate = DateTime.UtcNow.ToString("yyyyMMdd HHmm");
                _session.LastErrors.Add($"{formattedDate}:SqliteRepository:GetById:{ex.Message}");
                return null;
            }
        }

        public async Task<bool?> DoesEntityExist(Guid id)
        {
            try
            {
                // Uses SQLite-net Table query to check if an entity exists.
                bool exists = _db.Table<T>().Any(x => x.Id == id);
                await Task.CompletedTask;
                return exists;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"DoesEntityExist failed for {typeof(T)}");
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
                int result = _db.Insert(entity);
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
                Log.Error(ex, $"Update failed for {typeof(T)}");
                string formattedDate = DateTime.UtcNow.ToString("yyyyMMdd HHmm");
                _session.LastErrors.Add($"{formattedDate}:SqliteRepository:Update:{ex.Message}");
                return null;
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }

        public async Task<int?> Upsert(List<T> entities)
        {
            int updatedNum = 0;
            foreach (var entity in entities)
            {
                var exists = await DoesEntityExist(entity.Id);
                if (exists is null) continue;

                bool? result;
                if (exists.Value)
                {
                    // If the entity is flagged as deleted, remove it.
                    if (entity.DeletedOn != null)
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
                    if (entity.DeletedOn != null)
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
                Log.Error(ex, $"SoftDelete failed for {typeof(T)}");
                return null;
            }
        }

        public async Task<bool?> HardDelete(T entity)
        {
            await _writeSemaphore.WaitAsync();
            try
            {
                // Retrieve the entity first (if needed)
                var entityToDelete = await GetById(entity.Id);
                int result = _db.Delete(entityToDelete);
                if (result > 0)
                    OnDataUpdated(entity);
                return result > 0;
            }
            catch (SQLiteException ex)
            {
                Log.Error(ex, $"HardDelete failed for {typeof(T)}");
                return null;
            }
            finally
            {
                _writeSemaphore.Release();
            }
        }
    }
}
