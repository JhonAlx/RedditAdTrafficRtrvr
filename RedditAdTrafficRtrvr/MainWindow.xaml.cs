using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using HtmlAgilityPack;
// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable ConditionIsAlwaysTrueOrFalse

namespace RedditAdTrafficRtrvr
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public CookieContainer Cookies;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void DestinationFolderPickerButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog { IsFolderPicker = true };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                DestinationFolderTextBox.Text = dialog.FileName;
        }

        /// <summary>
        /// Register events to MainWindow status textblock and Log file
        /// </summary>
        /// <param name="type">Log message type (INFO, WARNING, ERROR)</param>
        /// <param name="msg">Message to display</param>
        private void Log(string type, string msg)
        {
            var status = string.Empty;

            switch (type)
            {
                case "INFO":

                    status += $"[INFO] {DateTime.Now.ToString(CultureInfo.CurrentCulture)} - {msg}";

                    break;

                case "ERROR":

                    status += $"[ERROR] {DateTime.Now.ToString(CultureInfo.CurrentCulture)} - {msg}";

                    break;

                case "WARNING":

                    status += $"[WARNING] {DateTime.Now.ToString(CultureInfo.CurrentCulture)} - {msg}";

                    break;
            }

            StatusTextBlock.Text += status + Environment.NewLine;
            StatusTextBlock.ScrollToEnd();

            using (var writer = new StreamWriter("Log.txt", true))
            {
                writer.Write(status + Environment.NewLine);
            }
        }

        private async void DownloadTrafficDataButton_Click(object sender, RoutedEventArgs e)
        {
            Log("INFO", "Starting URL gathering process...");

            var doc = new HtmlDocument();
            var path = DestinationFolderTextBox.Text;
            var stop = false;

            var task = Task.Factory.StartNew(() =>
            {
                doc = GetHtmlDocument("https://www.reddit.com/promoted/");
            });

            await task;

            do
            {
                var trafficNodes = doc.DocumentNode.SelectNodes("//a[contains(text(), 'traffic')]");

                foreach (var traffic in trafficNodes)
                {
                    var url = new Uri(traffic.GetAttributeValue("href", ""));

                    Log("INFO", $"Downloading ad ID {url.Segments.Last().Replace("/", "")}");

                    task = Task.Factory.StartNew(() =>
                    {
                        var trafficRequest = WebRequest.Create(url) as HttpWebRequest;

                        if (trafficRequest != null)
                        {
                            trafficRequest.CookieContainer = Cookies;
                            trafficRequest.Method = "GET";
                            trafficRequest.Accept =
                                "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";

                            var trafficResponse = (HttpWebResponse)trafficRequest.GetResponse();

                            if (trafficResponse.StatusCode == HttpStatusCode.OK)
                                using (var s = trafficResponse.GetResponseStream())
                                {
                                    // ReSharper disable once AssignNullToNotNullAttribute
                                    using (var sr = new StreamReader(s, Encoding.GetEncoding(name: trafficResponse.CharacterSet)))
                                    {
                                        if (!Directory.Exists(Path.Combine(path, url.Segments.Last())))
                                        {
                                            Directory.CreateDirectory(Path.Combine(path,
                                                url.Segments.Last()));
                                            //using (var sw = new StreamWriter($"{Path.Combine(path, url.Segments.Last()}  {url.Segments.Last()}.html"))
                                            using (var sw = new StreamWriter(Path.Combine(path, url.Segments.Last().Replace("/", ""), $"{url.Segments.Last().Replace("/", "")}.html")))
                                            {
                                                sw.Write(sr.ReadToEnd());
                                            }
                                        }
                                    }
                                }
                        }
                    });

                    await task;
                }

                if (doc.DocumentNode.SelectSingleNode("//a[contains(text(), 'next')]") != null)
                {
                    Log("INFO", (doc.DocumentNode.SelectSingleNode("//a[contains(text(), 'next')]") != null).ToString());
                    Log("INFO", (stop).ToString());
                    Log("INFO", "Handling pagination");

                    var newUrl =
                        doc.DocumentNode.SelectSingleNode("//a[contains(text(), 'next')]").GetAttributeValue("href", "");

                    await Task.Factory.StartNew(() =>
                    {
                        doc = GetHtmlDocument(newUrl);
                    });
                }
                else
                    stop = true;
            }
            while (stop != true);

            Log("INFO", "Ended URL gathering process...");
        }

        /// <summary>
        /// Get a HtmlDocument from the supplied Url
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private HtmlDocument GetHtmlDocument(string url)
        {
            var doc = new HtmlDocument();

            var docRequest = WebRequest.Create(url) as HttpWebRequest;

            if (docRequest != null)
            {
                docRequest.CookieContainer = Cookies;
                docRequest.Method = "GET";
                docRequest.Accept =
                    "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";

                var docResponse = (HttpWebResponse)docRequest.GetResponse();

                if (docResponse.StatusCode == HttpStatusCode.OK)
                    using (var s = docResponse.GetResponseStream())
                    {
                        using (var sr = new StreamReader(s, Encoding.GetEncoding(name: docResponse.CharacterSet)))
                        {
                            doc.Load(sr);
                        }
                    }
            }

            return doc;
        }
    }
}
