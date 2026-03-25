using System.Net;
using System.Net.Sockets;
using NAudio.Wave;

/* AudioPacket layout
 * Start-Packet
 * Sends information about audio stream needed to create wav/opus
 * Bytes | Name
 * ---Header--- 1 Byte
 * 1     | Type
 * ---Data---
 * Dyn   | Data
 
 * Data-Packet
 * Bytes | Name
 * ---Header--- 10 Byte
 * 1     | Type
 * 1     | Codec
 * 4     | Sequence Number
 * 4     | Body Length
 * ---Data---
 * Dyn   | Data
 
 * END-Packet
 * Bytes | Name
 * ---Header--- 1 Byte
 * 1     | Type
 * ---Data---
 * Dyn   | Data
 */


abstract class AudioPacket
{
  public byte[] Header { get; protected set; }
  public byte[] Data { get; protected set;}

  public AudioPacket(byte[] header, byte[] data)
  {
    this.Header = header;
    this.Data = data;
  }
  public abstract byte[] GetHeader();
}

class StartPacket : AudioPacket
{
  private StartPacket(byte[] data) : base(new byte[] { 0x01 }, data) { }

  public static StartPacket Create()
  {
    return new StartPacket(new byte[0]);
  }
  public override byte[] GetHeader()
  {
    return new byte[] { 0x01 };
  }
}
class DataPacket : AudioPacket
{
  //                            type           codec          sequence no    body length 
  public const int HeaderSize = sizeof(byte) + sizeof(byte) + sizeof(uint) + sizeof(uint);
  
  public byte Type => Header[0];
  public byte Codec => Header[1];
  public uint SequenceNumber => BitConverter.ToUInt32(Header, 2);
  public uint BodyLength => BitConverter.ToUInt32(Header, 6);
  
  private DataPacket(byte[] header, byte[] data) : base(header, data) { }

  public static DataPacket Create(byte codec, uint sequenceNumber, uint bodyLength, byte[] data)
  {
    byte[] header = new byte[HeaderSize];

    using MemoryStream ms = new(header);
    using BinaryWriter w = new(ms);

    w.Write((byte)0x02); // type
    w.Write(codec);
    w.Write(sequenceNumber);
    w.Write(bodyLength);

    return new(header, data);
  }
  public static DataPacket Create(byte[] received)
  {
    byte[] header = received[..HeaderSize];
    byte[] data = received[HeaderSize..];

    return new(header, data);
  }

  public override byte[] GetHeader()
  {
    return Header;
  }
  public byte[] GetData()
  {
    return Data;
  }
  
}

class EndPacket : AudioPacket
{
  private EndPacket(byte[] data) : base(new byte[] { 0x03 }, data) { }

  public static EndPacket Create()
  {
    return new(new byte[0]);
  }
  
  public override byte[] GetHeader()
  {
    return new byte[] { 0x03 };
  }
}

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
      
      StartPacket startPacket = StartPacket.Create();
      server.Send(startPacket.GetHeader(), startPacket.GetHeader().Length, endPoint);
      
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
            DataPacket dataPacket = DataPacket.Create(
              0x0,
              (uint)frameCounter,
              (uint)bytesRead,
              buffer[..bytesRead]
            );

            byte[] data = dataPacket.GetHeader().Concat(dataPacket.GetData()).ToArray();
            
            int sent = server.Send(data, data.Length, endPoint);
          }
        }
        Thread.Sleep(5);

        // next frame
        startPos = endPos;
        frameCounter++;
      }
      byte[] endPacket = [0x03];
      server.Send(endPacket, endPacket.Length, endPoint);
    }
  }
  
  static void RunClient()
  {
    using UdpClient client = new(25567, AddressFamily.InterNetwork);
    WaveFormat format = new(44100, 24, 2);
    AudioPacket packet = null;
    using WaveFileWriter writer = new($"data/received/received.wav", format);
    int last = 0;
    
    while (true)
    {
      try
      {
        IPEndPoint endPoint = new(IPAddress.Any, 0);
        byte[] received = client.Receive(ref endPoint);
        // check header + body of received packet
        switch (received[0])
        {
          case 0x01: // start
            Console.WriteLine("Received start packet.");
            break;
          case 0x02: // data
            packet = DataPacket.Create(received);
            break;
          case 0x03: // end
            Console.WriteLine("Received end packet.");
            return;
          default:
            Console.WriteLine("Unknown packet type.{}", received[0]);
            return;
        }
        
        //if (received.SequenceEqual(endPacket)) break;
        
        if(packet is DataPacket dataPacket)
        {
          writer.Write(packet.Data, 0, packet.Data.Length);
          last += packet.Data.Length;
          
          Console.WriteLine($"Recieved DataPacket Seq: {dataPacket.SequenceNumber}");
        }
        
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
