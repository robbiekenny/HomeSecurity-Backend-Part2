using Microsoft.Azure.Mobile.Server.Config;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Configuration;
using System.IO;

namespace homesecurityserviceService.Controllers
{
    [MobileAppController]
    public class SendEmailController : ApiController
    {
        CloudBlockBlob blockBlob;

        public SendEmailController()
        {

        }

        // POST api/SendEmail
        public HttpResponseMessage Post([FromBody]string image,string email)
        {
            var message = "Sent";

            try
            {
                UploadImage(image);
                string url = getImageURL();
                SendPhoto(url,email);
            }
            catch (Exception e)
            {
                
                message = e.Message + "," + e.StackTrace + "," + e.Source;
                return this.Request.CreateResponse(HttpStatusCode.BadRequest, new { message });
            }

            return this.Request.CreateResponse(HttpStatusCode.Created, new { message });
        }

        public void UploadImage(string encodedImage)
        {
            // Retrieve storage account from connection string.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                "DefaultEndpointsProtocol=https;AccountName=**;AccountKey=**");
            //Sensitive information was replaced with ** from the above string for protection

            // Create the blob client.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve reference to a previously created container.
            CloudBlobContainer container = blobClient.GetContainerReference("images");

            //Create the "images" container if it doesn't already exist.
            if (container.CreateIfNotExists())
            {
                // Enable public access on the newly created "images" container
                container.SetPermissionsAsync(
                   new BlobContainerPermissions
                   {
                       PublicAccess =
                           BlobContainerPublicAccessType.Blob
                   });


            }

            // Retrieve reference to a blob with this name
            //Images will be saved in the format "image_[Guid][Date]"
            blockBlob = container.GetBlockBlobReference("image_" + Guid.NewGuid() + System.DateTime.Now);

            byte[] imageBytes = Convert.FromBase64String(encodedImage);

            // create or overwrite the blob named "image_" and the current date and time 
            blockBlob.UploadFromByteArray(imageBytes, 0, imageBytes.Length);
        }

        public string getImageURL()
        {
            return blockBlob.StorageUri.PrimaryUri.AbsoluteUri;
        }

        public static IRestResponse SendPhoto(string imageUrl,string email)
        {
            RestClient client = new RestClient();
            client.BaseUrl = new Uri("https://api.mailgun.net/v3");
            client.Authenticator =
                    new HttpBasicAuthenticator("api",
                                               "key-7849f01c422d48638073c526a5b69ee1");
            RestRequest request = new RestRequest();
            request.AddParameter("domain",
                                 "sandbox09debf1633754ea39b8f70e8c5dc3a35.mailgun.org", ParameterType.UrlSegment);
            request.Resource = "{domain}/messages";
            request.AddParameter("from", "Home Security <homesecurity@security.ie>");
            request.AddParameter("to", email);
            request.AddParameter("subject", "Motion Detected");
            request.AddParameter("html", "<html>A security camera has detected motion. Here is the image showing what " +
            "has triggered the motion detection system." +
             "<img src=\"" + imageUrl + "\">" + "</html>");

            //request.AddFile("attachment", Path.Combine("files", "test.jpg"));
            //request.AddFile("attatchment", ms.ToArray(), "test.jpg");


            request.Method = Method.POST;
            return client.Execute(request);
        }
    }
}
