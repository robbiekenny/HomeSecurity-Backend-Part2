using Microsoft.Azure.Mobile.Server.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.IO.Compression;
using System.Web.Http;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using RestSharp;
using RestSharp.Authenticators;
using System.Text;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Globalization;
using System.Threading;

namespace homesecurityserviceService.Controllers
{
    [MobileAppController]
    public class UploadVideoController : ApiController
    {
        public UploadVideoController() { }

        [HttpPost]
        public IHttpActionResult Post(string email)
        {
            var file = HttpContext.Current.Request.Files.Count > 0 ?
         HttpContext.Current.Request.Files[0] : null;

            if (file != null && file.ContentLength > 0)
            {
                var path = Path.Combine(Environment.GetFolderPath(
    Environment.SpecialFolder.ApplicationData), Path.GetFileName(file.FileName));
                
                file.SaveAs(path);

                var context = new CloudMediaContext("**", "**");
                var uploadAsset = context.Assets.Create(Path.GetFileName(file.FileName), AssetCreationOptions.None);
                var assetFile = uploadAsset.AssetFiles.Create(Path.GetFileName(file.FileName));
                assetFile.Upload(path);

                File.Delete(path);

                var encodeAssetId = uploadAsset.Id; // "YOUR ASSET ID";
                var encodingPreset = "H264 Multiple Bitrate 720p";
                var assetToEncode = context.Assets.Where(a => a.Id == encodeAssetId).FirstOrDefault();
                if (assetToEncode == null)
                {
                    throw new ArgumentException("Could not find assetId: " + encodeAssetId);
                }

                IJob job = context.Jobs.Create("Encoding " + assetToEncode.Name + " to " + encodingPreset);

                IMediaProcessor latestWameMediaProcessor = (from p in context.MediaProcessors where p.Name == "Media Encoder Standard" select p).ToList().OrderBy(wame => new Version(wame.Version)).LastOrDefault();
                ITask encodeTask = job.Tasks.AddNew("Encoding", latestWameMediaProcessor, encodingPreset, TaskOptions.None);
                encodeTask.InputAssets.Add(assetToEncode);
                encodeTask.OutputAssets.AddNew(assetToEncode.Name + " as " + encodingPreset, AssetCreationOptions.None);

                job.StateChanged += new EventHandler<JobStateChangedEventArgs>((sender, jsc) => Console.WriteLine(string.Format("{0}\n  State: {1}\n  Time: {2}\n\n", ((IJob)sender).Name, jsc.CurrentState, DateTime.UtcNow.ToString(@"yyyy_M_d_hhmmss"))));
                job.Submit();
                job.GetExecutionProgressTask(CancellationToken.None).Wait();

                var preparedAsset = job.OutputMediaAssets.FirstOrDefault();

                var streamingAssetId = preparedAsset.Id; // "YOUR ASSET ID";
                var daysForWhichStreamingUrlIsActive = 365;
                var streamingAsset = context.Assets.Where(a => a.Id == streamingAssetId).FirstOrDefault();
                var accessPolicy = context.AccessPolicies.Create(streamingAsset.Name, TimeSpan.FromDays(daysForWhichStreamingUrlIsActive),
                                                         AccessPermissions.Read);
                string streamingUrl = string.Empty;
                var assetFiles = streamingAsset.AssetFiles.ToList();
                var streamingAssetFile = assetFiles.Where(f => f.Name.ToLower().EndsWith("m3u8-aapl.ism")).FirstOrDefault();
                if (streamingAssetFile != null)
                {
                    var locator = context.Locators.CreateLocator(LocatorType.OnDemandOrigin, streamingAsset, accessPolicy);
                    Uri hlsUri = new Uri(locator.Path + streamingAssetFile.Name + "/manifest(format=m3u8-aapl)");
                    streamingUrl = hlsUri.ToString();
                }
                streamingAssetFile = assetFiles.Where(f => f.Name.ToLower().EndsWith(".ism")).FirstOrDefault();
                if (string.IsNullOrEmpty(streamingUrl) && streamingAssetFile != null)
                {
                    var locator = context.Locators.CreateLocator(LocatorType.OnDemandOrigin, streamingAsset, accessPolicy);
                    Uri smoothUri = new Uri(locator.Path + streamingAssetFile.Name + "/manifest");
                    streamingUrl = smoothUri.ToString();
                }
                streamingAssetFile = assetFiles.Where(f => f.Name.ToLower().EndsWith(".mp4")).FirstOrDefault();
                if (string.IsNullOrEmpty(streamingUrl) && streamingAssetFile != null)
                {
                    var locator = context.Locators.CreateLocator(LocatorType.Sas, streamingAsset, accessPolicy);
                    var mp4Uri = new UriBuilder(locator.Path);
                    mp4Uri.Path += "/" + streamingAssetFile.Name;
                    streamingUrl = mp4Uri.ToString();
                }


                //encode the streaming url so that its not visible in the url
                var encodedUrl = System.Text.Encoding.UTF8.GetBytes(streamingUrl);



                SendEmail(System.Convert.ToBase64String(encodedUrl),email);

                return Ok();
            }

            return BadRequest();
        }

        private void SendEmail(string encodedStreamingUrl,string email)
        {
            string link = "http://homesecurityapp.azurewebsites.net/Video/PlayVideo/" + encodedStreamingUrl;
            RestClient client = new RestClient();
            client.BaseUrl = new Uri("https://api.mailgun.net/v3");
            client.Authenticator =
                    new HttpBasicAuthenticator("api",
                                               "**");
            RestRequest request = new RestRequest();
            request.AddParameter("domain",
                                 "**", ParameterType.UrlSegment);
            request.Resource = "{domain}/messages";
            request.AddParameter("from", "Home Security <homesecurity@security.ie>");
            request.AddParameter("to", email);
            request.AddParameter("subject", "Motion Detected");
            request.AddParameter("html", "<html>A security camera has produced the following 30 second video which you can watch " +
             "<a href=\"" + link + "\">here</a>." + "</html>");

            request.Method = Method.POST;
            client.Execute(request);
        }
    }
}
