using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class Program
{
    private static readonly string ImageUrl = "https://www.th-deg.de/static/images/webcam.jpg";
    private static readonly string ConfigFilePath = "config.json";
    private static readonly int DefaultInterval = 5; // Default interval in seconds
    private static ILogger<Program>? _logger;
    private static Config? _config;

    public static async Task Main(string[] args)
    {
        var serviceProvider = new ServiceCollection()
            .AddLogging(configure => configure.AddConsole())
            .BuildServiceProvider();

        _logger = serviceProvider.GetService<ILoggerFactory>()
            .CreateLogger<Program>();

        _logger.LogInformation("Application started.");

        LoadConfig();

        while (true)
        {
            try
            {
                await ProcessImage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the image.");
            }

            await Task.Delay(_config.Interval * 1000);
        }
    }

    private static void LoadConfig()
    {
        if (!File.Exists(ConfigFilePath))
        {
            _config = new Config { LastHash = null, Interval = DefaultInterval, ImageUrl = ImageUrl }; // Use default ImageUrl if config does not exist
            SaveConfig();
            _logger.LogInformation("Config file not found. Created default config.");
        }
        else
        {
            var configJson = File.ReadAllText(ConfigFilePath);
            _config = JsonConvert.DeserializeObject<Config>(configJson);

            if (string.IsNullOrEmpty(_config.ImageUrl))
            {
                _config.ImageUrl = ImageUrl; // Set to default if not specified in config
                SaveConfig();
                _logger.LogInformation("ImageUrl not found or invalid in config. Set to default ImageUrl.");
            }

            if (_config.Interval <= 0)
            {
                _config.Interval = DefaultInterval;
                SaveConfig();
                _logger.LogInformation("Interval not found or invalid in config. Set to default interval.");
            }

            _logger.LogInformation($"Config loaded. Interval: {_config.Interval} seconds, ImageUrl: {_config.ImageUrl}");
        }
    }

    private static void SaveConfig()
    {
        var configJson = JsonConvert.SerializeObject(_config, Formatting.Indented);
        File.WriteAllText(ConfigFilePath, configJson);
        _logger.LogInformation("Config saved.");
    }

    private static async Task ProcessImage()
    {
        HttpClientHandler clientHandler = new HttpClientHandler();
        clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };

        using (HttpClient client = new HttpClient(clientHandler))
        {
            _logger.LogInformation("Downloading image...");
            var imageBytes = await client.GetByteArrayAsync(_config.ImageUrl); // Use ImageUrl from _config
            var currentHash = ComputeHash(imageBytes);

            string lastHash = _config.LastHash;

            if (currentHash != lastHash)
            {
                _logger.LogInformation("Image has changed. Saving new image...");
                SaveImage(imageBytes);
                _config.LastHash = currentHash;
                SaveConfig();
            }
            else
            {
                _logger.LogInformation("Image has not changed.");
            }
        }
    }

    private static string ComputeHash(byte[] imageBytes)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(imageBytes);
            string hash = Convert.ToBase64String(hashBytes);
            _logger.LogInformation($"Computed hash: {hash}");
            return hash;
        }
    }

    private static void SaveImage(byte[] imageBytes)
    {
        string directoryPath = Path.Combine("images",
                                            DateTime.Now.Year.ToString() + "." + 
                                            DateTime.Now.Month.ToString("D2") + "." + 
                                            DateTime.Now.Day.ToString("D2"));

        Directory.CreateDirectory(directoryPath);

        string fileName = $"{DateTime.Now:yyyy.MM.dd_HH-mm-ss}.jpg";
        string filePath = Path.Combine(directoryPath, fileName);

        File.WriteAllBytes(filePath, imageBytes);
        _logger.LogInformation($"Saved image to {filePath}");
    }

    private class Config
    {
        public string? LastHash { get; set; }
        public int Interval { get; set; }  // Interval in seconds
        public string? ImageUrl { get; set; } // Add this line
    }

}
