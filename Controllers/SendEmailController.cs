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
        [HttpPost]
        public HttpResponseMessage Post([FromBody]string image,string email)
        {
            var message = "Sent";

            try
            {
                SendPhoto(UploadImage(image), email);
            }
            catch (Exception e)
            {

                message = e.Message + "," + e.StackTrace + "," + e.Source;
                return this.Request.CreateResponse(HttpStatusCode.BadRequest, new { message });
            }

            return this.Request.CreateResponse(HttpStatusCode.Created, new { message });
        }

        public string UploadImage(string encodedImage)
        {
			//https://azure.microsoft.com/en-in/documentation/articles/storage-dotnet-shared-access-signature-part-2/
            // Retrieve storage account from connection string.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                "**");
            //Sensitive information was replaced with ** from the above string for protection

            // Create the blob client.
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve reference to a previously created container.
            CloudBlobContainer container = blobClient.GetContainerReference("images");

            //Create the "images" container if it doesn't already exist.
            container.CreateIfNotExists();

            // Retrieve reference to a blob with this name
            //Images will be saved in the format "image_[Guid][Date]"
            blockBlob = container.GetBlockBlobReference("image_" + Guid.NewGuid() + System.DateTime.Now);

            byte[] imageBytes = Convert.FromBase64String(encodedImage);

            // create or overwrite the blob named "image_" and the current date and time 
            blockBlob.UploadFromByteArray(imageBytes, 0, imageBytes.Length);

            //Set the expiry time and permissions for the blob.
            //Start time is not specified meaning that this SAS is activated immediately
           
            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy();
            //Access is Valid for a week
            sasConstraints.SharedAccessExpiryTime = DateTime.UtcNow.AddDays(7);
            sasConstraints.Permissions = SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Write;

            //Generate the shared access signature on the blob, setting the constraints directly on the signature.
            string sasBlobToken = blockBlob.GetSharedAccessSignature(sasConstraints);

            //Return the URI string for the container, including the SAS token.
            return blockBlob.Uri + sasBlobToken;
        }

        public string getImageURL()
        {
            return blockBlob.StorageUri.PrimaryUri.AbsoluteUri;
        }

        private void SendPhoto(string imageUrl,string email)
        {
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
            request.AddParameter("html", "<html>A security camera has detected motion. Here is the image showing what " +
            "has triggered the motion detection system." +
             "<img src=\"" + imageUrl + "\">" + "</html>");

            request.Method = Method.POST;
            client.Execute(request);
        }
    }
}
