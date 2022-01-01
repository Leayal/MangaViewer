using Microsoft.Web.WebView2.Core;
using System;
using Microsoft.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Leayal.MangaViewer.Classes
{
    class WebBrowserTask : IDisposable
    {
        private static readonly JsonSerializerOptions jsonOpts = new JsonSerializerOptions() { IncludeFields = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never };
        private readonly Dictionary<Guid, TaskCompletionSource> taskMap;
        private readonly CoreWebView2 core;
        private bool _disposed;

        public WebBrowserTask(CoreWebView2 core)
        {
            this._disposed = false;
            this.core = core;
            this.taskMap = new Dictionary<Guid, TaskCompletionSource>();
            this.core.WebMessageReceived += this.Core_WebMessageReceived;
        }

        public void Dispose()
        {
            if (this._disposed) return;
            this._disposed = true;
            this.core.WebMessageReceived -= this.Core_WebMessageReceived;
            var guids = this.taskMap.Keys.ToArray();
            foreach (var guid in guids)
            {
                if (this.taskMap.Remove(guid, out var t))
                {
                    t.SetResult();
                }
            }
        }

        private void Core_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var jsonString = e.WebMessageAsJson;
            if (!string.IsNullOrEmpty(jsonString))
            {
                var obj = JsonSerializer.Deserialize<JsonObj>(jsonString, jsonOpts);
                if (obj is not null && this.taskMap.Remove(Guid.Parse(obj.guid), out var t))
                {
                    t.SetResult();
                }
            }
        }

        public async Task SetState(string statename)
        {
            await this.Run("setState", statename);
        }

        public async Task LoadManga()
        {
            await this.Run("loadManga", Array.Empty<object>());
        }

        class JsonObj
        {
            public string guid;
            public string cmd;
            public object[] args;
        }

        public async Task Run(string functionName, params object[] args)
        {
            var t = new TaskCompletionSource();
            var guid = Guid.NewGuid();

            this.taskMap.Add(guid, t);

            var obj = new JsonObj();
            obj.guid = guid.ToString();
            obj.cmd = functionName;
            obj.args = args ?? Array.Empty<object>();
            try
            {
                this.core.PostWebMessageAsJson(JsonSerializer.Serialize<JsonObj>(obj, jsonOpts));
            }
            catch (InvalidOperationException)
            {
                this.taskMap.Remove(guid);
                t.SetResult();
            }
        }
    }
}
