using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cove.Server
{
    public partial class CoveServer
    {
        // purely for debug
        void printStringDict(Dictionary<string, object> obj, string sub = "")
        {
            foreach (var kvp in obj)
            {
                if (kvp.Value is Dictionary<string, object>)
                {
                    printStringDict((Dictionary<string, object>)kvp.Value, sub + "." + kvp.Key);
                }
                else if (kvp.Value is Dictionary<int, object>)
                {
                    printArray((Dictionary<int, object>)kvp.Value, sub + "." + kvp.Key);
                }
                {
                    Console.WriteLine($"{sub} {kvp.Key}: {kvp.Value}");
                }
            }
        }
        void printArray(Dictionary<int, object> obj, string sub = "")
        {
            foreach (var kvp in obj)
            {
                if (kvp.Value is Dictionary<string, object>)
                {
                    printStringDict((Dictionary<string, object>)kvp.Value, sub + "." + kvp.Key);
                }
                else if (kvp.Value is Dictionary<int, object>)
                {
                    printArray((Dictionary<int, object>)kvp.Value, sub + "." + kvp.Key);
                }
                {
                    Console.WriteLine($"{sub} {kvp.Key}: {kvp.Value}");
                }
            }
        }
    }
}
