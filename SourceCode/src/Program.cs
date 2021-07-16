using System;
using System.Threading.Tasks;

namespace NyuBot {
    class Program {
        public const string VERSION = "v1.5"; 
        public static Task Main(string[] args) => Startup.RunAsync(args);
    }
}
