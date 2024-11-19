namespace Cove.Server
{
    public partial class CoveServer
    {
        /// <summary>
        /// Recursively prints the contents of a dictionary with string keys for debugging purposes.
        /// </summary>
        /// <param name="obj">The dictionary to print.</param>
        /// <param name="prefix">The prefix for nested keys.</param>
        private void PrintStringDictionary(Dictionary<string, object> obj, string prefix = "")
        {
            foreach (var (key, value) in obj)
            {
                var fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";

                switch (value)
                {
                    case Dictionary<string, object> stringDict:
                        PrintStringDictionary(stringDict, fullKey);
                        break;

                    case Dictionary<int, object> intDict:
                        PrintArray(intDict, fullKey);
                        break;

                    default:
                        Console.WriteLine($"{fullKey}: {value}");
                        break;
                }
            }
        }

        /// <summary>
        /// Recursively prints the contents of a dictionary with integer keys for debugging purposes.
        /// </summary>
        /// <param name="obj">The dictionary to print.</param>
        /// <param name="prefix">The prefix for nested keys.</param>
        private void PrintArray(Dictionary<int, object> obj, string prefix = "")
        {
            foreach (var (key, value) in obj)
            {
                var fullKey = string.IsNullOrEmpty(prefix) ? key.ToString() : $"{prefix}.{key}";

                switch (value)
                {
                    case Dictionary<string, object> stringDict:
                        PrintStringDictionary(stringDict, fullKey);
                        break;

                    case Dictionary<int, object> intDict:
                        PrintArray(intDict, fullKey);
                        break;

                    default:
                        Console.WriteLine($"{fullKey}: {value}");
                        break;
                }
            }
        }
    }
}
