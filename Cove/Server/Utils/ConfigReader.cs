using System.Reflection;

namespace Cove.Server.Utils
{
  /// <summary>
  /// Utility class for reading configuration files.
  /// </summary>
  public class ConfigReader
  {
    /// <summary>
    /// Reads configuration from a file. If the file does not exist, attempts to read it from embedded resources.
    /// </summary>
    /// <param name="fileName">The name of the configuration file.</param>
    /// <returns>A dictionary containing configuration key-value pairs.</returns>
    public static Dictionary<string, string> ReadConfig(string fileName)
    {
      string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

      if (File.Exists(filePath))
      {
        string fileContent = File.ReadAllText(filePath);
        return ParseConfig(fileContent);
      }
      else
      {
        string resourceName = Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty;
        if (string.IsNullOrEmpty(resourceName))
          throw new Exception("Assembly name is null");

        string embeddedResourceName = $"{resourceName}.{fileName}";

        bool resourceExists = Assembly.GetExecutingAssembly()
            .GetManifestResourceNames()
            .Any(name => name.Equals(embeddedResourceName, StringComparison.OrdinalIgnoreCase));

        if (resourceExists)
        {
          string fileContent = ReadFromAssembly(embeddedResourceName);
          Console.ForegroundColor = ConsoleColor.Green;
          Console.WriteLine($"Using default {fileName}, it has been added to the root directory for editing!");
          Console.ResetColor();

          File.WriteAllText(filePath, fileContent);

          return ParseConfig(fileContent);
        }
        else
        {
          throw new Exception($"Cannot find config file '{fileName}' to read.");
        }
      }
    }

    /// <summary>
    /// Reads an embedded resource from the assembly.
    /// </summary>
    /// <param name="resourceName">The name of the embedded resource.</param>
    /// <returns>The content of the resource as a string.</returns>
    private static string ReadFromAssembly(string resourceName)
    {
      using Stream stream = Assembly.GetExecutingAssembly()
          .GetManifestResourceStream(resourceName)
          ?? throw new Exception($"Cannot find embedded resource '{resourceName}' in assembly.");

      using StreamReader reader = new(stream);
      return reader.ReadToEnd();
    }

    /// <summary>
    /// Parses the content of a configuration file into a dictionary.
    /// </summary>
    /// <param name="fileContent">The content of the configuration file.</param>
    /// <returns>A dictionary containing configuration key-value pairs.</returns>
    public static Dictionary<string, string> ParseConfig(string fileContent)
    {
      var configValues = new Dictionary<string, string>();
      var fileLines = fileContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

      foreach (string line in fileLines)
      {
        string trimmedLine = line.Trim();

        // Skip empty lines and comments
        if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
          continue;

        int equalsIndex = trimmedLine.IndexOf('=');
        if (equalsIndex > 0)
        {
          string key = trimmedLine.Substring(0, equalsIndex).Trim();
          string value = trimmedLine.Substring(equalsIndex + 1).Trim();
          configValues[key] = value;
        }
        else
        {
          // Handle lines without an '=' character
          Console.WriteLine($"Warning: Invalid config line '{trimmedLine}'");
        }
      }

      return configValues;
    }
  }
}
