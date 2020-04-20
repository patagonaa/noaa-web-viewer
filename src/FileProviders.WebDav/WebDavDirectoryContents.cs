using Microsoft.Extensions.FileProviders;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using WebDav;

namespace FileProviders.WebDav
{
    class WebDavDirectoryContents : IDirectoryContents
    {
        private readonly WebDavClient _client;
        private readonly IReadOnlyCollection<WebDavResource> _resources;

        public bool Exists => true;

        public WebDavDirectoryContents(WebDavClient client, IReadOnlyCollection<WebDavResource> resources)
        {
            _client = client;
            _resources = resources;
        }

        public IEnumerator<IFileInfo> GetEnumerator()
        {
            return _resources.Select(x => new WebDavFileInfo(_client, x)).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
