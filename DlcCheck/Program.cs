using System;
using System.IO;

namespace DlcCheck
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length > 3 || args.Length < 2)
            {
                PrintUsage("Invalid amount of parameters.");
                return 1;
            }

            var mbdPath = args[0];
            var gameRoot = args[1];
            string output = (args.Length > 2) ? args[2] : null;

            if (!File.Exists(mbdPath))
            {
                Console.Error.WriteLine("mbd file does not exist.");
                return 2;
            }
            if (!Directory.Exists(gameRoot))
            {
                Console.Error.WriteLine("Game root directory does not exist.");
                return 3;
            }

            var c = new DlcChecker();
            c.Check(mbdPath, gameRoot, output);

            return 0;
        }

        static void PrintUsage(string err)
        {
            Console.WriteLine(err);
            Console.WriteLine("Usage:");
            Console.WriteLine("DlcCheck.exe mbd_path game_root_path [output_path]");
        }
    }
}
