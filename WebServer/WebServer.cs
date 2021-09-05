using System;
using System.Net;
using OpenMod.Core.Plugins;
using OpenMod.API.Plugins;
using System.Threading.Tasks;
using System.IO;
using OpenMod.Core.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;

[assembly: PluginMetadata("Feli.WebServer", 
    DisplayName = "WebServer",
    Author = "Feli",
    Website = "discord.fplugins.com",
    Description = "A plugin that provides a web server to host files"
)]

namespace WebServer
{
    public class WebServer : OpenModUniversalPlugin
    {
        private readonly ILogger<WebServer> logger;
        private readonly IConfiguration configuration;

        private HttpListener httpListener;

        private string directory;

        public WebServer(
            ILogger<WebServer> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider) : base(serviceProvider)
        {
            this.logger = logger;
            this.configuration = configuration;
            httpListener = new HttpListener();
            directory = Path.Combine(WorkingDirectory, "WebServerFiles");
        }

        protected override async Task OnLoadAsync()
        {
            File.Delete(Path.Combine(WorkingDirectory, "WebServerFiles.404.html"));
            File.Delete(Path.Combine(WorkingDirectory, "WebServerFiles.index.html"));
            File.Delete(Path.Combine(WorkingDirectory, "WebServerFiles.1011.png"));

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);


            var index = GetType().Assembly.GetManifestResourceStream("WebServer.WebServerFiles.index.html");
            var notFound = GetType().Assembly.GetManifestResourceStream("WebServer.WebServerFiles.404.html");
            var testImage = GetType().Assembly.GetManifestResourceStream("WebServer.WebServerFiles.1011.png");

            if(configuration.GetSection("exampleFiles").Get<bool>())
            {
                File.WriteAllBytes(Path.Combine(directory, "index.html"), await index.ReadBytesFromStreamAsync());
                File.WriteAllBytes(Path.Combine(directory, "404.html"), await notFound.ReadBytesFromStreamAsync());
                File.WriteAllBytes(Path.Combine(directory, "1011.png"), await testImage.ReadBytesFromStreamAsync());
            }
            

            AsyncHelper.Schedule("WebServer", () => RunWebServer());
        }

        private Task RunWebServer()
        {
            logger.LogInformation("Starting Web Server");

            httpListener.Prefixes.Add(configuration["webServer:serverAddress"]);
            httpListener.Start();

            logger.LogInformation("Web Server started");

            while (IsComponentAlive && httpListener.IsListening)
            {
                logger.LogInformation("Listening...");
                var result = httpListener.BeginGetContext(HandleRequest, httpListener);
                result.AsyncWaitHandle.WaitOne();
            }

            return Task.CompletedTask;
        }

        private void HandleRequest(IAsyncResult result)
        {
            if (!httpListener.IsListening)
                return;

            var listener = (HttpListener)result.AsyncState;

            var context = listener.EndGetContext(result);
            logger.LogInformation("Handling request from: {RemoteEndPoint} to: {RawUrl}", context.Request.RemoteEndPoint, context.Request.RawUrl);
            var response = context.Response;
            
            var split = context.Request.RawUrl.Split('/').ToList();

            var searchFile = split.LastOrDefault();
            split.Remove(searchFile);

            var path = Path.Combine(split.ToArray());
            path = Path.Combine(directory, path);

            var dir = new DirectoryInfo(path);

            var notFound = Path.Combine(directory, "404.html");
            
            if (!dir.Exists)
            {
                var rawData = File.ReadAllBytes(notFound);

                response.SendResult(rawData);
                return;
            }

            var file = dir.GetFiles().FirstOrDefault(x =>
            {
                var split = x.Name.Split('.').ToList();
                split.RemoveAt(split.Count - 1);

                var fileName = string.Join(".", split);

                return fileName == searchFile || x.Name == searchFile;
            });

            if (file == null || !file.Exists)
            {
                if (dir.GetFiles().FirstOrDefault(x => x.Name == "index.html") != null)
                {
                    file = new FileInfo(dir.GetFiles().FirstOrDefault(x => x.Name == "index.html").FullName);
                }
                else
                {
                    file = new FileInfo(notFound);
                }
            }

            var buffer = File.ReadAllBytes(file.FullName);

            response.SendResult(buffer);
        }

        protected override Task OnUnloadAsync()
        {
            if (httpListener.IsListening)
            {
                httpListener.Stop();
            }

            return Task.CompletedTask;
        }
    }
}
