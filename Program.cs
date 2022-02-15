// See https://aka.ms/new-console-template for more information
using System;
using System.Diagnostics;
using System.Globalization;
using System.Web;
using System.Timers;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace urlhandler
{
    internal class Program
    {
        private static string _token = string.Empty;
        private static string _url = string.Empty;
        private static System.Timers.Timer _timer = new System.Timers.Timer(55 * 60 * 1000); //one hour in milliseconds

        private static async void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            Console.WriteLine("Refresh Token");
            //Do the stuff you want to be done every hour;
            await RefreshToken(_url, _token);
        }

        static async Task<int> Main(string[] args)
        {
            args = Environment.GetCommandLineArgs();
            if (args.Length < 2)
            {
                Console.WriteLine("Argument needed.");
                Console.ReadLine();
                return 1;
            }

            _timer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            _timer.Start();
            string url = string.Join(" ", args[1..]);

            Console.ForegroundColor = ConsoleColor.White;
            _url = url.Replace("chemotion://", string.Empty);
            Console.WriteLine($"This is what I got: {url}\n");
            var directory = $"{Path.GetTempPath()}/ChemotionTemp";
            Directory.CreateDirectory(directory);
            string fileName = String.Empty;
            var uri = new Uri(_url);
            var queryDictionary = HttpUtility.ParseQueryString(uri.Query);
            _token = queryDictionary["token"];
            Console.WriteLine("Start download");
            fileName = await DownloadFile(directory, _url, fileName, _token);
            _url = uri.GetLeftPart(UriPartial.Authority);
            Console.WriteLine("Finish download");
            string filePath = string.Empty;
            filePath = $"{directory}/{fileName}";
            DateTime lmod = File.GetLastWriteTime(filePath);
            using (Process myProcess = new Process())
            {
                myProcess.StartInfo.UseShellExecute = true;
                myProcess.StartInfo.FileName = filePath;
                myProcess.StartInfo.Arguments = filePath;
                myProcess.StartInfo.CreateNoWindow = true;
                myProcess.StartInfo.Verb = "";
                myProcess.Start();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("Waiting for process to exit. Don't turn it off!!!");
                await myProcess.WaitForExitAsync();
                Console.WriteLine("\n");
            }

            if (File.GetLastWriteTime(filePath) > lmod)
            {
                var result = await DoneEditing(directory, _url, fileName, _token);
                Console.ForegroundColor = ConsoleColor.Blue;
            }
            else
            {
                var result = await DoneEditing(directory, _url, null, _token);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.ResetColor();
            }

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            Console.Write("Finish!!!");
            return 0;
        }

        static async Task<string> DownloadFile(string directory, string url, string filename, string token)
        {
            string fileName = String.Empty;
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(url);
                fileName = response.Content.Headers?.ContentDisposition?.FileName.Replace("\"", "");
                var filePath = $"{directory}/{fileName}";
                using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }
            return fileName;
        }

        static async Task<bool> DoneEditing(string directory, string url, string fileName, string token)
        {
            Console.WriteLine(url);
            using (var client = new HttpClient())
            {
                using (var content =
                    new MultipartFormDataContent("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture)))
                {
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        using var fs = new StreamReader($"{directory}/{fileName}");
                        var bytes = File.ReadAllBytes($"{directory}/{fileName}");
                        content.Add(new StreamContent(new MemoryStream(bytes)), "file", fileName);
                    }

                    content.Add(new StringContent(token), "token");
                    using var message = await client.PostAsync($"{url}/api/v1/public/done", content);
                    var input = await message.Content.ReadAsStringAsync();
                }
            }

            return true;
        }

        static async Task RefreshToken(string url, string token)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var message = await client.GetAsync($"{url}/api/v1/public/refresh_token");
                var input = await message.Content.ReadAsStringAsync();
                var result = JObject.Parse(input);
                _token = result["token"].ToString();
            }
        }
    }
}
