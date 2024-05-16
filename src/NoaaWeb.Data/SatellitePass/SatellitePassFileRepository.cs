using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace NoaaWeb.Data.SatellitePass
{
    public class SatellitePassFileRepository : ISatellitePassRepository
    {
        private readonly FileDbConfiguration _dbConfig;
        private readonly ILogger<SatellitePassFileRepository> _logger;

        private IList<SatellitePass> _cache;
        private DateTime _cacheTime;

        public SatellitePassFileRepository(ILogger<SatellitePassFileRepository> logger, IOptions<FileDbConfiguration> dbConfig)
        {
            _dbConfig = dbConfig.Value;
            _logger = logger;
        }

        public IQueryable<SatellitePass> Get()
        {
            var file = new FileInfo(GetFileName());

            if (!file.Exists)
            {
                return Enumerable.Empty<SatellitePass>().AsQueryable();
            }

            if (_cache != null && file.LastWriteTimeUtc == _cacheTime)
            {
                return _cache.AsQueryable();
            }

            using (var dbsr = new StreamReader(OpenDb(FileAccess.Read, FileShare.Read), Encoding.UTF8))
            {
                var dbStr = dbsr.ReadToEnd();
                var sw = Stopwatch.StartNew();
                var db = dbStr.Length == 0 ? new List<SatellitePass>() : JsonConvert.DeserializeObject<IList<SatellitePass>>(dbStr);
                _logger.LogInformation("DB deserialize took {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
                _cache = db;
                _cacheTime = file.LastWriteTimeUtc;
                return db.AsQueryable();
            }
        }

        public void Insert(SatellitePass pass)
        {
            using (var dbfile = OpenDb(FileAccess.ReadWrite, FileShare.None))
            {
                var sw = Stopwatch.StartNew();
                IList<SatellitePass> db;
                using (var dbsr = new StreamReader(dbfile, Encoding.UTF8, false, 1024, true))
                {
                    var dbStr = dbsr.ReadToEnd();
                    db = dbStr.Length == 0 ? new List<SatellitePass>() : JsonConvert.DeserializeObject<IList<SatellitePass>>(dbStr);
                }

                db.Add(pass);

                dbfile.Position = 0;
                dbfile.SetLength(0);

                using (var sbsw = new StreamWriter(dbfile, Encoding.UTF8, 1024, true))
                {
                    sbsw.Write(JsonConvert.SerializeObject(db, Formatting.Indented));
                }

                _logger.LogInformation("DB insert took {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
            }
        }

        private string GetFileName()
        {
            return Path.Combine(_dbConfig.DbDirectory, "passes.json");
        }

        private FileStream OpenDb(FileAccess access, FileShare share)
        {
            while (true)
            {
                try
                {
                    return File.Open(GetFileName(), FileMode.OpenOrCreate, access, share);
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
