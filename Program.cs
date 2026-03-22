using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;


class Program
{
  static void RunServer()
  {

  }
  
  static void Main(string[] args)
  {
    if (args.Length > 0)
    {
      switch (args[0])
      {
        case "server":
          RunServer();
          break;
        // add client
        default:
          Console.WriteLine($"Unknown argument: {args[0]}");
          break;
      }
    }
    else
    {
      Console.WriteLine("No arguments provided.");
    }
  }
}