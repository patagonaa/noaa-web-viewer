using Microsoft.Extensions.FileProviders;
using System;
using System.IO;
using System.Net;
using WebDav;

namespace FileProviders.WebDav
{
    class WebDavFileInfo : IFileInfo
    {
        private readonly WebDavClient _client;
        private readonly WebDavResource _resource;

        public WebDavFileInfo(WebDavClient client, WebDavResource resource)
        {
            _client = client;
            _resource = resource;
        }

        public bool Exists => true;

        public long Length => _resource.ContentLength ?? -1;

        public string PhysicalPath => null;

        public string Name => Path.GetFileName(_resource.Uri);

        public DateTimeOffset LastModified => _resource.LastModifiedDate ?? DateTimeOffset.MinValue;

        public bool IsDirectory => _resource.IsCollection;

        public Stream CreateReadStream()
        {
            var result = _client.GetProcessedFile(_resource.Uri).Result;
            if (!result.IsSuccessful)
            {
                throw new WebException("WebDav error " + result.StatusCode + " while getting file");
            }
            return result.Stream;
        }
    }
}
