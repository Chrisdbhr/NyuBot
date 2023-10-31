using System;
using System.Threading.Tasks;

namespace NyuBot {
    class Program {
        public static readonly Version VERSION = new ("6.0.0");
        public static Task Main(string[] args) => Startup.RunAsync(args);
    }
}