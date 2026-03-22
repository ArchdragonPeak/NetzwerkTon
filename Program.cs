using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using NAudio.Wave;

class Program
{
  static void RunServer()
  {
    // get wav
    {
      using WaveFileReader reader = new("data/zphr.wav");
      using WaveFileWriter writer = new("data/zphr_out.wav", reader.WaveFormat);
      int bytesPerMs = reader.WaveFormat.AverageBytesPerSecond / 1000;
      int startPos = 0;
      startPos -= startPos % reader.WaveFormat.BlockAlign;
      int endPos = startPos + 200 * bytesPerMs;
      endPos -= endPos % reader.WaveFormat.BlockAlign;

      reader.Position = startPos;
      byte[] buffer = new byte[1024 - (1024 % reader.WaveFormat.BlockAlign)];
      Console.WriteLine($"Buffer size: {buffer.Length}, BlockAlign: {reader.WaveFormat.BlockAlign}, BytesPerMs: {bytesPerMs}, startPos: {startPos}, endPos: {endPos}");

      while (reader.Position < endPos)
      {
        int bytesRequired = endPos - (int)reader.Position;
        int bytesToRead = Math.Min(bytesRequired, buffer.Length);

        int bytesRead = reader.Read(buffer, 0, bytesToRead);
        if (bytesRead == 0)
          break;

        writer.Write(buffer, 0, bytesRead);
      }
    }
    // send wav
    byte[] file = File.ReadAllBytes("data/zphr_out.wav");
    
    Console.WriteLine("Sending file...");
    
    UdpClient server = new();
    IPAddress ip = IPAddress.Parse("127.0.0.1");
    IPEndPoint endPoint = new(ip, 25567);
    int sent = server.Send(file, file.Length, endPoint);
    Console.WriteLine($"Sent {sent} bytes.");
  }
  
  async static void RunClient()
  {
    using UdpClient client = new(25567, AddressFamily.InterNetwork);
    while(true)
    {
      try
      {
        IPEndPoint endPoint = new(IPAddress.Any, 0);
        byte[] received = client.Receive(ref endPoint);
        File.WriteAllBytes("data/received.wav", received);
        Console.WriteLine("Received file.");
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }
    }
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
        case "client":
          RunClient();
          break;
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