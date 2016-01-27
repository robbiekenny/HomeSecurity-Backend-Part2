using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Configuration;
using System.IO;

namespace homesecurityserviceService
{
    public class UploadFile
    {
        // Retrieve storage account from connection string.
CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
    ConfigurationManager.ConnectionStrings["StorageConnectionString"].ConnectionString);

        CloudBlobClient blobClient;
        CloudBlobContainer container;
        CloudBlockBlob blockBlob;

        public UploadFile(string encodedString)
        {
            // Create the blob client.
             blobClient = storageAccount.CreateCloudBlobClient();

            // Retrieve reference to a previously created container.
             container = blobClient.GetContainerReference("images");

            // Retrieve reference to a blob named "myblob".
             blockBlob = container.GetBlockBlobReference("image1");

            byte[] imageBytes = Convert.FromBase64String(encodedString);
            MemoryStream ms = new MemoryStream(imageBytes, 0, imageBytes.Length);

            // Create or overwrite the "myblob" blob 
            
                blockBlob.UploadFromStream(ms);
            
        }

        public string GetBLOBUrl()
        {
            return blockBlob.StorageUri.PrimaryUri.AbsoluteUri;
        }
    }
}