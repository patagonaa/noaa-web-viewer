﻿using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using WebDav;

namespace FileProviders.WebDav
{
    public class WebDavFileProvider : IFileProvider
    {
        private readonly WebDavClient _client;

        public WebDavFileProvider(IOptions<WebDavConfiguration> options)
        {
            var config = options.Value;
            var clientParams = new WebDavClientParams
            {
                BaseAddress = new Uri(config.BaseUri),
                Credentials = new NetworkCredential(config.User, config.Password)
            };
            _client = new WebDavClient(clientParams);
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            subpath = subpath.TrimStart('/');

            var parameters = new PropfindParameters
            {
                Headers = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("depth", "1")
                }
            };
            var result = _client.Propfind(subpath, parameters).Result;
            if (result.StatusCode == 404)
            {
                return NotFoundDirectoryContents.Singleton;
            }

            if (!result.IsSuccessful)
            {
                throw new WebException("WebDav error " + result.StatusCode + " while listing directory");
            }

            return new WebDavDirectoryContents(_client, result.Resources.Skip(1).ToList()); // Skip directory itself
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            subpath = subpath.TrimStart('/');

            var parameters = new PropfindParameters
            {
                Headers = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("depth", "0")
                }
            };

            var result = _client.Propfind(subpath, parameters).Result;
            if (result.StatusCode == 404)
            {
                return new NotFoundFileInfo(subpath);
            }

            if (!result.IsSuccessful)
            {
                throw new WebException("WebDav error " + result.StatusCode + " while listing directory");
            }

            return new WebDavFileInfo(_client, result.Resources.Single());
        }

        public bool DeleteFile(string subpath)
        {
            subpath = subpath.TrimStart('/');

            var result = _client.Delete(subpath).Result;
            if (result.StatusCode == 404)
            {
                return false;
            }

            if (!result.IsSuccessful)
            {
                throw new WebException("WebDav error " + result.StatusCode + " while deleting file");
            }

            return true;
        }

        public IChangeToken Watch(string filter)
        {
            throw new NotImplementedException();
        }
    }
}
