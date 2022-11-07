using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Identity;
using Azure.Storage.Blobs;
using System.Collections.Generic;
using MolePatrolAPI.Models;
using System.Linq;

namespace MolePatrolAPI
{
    public static class GetLeaderboard
    {
        [FunctionName("GetLeaderboard")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("-----");
            log.LogInformation("Recieved a request to recieve the leaderboard");

            string userName = string.Empty;
            string requestedModeString = string.Empty;
            GameMode requestedMode = GameMode.Slow;
            List<ScoreItem> scores = new();

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            try
            {
                userName = data?.name;
                requestedModeString = data?.mode;
            }
            catch (Exception ex)
            {
                log.LogError($"Error: {ex.Message}");
                return new BadRequestObjectResult($"Error: {ex.Message}");
            }

            if (userName is null)
            {
                log.LogError("Error: Name cannot be null");
                return new BadRequestObjectResult("Error: Name cannot be null");
            }

            if (requestedModeString is null)
            {
                log.LogError("Error: Mode cannot be null");
                return new BadRequestObjectResult("Error: Mode cannot be null");
            }

            switch (requestedModeString.ToLower())
            {
                case "slow":
                    requestedMode = GameMode.Slow;
                    break;
                case "medium":
                    requestedMode = GameMode.Medium;
                    break;
                case "fast":
                    requestedMode = GameMode.Fast;
                    break;
                case "hard":
                    requestedMode = GameMode.Hard;
                    break;
                default:
                    log.LogError($"Error: Could not match the requested Mode Filter \"{requestedModeString}\" to a GameMode Type");
                    break;
            }

            log.LogInformation("Successfully retrieved and validated the information. Retrieving the leaderboard");

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
            catch (Exception ex)
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

            BlobClient blobClient = containerClient.GetBlobClient(fileName);

            log.LogInformation("Downloading blob to\n\t{0}\n", blobClient.Uri);

            // Upload data from the local file
            await blobClient.DownloadToAsync(localFilePath);

            // Reading the csv file
            string[] lines = await File.ReadAllLinesAsync(localFilePath);

            foreach (string line in lines)
            {
                bool errorReadingLine = false;

                string[] items = line.Split(",");

                if (items.GetLength(0) == 3)
                {
                    string name = items[0];

                    int score = 0;

                    if (!Int32.TryParse(items[1], out score))
                    {
                        errorReadingLine = true;
                        log.LogError($"Error: Could not convert \"{items[1]}\" to an integer");
                    }

                    GameMode mode = GameMode.Slow;

                    switch (items[2].ToLower())
                    {
                        case "slow":
                            mode = GameMode.Slow;
                            break;
                        case "medium":
                            mode = GameMode.Medium;
                            break;
                        case "fast":
                            mode = GameMode.Fast;
                            break;
                        case "hard":
                            mode = GameMode.Hard;
                            break;
                        default:
                            errorReadingLine = true;
                            log.LogError($"Error: Could not match \"{items[2]}\" to a GameMode Type for the line: \"{line}\"");
                            break;
                    }

                    if (errorReadingLine)
                    {
                        log.LogError($"Error: There was an error converting the line: \"{line}\" into a ScoreItem");
                    }
                    else
                    {
                        var scoreItem = new ScoreItem(name, mode, score, userName == name);

                        scores.Add(scoreItem);
                    }
                }
                else
                {
                    log.LogError($"Error: The line: \"{line}\" is not the correct length to convert to a ScoreItem");
                }
            }

            // Sort
            scores = scores.Where(x => x.Mode == requestedMode)
                           .OrderByDescending(x => x.Score)
                           .ToList();

            log.LogInformation("Successfully retrieved and processed the leaderboard");
            log.LogInformation("Sending the leaderboard");
            log.LogInformation("-----");

            return new OkObjectResult(scores);
        }
    }
}
