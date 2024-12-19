﻿using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System.Diagnostics;
using System.Text;
//using static System.Net.WebRequestMethods;

namespace DataSubmissionApp.Helpers
{
    public class DonwloadAndInstallService
    {
        public  async Task StartDownloading()
        {
            string url1 = "http://localhost/data/DataSubmit.zip";
            string url = "http://dl.dropboxusercontent.com/scl/fi/ad8im5tynj5co31kv6oq3/DataSubmit.zip?rlkey=6pu74c0mv5pmh9rru0xe1b2ef&st=04nuqh1z&dl=0";
            string targetPath = @"C:\ProgramData\FairbancData";
            string downloadPath = Path.Combine(targetPath, "DataSubmit.zip");
            string extractPath = Path.Combine(targetPath, "extracted");
            string zipPassword = "F41rb4nc";

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    await using (FileStream fileStream = new FileStream(downloadPath, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fileStream);
                    }
                }
                using (FileStream fs = File.OpenRead(downloadPath))
                using (ZipFile zipFile = new ZipFile(fs))
                {
                    if (!string.IsNullOrEmpty(zipPassword))
                    {
                        zipFile.Password = zipPassword;
                    }

                    foreach (ZipEntry entry in zipFile)
                    {
                        if (!entry.IsFile)
                            continue;
                        string entryFileName = entry.Name;
                        string fullZipToPath = Path.Combine(extractPath, entryFileName);
                        string directoryName = Path.GetDirectoryName(fullZipToPath);
                        if (directoryName.Length > 0)
                        {
                            Directory.CreateDirectory(directoryName);
                        }
                        using (FileStream streamWriter = File.Create(fullZipToPath))
                        {
                            byte[] buffer = new byte[4096];
                            Stream zipStream = zipFile.GetInputStream(entry);
                            StreamUtils.Copy(zipStream, streamWriter, buffer);
                        }
                    }
                }
                string[] extractedFiles = Directory.GetFiles(extractPath, "*.*", SearchOption.AllDirectories);
                foreach (string file in extractedFiles)
                {
                    string fileName = Path.GetFileName(file);
                    string destFile = Path.Combine(targetPath, fileName);
                    if (File.Exists(destFile)) { File.Delete(destFile); }
                    File.Copy(file, destFile);
                }
                bool isInstalled = false;
                foreach (string file in extractedFiles)
                {
                    if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        string fileName = Path.GetFileName(file);
                        string destFile = Path.Combine(targetPath, fileName);

                        await GetServiceStateAsync($"\"{destFile}\" install start");

                        var result = await GetServiceStateAsync("sc query DataSubmission");
                        if (result == "RUNNING" || result == "STOPPED")
                        {
                            isInstalled = true;
                            if (result == "RUNNING")
                            {
                                await GetServiceStateAsync("stop DataSubmission", true);
                            }
                        }
                    }
                }

                Console.WriteLine($"File has been downloaded, extracted, moved, and the Windows Service is installed are  => {isInstalled}.");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private async Task<string> GetServiceStateAsync(string command, bool directSC = false)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo();
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            if (!directSC)
            {
                processInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas"
                };
            }
            else
            {
                processInfo = new ProcessStartInfo
                {
                    FileName = "sc",
                    Arguments = $"{command}",
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    Verb = "runas"
                };
            }

            using (var process = new Process { StartInfo = processInfo })
            {
                process.OutputDataReceived += (sender, args) => outputBuilder.AppendLine(args.Data);
                process.ErrorDataReceived += (sender, args) => errorBuilder.AppendLine(args.Data);

                process.Start();
                if (!directSC)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await Task.Run(() => process.WaitForExit());
                }
                else
                {
                    await Task.Run(() => process.WaitForExit());
                    return "";
                }

                var outputs = outputBuilder.ToString();
                var errors = errorBuilder.ToString();

                if (outputs.Contains("RUNNING"))
                {
                    return "RUNNING";
                }
                else if (outputs.Contains("STOPPED"))
                {
                    return "STOPPED";
                }
                else if (outputs.Contains("not exist"))
                {
                    return "NOTINSTALLED";
                }
                else
                {
                    return "UNKNOWN";
                }
            }
        }


        public async Task<Boolean> IsServiceInstalled()
        {
            try
            {
                var result = await GetServiceStateAsync("sc query DataSubmission");
                if (result == "NOTINSTALLED") 
                { return false; }
                return true;
            }
            catch (Exception ex)
            {
                return false;
                throw ex;
            }
        }
    }

}
