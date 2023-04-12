using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace AzureDownloader
{
    public class AzureArtifactDownloader
    {
        private static ILogger _logger;
        private readonly IConfiguration _configuration;
        private static readonly HttpClient Client = new();

        public AzureArtifactDownloader(ILogger<AzureArtifactDownloader> logger, IConfiguration config)
        {
            _configuration = config;
            _logger = logger;
        }

        public async Task Run()
        {
            try
            {
                _logger.LogInformation("Gathering Configuration");
                var azurePAT = _configuration.GetSection("Configuration")["AzurePAT"];
                var organization = _configuration.GetSection("Configuration")["AzureOrg"];
                var project = _configuration.GetSection("Configuration")["AzureProject"];
                var tempDirectory = _configuration.GetSection("Configuration")["TempDirectory"];
                var packageZipDirectory = _configuration.GetSection("Configuration")["ZipLocation"];
                Client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(
                        Encoding.ASCII.GetBytes(
                            $" :{azurePAT}")));
                _logger.LogInformation("Gathering builds");
                var response =
                    await Client.GetAsync($"https://dev.azure.com/{organization}/{project}/_apis/build/builds/");

                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();

                var azureDevOpsObject = JsonConvert.DeserializeObject<AzureDevOpsObject>(responseBody);
                if (azureDevOpsObject is { count: 0 })
                {

                }
                else
                {
                    var buildsFound = new List<PackageData>();
                    if (azureDevOpsObject != null)
                        foreach (var item in azureDevOpsObject.value)
                        {
                            _logger.LogInformation($"Gathering artifacts from {item.definition.name}");
                            var artifactsResponse = await Client.GetAsync(
                                $"https://dev.azure.com/{organization}/{project}/_apis/build/builds/{item.id}/artifacts");
                            var artifactResponse = await artifactsResponse.Content.ReadAsStringAsync();

                            var azureArtifactsObject = JsonConvert.DeserializeObject<Artifact>(artifactResponse);
                            if (item.sourceBranch.Contains("main") && item.sourceBranch.Contains("master")) continue;
                            var packageFound = new PackageData
                            {
                                BuildID = item.id,
                                PackageName = item.definition.name,
                            };
                            if (azureArtifactsObject != null && azureArtifactsObject.count != 0)
                            {
                                foreach (var azureArtifact in azureArtifactsObject.value)
                                {
                                    if (!azureArtifact.name.Contains('$'))
                                    {
                                        packageFound.DownloadURL = azureArtifact.resource.downloadUrl;
                                        if (azureArtifact.name.Contains(".zip"))
                                        {
                                            packageFound.FileName = azureArtifact.name;
                                        }
                                        else
                                        {
                                            packageFound.FileName = azureArtifact.name + ".zip";
                                        }
                                    }
                                    else
                                    {
                                        packageFound.FileName = null;
                                    }
                                }
                            }

                            if (packageFound.FileName == null) continue;
                            _logger.LogInformation(
                                $"Adding {packageFound.PackageName} with filename {packageFound.FileName}");
                            buildsFound.Add(packageFound);
                        }


                    if (buildsFound.Count == 0)
                    {
                        _logger.LogInformation("No builds found");
                    }
                    foreach (var packageToDownload in buildsFound.Where(packageToDownload => !packageToDownload.FileName.Contains("drop")))
                    {

                        var packageTempDirectory = tempDirectory +
                                                       Path.DirectorySeparatorChar +
                                                       packageToDownload.PackageName +
                                                       Path.DirectorySeparatorChar;
                        var packageTempFileName = packageTempDirectory +
                                                      Path.DirectorySeparatorChar +
                                                      packageToDownload.FileName;
                        var packageTempFolderOutput = packageTempDirectory +
                                                          Path.DirectorySeparatorChar +
                                                          "output" +
                                                          Path.DirectorySeparatorChar;

                        var packageReleaseFolder = packageZipDirectory +
                                                       Path.DirectorySeparatorChar +
                                                       "release" +
                                                       Path.DirectorySeparatorChar;
                        var packageReleaseFileNameFolder = packageReleaseFolder +
                                                               packageToDownload.PackageName +
                                                               Path.DirectorySeparatorChar;
                        byte[] artifactStream = null;
                        if (!File.Exists(packageTempFileName))
                        {
                            _logger.LogInformation($"Downloading file: {packageToDownload.DownloadURL}");
                            var downloadResponse = await Client.GetAsync(packageToDownload.DownloadURL);
                            artifactStream = await downloadResponse.Content.ReadAsByteArrayAsync();
                        }


                        CreateDirectoriesNeeded(packageTempDirectory, packageReleaseFolder, packageReleaseFileNameFolder, packageTempFolderOutput);

                        if (File.Exists(packageReleaseFileNameFolder + packageToDownload.FileName))
                            continue;
                        if (!File.Exists(packageTempFileName))
                        {
                            await File.WriteAllBytesAsync(packageTempFileName, artifactStream);
                        }
                        _logger.LogInformation("Extracting Downloaded Zip File");

                        await Task.Run(() =>
                        {
                            using var archive = ZipFile.Open(packageTempFileName, ZipArchiveMode.Read);
                            if (File.Exists(
                                    packageTempFolderOutput + packageToDownload.FileName + Path.DirectorySeparatorChar +
                                    packageToDownload.FileName)) return;
                            try
                            {
                                archive.ExtractToDirectory(packageTempFolderOutput);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Unable to extract zip file: {ex.Message}");
                            }

                        });

                        await Task.Run(() =>
                        {

                            if (packageTempDirectory == null) throw new ArgumentNullException(nameof(packageTempDirectory));
                            if (packageToDownload == null) throw new ArgumentNullException(nameof(packageToDownload));
                            if (packageZipDirectory == null) throw new ArgumentNullException(nameof(packageZipDirectory));
                            _logger.LogInformation("Moving work packages to release folder");
                            if (File.Exists(packageZipDirectory + "/release/" +
                                            packageToDownload.PackageName + "/" +
                                            packageToDownload.FileName))
                            {


                            }
                            else
                            {
                                File.Copy(
                                    packageTempDirectory + "/output/" + packageToDownload.FileName +
                                    "/" +
                                    packageToDownload.FileName,
                                    packageZipDirectory + "/release/" +
                                    packageToDownload.PackageName + "/" +
                                    packageToDownload.FileName);

                            }

                        });
                    }
                }

            }


            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                _logger.LogError("Message :{0} ", e.Message);
            }
        }



        private static void CreateDirectoriesNeeded(string packageTempDirectory, string packageReleaseFolder,
            string packageReleaseFileNameFolder, string packageTempFolderOutput)
        {
            if (packageTempDirectory != null && !Directory.Exists(packageTempDirectory))
            {
                Directory.CreateDirectory(packageTempDirectory);
            }

            if (packageReleaseFolder != null && !Directory.Exists(packageReleaseFolder))
            {
                Directory.CreateDirectory(packageReleaseFolder);
            }

            if (packageReleaseFileNameFolder != null && !Directory.Exists(packageReleaseFileNameFolder))
            {
                Directory.CreateDirectory(packageReleaseFileNameFolder);
            }

            if (packageTempFolderOutput != null && !Directory.Exists(packageTempFolderOutput))
            {
                Directory.CreateDirectory(packageTempFolderOutput);
            }
        }


    }
}
