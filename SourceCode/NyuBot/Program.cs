using System;
using System.Threading.Tasks;

namespace NyuBot
{
    public class Program
    {
        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            Console.WriteLine("Hello World!");
            await Task.Delay(-1);
        }
    }
}
