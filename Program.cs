using System.Net;
using FFMpegCore;
using FFMpegCore.Pipes;
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

    public static async Task HandleIncomingConnections(string picutre_folder, uint maxSize)
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
                dynamic body;
                try
                {
                    body = JsonConvert.DeserializeObject(bodyString)!;
                }
                catch
                {
                    Response.Fail(resp, "bad request");
                    resp.Close();
                    continue;
                }

                string data;
                string allowVideos;
                try
                {
                    data = ((string) body.data).Trim();
                    allowVideos = ((string) body.allowVideos).Trim();
                }
                catch
                {
                    data = "";
                    allowVideos = "";
                }

                if (data.Length > maxSize*1_000_000)
                {
                    Response.Fail(resp, "the file is too big");
                }
                else
                {
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
                            filePath = Path.Join(picutre_folder, filename);
                        } while (File.Exists(filePath));

                        byte[] imageBytes;
                        try
                        {
                            imageBytes = Convert.FromBase64String(data);
                        }
                        catch
                        {
                            Response.Fail(resp, "invalid data");
                            resp.Close();
                            continue;
                        }

                        try
                        {
                            if (!string.Equals(allowVideos, "true"))
                            {
                                throw new Exception();
                            }
                            await FFMpegArguments
                                .FromPipeInput(new StreamPipeSource(new MemoryStream(imageBytes)))
                                .OutputToFile(filePath, false, options => options
                                    .WithVideoCodec("vp9")
                                    .ForceFormat("webm")
                                    .WithFastStart())
                                .ProcessAsynchronously();
                            Response.Success(resp, "saved video", filename);
                        }
                        catch
                        {
                            try
                            {
                                using (Image image = Image.Load(imageBytes))
                                {
                                    // Remove metadata
                                    image.Metadata.ExifProfile = null;
                                    // Save image as webp
                                    await image.SaveAsWebpAsync(filePath, new WebpEncoder() {Quality = 100});
                                }

                                Response.Success(resp, "saved image", filename);
                            }
                            catch
                            {
                                Response.Fail(resp, "image or video format isn't supported");
                            }
                        }
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

                string filePath = Path.Join(picutre_folder, id);
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        Response.Success(resp, "file deleted", "");
                    }
                    catch
                    {
                        Response.Fail(resp, "couldn't delete file");
                    }
                }
                else
                {
                    Response.Success(resp, "file doesn't exist", "");
                }
            }
            // Get a file
            else if (req.HttpMethod == "POST" && req.Url?.AbsolutePath == "/getFile")
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


                string filePath = Path.Join(picutre_folder, id);
                if (File.Exists(filePath))
                {
                    try
                    {
                        Response.Success(resp, "accessed file",
                            Convert.ToBase64String(await File.ReadAllBytesAsync(filePath)));
                    }
                    catch
                    {
                        Response.Fail(resp, "couldn't access file");
                    }
                }
                else
                {
                    Response.Fail(resp, "file doesn't exist");
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
        string pictureFolder = config.GetValue<String>("pictureFolder") ?? "";
        uint maxSize = config.GetValue<uint>("maxSize");

        // Create a Http server and start listening for incoming connections
        string url = "http://*:" + config.GetValue<String>("Port") + "/";
        listener = new HttpListener();
        listener.Prefixes.Add(url);
        listener.Start();
        Console.WriteLine("Listening for connections on {0}", url);

        if (!Directory.Exists(pictureFolder))
        {
            // Error
            Console.Error.WriteLine("picture folder doesn't exist");
        }
        else
        {
            // Handle requests
            Task listenTask = HandleIncomingConnections(pictureFolder, maxSize);
            listenTask.GetAwaiter().GetResult();
        }

        // Close the listener
        listener.Close();
    }
}
