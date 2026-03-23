using System.Net;
using System.Net.Sockets;
using NAudio.Wave;

class Program
{

  static void RunServer()
  {
    // get wav
    {
      using WaveFileReader reader = new("data/zphr.wav");
      Console.WriteLine(
        $"Format: {reader.WaveFormat}" +
        $"Rate: {reader.WaveFormat.SampleRate}" +
        $"BitsPerSample: {reader.WaveFormat.BitsPerSample}" +
        $"Channels: {reader.WaveFormat.Channels}" +
        $"BlockAlign: {reader.WaveFormat.BlockAlign}" +
        $"Encoding: {reader.WaveFormat.Encoding}"
      );
      
      int frameCounter = 0;
      int startPos = 0;
      int frameSize = 200; //ms
      int lastPos = (int)reader.Length;

      int bytesPerFrame = reader.WaveFormat.AverageBytesPerSecond * frameSize / 1000;
      bytesPerFrame -= bytesPerFrame % reader.WaveFormat.BlockAlign;

      startPos -= startPos % reader.WaveFormat.BlockAlign;
      
      byte[] buffer = new byte[bytesPerFrame];

      // network
      using UdpClient server = new();
      IPAddress ip = IPAddress.Parse("192.168.178.50");
      IPEndPoint endPoint = new(ip, 25567);

      while (startPos < lastPos)
      {
          int endPos = startPos + bytesPerFrame;
          if (endPos > lastPos)
            endPos = lastPos;
          endPos -= endPos % reader.WaveFormat.BlockAlign;
          
          reader.Position = startPos;
          Console.WriteLine(
            $"startPos: {startPos} " +
            $"endPos: {endPos} " +
            $"size: {endPos - startPos}"
          );
        {

          while (reader.Position < endPos)
          {
            int bytesRequired = endPos - (int)reader.Position;
            int bytesToRead = Math.Min(bytesRequired, buffer.Length);

            int bytesRead = reader.Read(buffer, 0, bytesToRead);
            if (bytesRead == 0)
              break;

            int sent = server.Send(buffer, bytesRead, endPoint);
          }
        }
        Thread.Sleep(5);

        // next frame
        startPos = endPos;
        frameCounter++;
      }
      byte[] endPacket = System.Text.Encoding.ASCII.GetBytes("ENDE");
      server.Send(endPacket, endPacket.Length, endPoint);
    }
  }
  
  static void RunClient()
  {
    using UdpClient client = new(25567, AddressFamily.InterNetwork);
    byte[] endPacket = System.Text.Encoding.ASCII.GetBytes("ENDE");
    
    WaveFormat format = new(44100, 24, 2);
    using WaveFileWriter writer = new($"data/received/received.wav", format);
    int last = 0;
    
    while (true)
    {
      try
      {
        IPEndPoint endPoint = new(IPAddress.Any, 0);
        byte[] received = client.Receive(ref endPoint);
        if (received.SequenceEqual(endPacket)) break;
        writer.Write(received, 0, received.Length);
        last += received.Length;
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