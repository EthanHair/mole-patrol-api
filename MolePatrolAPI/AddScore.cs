using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Storage.Blobs;
using Azure.Identity;
using System.Collections.Generic;

namespace MolePatrolAPI
{
    public static class AddScore
    {
        [FunctionName("AddScore")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("-----");
            log.LogInformation("Recieved a request to record a score");

            string name = string.Empty;
            int score = 0;
            string mode = string.Empty;

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            try
            {
                name = data?.name;
                score = data?.score;
                mode = data?.mode;
            }
            catch (Exception ex)
            {
                log.LogError($"Error: {ex.Message}");
                return new BadRequestObjectResult($"Error: {ex.Message}");
            }

            if (String.IsNullOrWhiteSpace(name))
            {
                log.LogError("Error: Name cannot be null or whitespace");
                return new BadRequestObjectResult("Error: Name cannot be null or whitespace");
            }
            if (String.IsNullOrWhiteSpace(mode))
            {
                log.LogError("Error: Mode cannot be null or whitespace");
                return new BadRequestObjectResult("Error: Mode cannot be null or whitespace");
            }
            if (score < 0)
            {
                log.LogError("Error: Score cannot be zero or negative");
                return new BadRequestObjectResult("Error: Score cannot be zero or negative");
            }

            log.LogInformation("Successfully retrieved and validated the information. Storing the score...");

            // Connecting to blob storage
            log.LogInformation("Attempting to connect to blob storage");
            BlobContainerClient containerClient;
            try
            {
                // This commented area is for when it is deployed. Hopefully it works
                //var blobServiceClient = new BlobServiceClient(
                //                        new Uri("https://molepatrolblobstorage.blob.core.windows.net"),
                //                        new DefaultAzureCredential());
                string connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

                var blobServiceClient = new BlobServiceClient(connectionString);

                string containerName = "molepatrolapiscores";

                containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            }
            catch(Exception ex) 
            {
                log.LogError($"Error: {ex.Message}");
                return new BadRequestObjectResult($"Error: {ex.Message}");
            }
            log.LogInformation("Successfully connected to blob storage");

            // Create a local file in the ./data/ directory for uploading and downloading
            string localPath = "data";
            Directory.CreateDirectory(localPath);
            string fileName = "scorefile.csv";
            string localFilePath = Path.Combine(localPath, fileName);

            await File.AppendAllLinesAsync(localFilePath, new List<string>() { $"{name},{score},{mode}" });

            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            log.LogInformation("Uploading to Blob storage as blob:\n\t {0}\n", blobClient.Uri);

            // Upload data from the local file
            await blobClient.UploadAsync(localFilePath, true);

            string responseMessage = $"Successfully recorded following score: Name: {name}, Score: {score}, Mode: {mode}";

            log.LogInformation(responseMessage);
            log.LogInformation("-----");

            return new OkObjectResult(responseMessage);
        }
    }
}
