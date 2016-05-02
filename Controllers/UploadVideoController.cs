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
using homesecurityserviceService.Models;
using System.Threading.Tasks;

/*UPLODED VIDEO IS SENT TO AZURE MEDIA SERVIES TO BE ENCODED INTO MULTIPLE BITRATES TO ALLOW MOBILE CLIENTS VIEW THE VIDEO
 VIDEO URL IS SENT TO USER FOR VIEWING AS WELL AS BEING SAVED TO THE DATABASE FOR FUTURE VIEWING ON MOBILE DEVICES
 */

namespace homesecurityserviceService.Controllers
{
    [MobileAppController]
    public class UploadVideoController : ApiController
    {
        public UploadVideoController() { }

        [HttpPost]
        public IHttpActionResult Post(string email,string roomName)
        {
            var file = HttpContext.Current.Request.Files.Count > 0 ?
         HttpContext.Current.Request.Files[0] : null;

            if (file != null && file.ContentLength > 0)
            {
                var path = Path.Combine(Environment.GetFolderPath(
Environment.SpecialFolder.ApplicationData), Path.GetFileName(file.FileName));

                file.SaveAs(path);

                Task.Factory.StartNew(() => uploadVideoToAMSAsync(path,file.FileName,email,roomName));

                return Ok();
            }

            return BadRequest();
        }

        //SEND USER AN EMAIL WHERE THEY WILL BE DIRECTED TO THE HOMESECURITY WEBSITE TO VIEW THE VIDEO THAT WAS JUST UPLOADED
        //THE VIDEOS PUBLISHED URL IS ENCODED SO THAT THE URL ISNT VISIBLE ON THE WEBSITE
        private void SendEmail(string encodedStreamingUrl,string email)
        {
            string link = "http://homesecurityapp.azurewebsites.net/Video/PlayVideo/?video=" + encodedStreamingUrl;
            RestClient client = new RestClient();
            client.BaseUrl = new Uri("https://api.mailgun.net/v3");
            client.Authenticator =
                    new HttpBasicAuthenticator("api",
                                               "");
            RestRequest request = new RestRequest();
            request.AddParameter("domain",
                                 "", ParameterType.UrlSegment);
            request.Resource = "{domain}/messages";
            request.AddParameter("from", "Home Security <homesecurity@security.ie>");
            request.AddParameter("to", email);
            request.AddParameter("subject", "Motion Detected");
            request.AddParameter("html", "<html>A security camera has produced the following 30 second video which you can watch " +
             "<a href=\"" + link + "\">here</a>." + "</html>");

            request.Method = Method.POST;
            client.Execute(request);
        }

        private void uploadVideoToAMSAsync(string path,string fileName,string email,string roomName)
        {
            //get reference to DB context
            homesecurityserviceContext db_context = new homesecurityserviceContext();

            //upload the video as an asset to AMS using the path where the video has been stored 
            var context = new CloudMediaContext("", "");
            var uploadAsset = context.Assets.Create(Path.GetFileName(fileName), AssetCreationOptions.None);
            var assetFile = uploadAsset.AssetFiles.Create(Path.GetFileName(fileName));
            assetFile.Upload(path);

            File.Delete(path); //delete the file that was saved locally

            var encodeAssetId = uploadAsset.Id; 
            var encodingPreset = "H264 Multiple Bitrate 720p"; //multi bitrate allows the video to be viewed on phones
            var assetToEncode = context.Assets.Where(a => a.Id == encodeAssetId).FirstOrDefault();
            if (assetToEncode == null)
            {
                throw new ArgumentException("Could not find assetId: " + encodeAssetId);
            }

            //Encoding process
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

            //Returning a streamingUrl for the video
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

            string dateString = DateTime.Now.ToString(@"MM\/dd\/yyyy HH:mm"); //format the date will be saved as (for the android app)

            //save the url of the video to the database so that a user can have a list of their videos in the android app
            Video v = new Video
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                Vid = streamingUrl,
                RoomName = roomName,
                CreatedAt = dateString
            };
            db_context.Videos.Add(v);
            db_context.SaveChanges();

            //encode the streaming url so that its not visible in the url
            var encodedUrl = System.Text.Encoding.UTF8.GetBytes(streamingUrl);
            SendEmail(System.Convert.ToBase64String(encodedUrl), email);
        }
    }
}
