using System.Net;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using VersineResponse;

namespace CDN;

class HttpServer
{
    public static HttpListener? listener;

    private static Random random = new Random();

    public static string RandomString()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 25)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    public static async Task HandleIncomingConnections(string picutre_folder)
    {
        while (true)
        {
            HttpListenerContext ctx = await listener?.GetContextAsync()!;

            HttpListenerRequest req = ctx.Request;
            HttpListenerResponse resp = ctx.Response;

            Console.WriteLine(req.HttpMethod);
            Console.WriteLine(req.Url?.ToString());
            Console.WriteLine(req.UserHostName);
            Console.WriteLine(req.UserAgent);

            // Add a file
            if (req.HttpMethod == "POST" && req.Url?.AbsolutePath == "/addFile")
            {
                StreamReader reader = new StreamReader(req.InputStream);
                string bodyString = await reader.ReadToEndAsync();
                dynamic body = JsonConvert.DeserializeObject(bodyString)!;

                string data;
                try
                {
                    data = ((string) body.data).Trim();
                }
                catch
                {
                    data = "";
                }

                if (string.Equals(data, ""))
                {
                    Response.Fail(resp, "invalid body");
                }
                else
                {
                    string filename = "";
                    string filePath = "";
                    do
                    {
                        filename = RandomString();
                        filePath = Path.Join(picutre_folder, filename + ".webp");
                    } while (File.Exists(filePath));
                    
                    try
                    {
                        byte[] imageBytes = Convert.FromBase64String(data);
                     
                        using (Image image = Image.Load(imageBytes))
                        {
                            // Remove metadata
                            image.Metadata.ExifProfile = null;
                            // Save image as webp
                            await image.SaveAsWebpAsync(filePath, new WebpEncoder() {Quality = 100});
                        }
                        Response.Success(resp, "saved file", filename);
                    }
                    catch
                    {
                        Response.Fail(resp, "image format isn't supported");
                    }
                }
            }

            // Delete a file
            else if (req.HttpMethod == "POST" && req.Url?.AbsolutePath == "/deleteFile")
            {
                StreamReader reader = new StreamReader(req.InputStream);
                string bodyString = await reader.ReadToEndAsync();
                dynamic body = JsonConvert.DeserializeObject(bodyString)!;

                string id;
                try
                {
                    id = ((string) body.id).Trim();
                }
                catch
                {
                    id = "";
                }

                
                string filePath = Path.Join(picutre_folder, id + ".webp");
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        Response.Success(resp, "file deleted", "");
                    }
                    catch
                    {
                        Response.Fail(resp, "file doesn't exist");
                    }
                }
                else
                {
                    Response.Success(resp, "file doesn't exist", "");
                }
            }
            else
            {
                Response.Fail(resp, "404");
            }

            // close response
            resp.Close();
        }
    }

    public static void Main(string[] args)
    {
        // Load config file
        IConfigurationRoot config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true)
            .AddEnvironmentVariables()
            .Build();

        // Get values from config file
        string picture_folder = config.GetValue<String>("picture_folder") ?? "";

        // Create a Http server and start listening for incoming connections
        string url = "http://*:" + config.GetValue<String>("Port") + "/";
        listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();
        Console.WriteLine("Listening for connections on {0}", url);

        if (!Directory.Exists(picture_folder))
        {
            // Error
            Console.Error.WriteLine("picture folder doesn't exist");
        }
        else
        {
            // Handle requests
            Task listenTask = HandleIncomingConnections(picture_folder);
            listenTask.GetAwaiter().GetResult();
        }

        // Close the listener
        listener.Close();
    }
}