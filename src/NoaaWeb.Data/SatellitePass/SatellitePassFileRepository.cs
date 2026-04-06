using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NoaaWeb.Data.SatellitePass
{
    public class SatellitePassFileRepository : ISatellitePassRepository
    {
        private readonly FileDbConfiguration _dbConfig;
        private readonly ILogger<SatellitePassFileRepository> _logger;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly string _thumbsDir;
        private IList<SatellitePass>? _cache;
        private DateTime _cacheTime;

        public SatellitePassFileRepository(ILogger<SatellitePassFileRepository> logger, IOptions<FileDbConfiguration> dbConfig)
        {
            _dbConfig = dbConfig.Value;
            _logger = logger;
            _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.General)
            {
                WriteIndented = true
            };

            _thumbsDir = Path.Join(dbConfig.Value.DbDirectory, "thumbs");
            Directory.CreateDirectory(_thumbsDir);
        }

        public IQueryable<SatellitePass> Get()
        {
            var file = new FileInfo(GetDbFileName());

            if (!file.Exists)
            {
                return Enumerable.Empty<SatellitePass>().AsQueryable();
            }

            if (_cache != null && file.LastWriteTimeUtc == _cacheTime)
            {
                return _cache.AsQueryable();
            }

            using var dbfile = OpenDb(FileAccess.Read, FileShare.Read);
            var sw = Stopwatch.StartNew();
            var db = (dbfile.Length == 0 ? null : JsonSerializer.Deserialize<IList<SatellitePass>>(dbfile, _serializerOptions)) ?? [];
            _logger.LogInformation("DB deserialize took {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);

            sw.Restart();
            Parallel.ForEach(db, x =>
            {
                var path = GetThumbnailFileName(x.Site, x.FileKey);
                if (File.Exists(path))
                    x.ThumbnailUri = File.ReadAllText(path);
            });
            _logger.LogInformation("Image loading took {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);

            _cache = db;
            _cacheTime = file.LastWriteTimeUtc;
            return db.AsQueryable();
        }

        public void Insert(SatellitePass pass)
        {
            using var dbfile = OpenDb(FileAccess.ReadWrite, FileShare.None);
            var sw = Stopwatch.StartNew();

            var db = (dbfile.Length == 0 ? null : JsonSerializer.Deserialize<IList<SatellitePass>>(dbfile, _serializerOptions)) ?? [];

            db.Add(pass);

            dbfile.Position = 0;
            dbfile.SetLength(0);

            JsonSerializer.Serialize(dbfile, db, _serializerOptions);
            _logger.LogInformation("DB insert took {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);

            if (pass.ThumbnailUri != null)
            {
                sw.Restart();
                File.WriteAllText(GetThumbnailFileName(pass.Site, pass.FileKey), pass.ThumbnailUri);
                _logger.LogInformation("Image save took {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
            }
        }

        private string GetDbFileName()
        {
            return Path.Join(_dbConfig.DbDirectory, "passes.json");
        }

        private string GetThumbnailFileName(string site, string fileKey)
        {
            return Path.Join(_thumbsDir, $"{site}_{fileKey}.txt");
        }

        private FileStream OpenDb(FileAccess access, FileShare share)
        {
            while (true)
            {
                try
                {
                    return File.Open(GetDbFileName(), FileMode.OpenOrCreate, access, share);
                }
                catch (IOException ioex)
                {
                    if (ioex.HResult != 32 && ioex.HResult != 33)
                    {
                        _logger.LogWarning(ioex, "Unhandled IOException with HResult {HResult}. Retrying.", ioex.HResult);
                    }
                    Thread.Sleep(100);
                }
            }
        }
    }
}
