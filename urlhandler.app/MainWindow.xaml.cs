using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Shapes;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Timers;
using System.Web;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Windows.Threading;

namespace urlhandler.app
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly StringBuilder stringBuilder;
        public MainWindow()
        {
            stringBuilder = new StringBuilder();
            InitializeComponent();
        }

        private static string _token = string.Empty;
        private static string _url = string.Empty;
        private static System.Timers.Timer _timer = new System.Timers.Timer(55 * 60 * 1000); //one hour in milliseconds

        private async void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            Console.WriteLine("Refresh Token");
            await RefreshToken();
        }

        async Task<int> Main(string[] args)
        {
            args = Environment.GetCommandLineArgs();
            if (args.Length < 2)
            {
                stringBuilder.AppendLine("Argument needed.");
                txtInfo.Text = stringBuilder.ToString();
                btnClose.IsEnabled = true;
                return 0;
            }

            var directory = $"{System.IO.Path.GetTempPath()}ChemotionTemp";
            Directory.CreateDirectory(directory);
            _timer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
            _timer.Start();

            Console.ForegroundColor = ConsoleColor.White;
            GetServerInfo(args);

            string fileName = await DownloadFile(directory);
            if (string.IsNullOrEmpty(fileName))
            {
                stringBuilder.AppendLine("Download file error. Press any close button to exit.");
                txtInfo.Text = stringBuilder.ToString();
                btnClose.IsEnabled = true;
                return 0;
            }

            await EditProcess(directory, fileName);
            string filePath = $"{directory}\\{fileName}";
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return 0;
        }

        void GetServerInfo(string[] args)
        {
            string url = string.Join(" ", args[1..]);
            _url = url.Replace("chemotion://", string.Empty);
            stringBuilder.AppendLine("Starting downloading the temp file...");
            txtInfo.Text = stringBuilder.ToString();
            var directory = $"{System.IO.Path.GetTempPath()}ChemotionTemp";
            Directory.CreateDirectory(directory);
            var uri = new Uri(_url);
            var queryDictionary = HttpUtility.ParseQueryString(uri.Query);
            _token = queryDictionary["token"];
            _url = uri.GetLeftPart(UriPartial.Authority);
        }

        async Task EditProcess(string directory, string fileName)
        {
            string filePath = $"{directory}\\{fileName}";
            DateTime lmod = File.GetLastWriteTime(filePath);
            _ = Dispatcher.BeginInvoke(new Action(() =>
              {
                  stringBuilder.AppendLine("Waiting for the process to exit.");
                  txtInfo.Text = stringBuilder.ToString();
              }), DispatcherPriority.Background);

            using (Process myProcess = new Process())
            {
                myProcess.StartInfo.UseShellExecute = true;
                myProcess.StartInfo.FileName = filePath;
                myProcess.StartInfo.Arguments = filePath;
                myProcess.StartInfo.CreateNoWindow = false;
                myProcess.StartInfo.Verb = "";
                myProcess.Start();
                //stringBuilder.AppendLine("Waiting for the process to exit.");
                //txtInfo.Text = stringBuilder.ToString();
                //Console.ForegroundColor = ConsoleColor.Green;
                //Console.Write("Waiting for process to exit. Don't turn it off!!!");
                await myProcess.WaitForExitAsync();
                //Console.WriteLine("\n");
            }


            if (File.GetLastWriteTime(filePath) > lmod)
            {
                await DoneEditing(directory, fileName);
            }
            else
            {
                await DoneEditing(null, null);
            }

            stringBuilder.AppendLine("Done with uploading the file.");
            txtInfo.Text = stringBuilder.ToString();
            btnClose.IsEnabled = true;
            //Console.WriteLine("Finish upload the file. Press any key to exit!");
            //Console.ReadKey();
        }

        async Task<string> DownloadFile(string directory)
        {
            string fileName = String.Empty;
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync($"{_url}/api/v1/public/download?token={_token}");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                fileName = response.Content.Headers?.ContentDisposition?.FileName.Replace("\"", "");
                var filePath = $"{directory}\\{fileName}";
                using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }

            return fileName;
        }

        async Task DoneEditing(string directory, string fileName)
        {
            using (var client = new HttpClient())
            {
                using (var content =
                    new MultipartFormDataContent("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture)))
                {
                    if (!string.IsNullOrEmpty(directory) && !string.IsNullOrEmpty(fileName))
                    {
                        var filePath = $"{directory}\\{fileName}";
                        var bytes = File.ReadAllBytes(filePath);
                        content.Add(new StreamContent(new MemoryStream(bytes)), "file", fileName);
                    }

                    content.Add(new StringContent(_token), "token");
                    using var message = await client.PostAsync($"{_url}/api/v1/public/done", content);
                    var input = await message.Content.ReadAsStringAsync();
                }
            }
        }

        async Task RefreshToken()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
                using var message = await client.GetAsync($"{_url}/api/v1/public/refresh_token");
                var input = await message.Content.ReadAsStringAsync();
                var result = JObject.Parse(input);
                _token = result["token"].ToString();
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = false;
            await Main(Environment.GetCommandLineArgs());
        }
    }
}
