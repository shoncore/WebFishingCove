using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cove.GodotFormat;

namespace Cove.GodotFormat
{
  /// <summary>
  /// A utility class for reading Godot's serialized data format.
  /// </summary>
  public class GodotReader
  {
    private readonly BinaryReader _reader;

    /// <summary>
    /// Initializes a new instance of the <see cref="GodotReader"/> class.
    /// </summary>
    /// <param name="data">The binary data to read.</param>
    public GodotReader(byte[] data)
    {
      _reader = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
    }

    /// <summary>
    /// Reads a Godot packet and returns it as a dictionary.
    /// </summary>
    /// <returns>A dictionary representation of the packet.</returns>
    public Dictionary<string, object> ReadPacket()
    {
      try
      {
        int type = _reader.ReadInt32();
        if (type != (int)GodotTypes.DictionaryValue)
        {
          throw new InvalidOperationException("The data is not a dictionary.");
        }

        return ReadDictionary();
      }
      catch (Exception ex)
      {
        Console.WriteLine("Error reading packet:");
        Console.WriteLine(ex);
        return new Dictionary<string, object>();
      }
    }

    private object ReadNext()
    {
      int header = _reader.ReadInt32();
      int type = header & 0xFFFF;
      int flags = header >> 16;

      return type switch
      {
          (int)GodotTypes.NullValue => null!,
          (int)GodotTypes.DictionaryValue => ReadDictionary(),
          (int)GodotTypes.ArrayValue => ReadArray(),
          (int)GodotTypes.StringValue => ReadString(),
          (int)GodotTypes.IntValue => ReadInt(flags),
          (int)GodotTypes.Vector3Value => ReadVector3(),
          (int)GodotTypes.QuatValue => ReadQuat(),
          (int)GodotTypes.BoolValue => ReadBool(),
          (int)GodotTypes.FloatValue => ReadFloat(flags),
          (int)GodotTypes.PlaneValue => ReadPlane(),
          (int)GodotTypes.Vector2Value => ReadVector2(),
          _ => new ReadError($"Unsupported object type: {type}")
      };
    }

    private Plane ReadPlane()
    {
      return new Plane(
          _reader.ReadSingle(),
          _reader.ReadSingle(),
          _reader.ReadSingle(),
          _reader.ReadSingle()
      );
    }

    private double ReadFloat(int flags)
    {
      return (flags & 1) == 1 ? _reader.ReadDouble() : _reader.ReadSingle();
    }

    private bool ReadBool()
    {
      return _reader.ReadInt32() != 0;
    }

    private Quat ReadQuat()
    {
      return new Quat(
          _reader.ReadSingle(),
          _reader.ReadSingle(),
          _reader.ReadSingle(),
          _reader.ReadSingle()
      );
    }

    private Vector2 ReadVector2()
    {
      return new Vector2(
          _reader.ReadSingle(),
          _reader.ReadSingle()
      );
    }

    private Vector3 ReadVector3()
    {
      return new Vector3(
          _reader.ReadSingle(),
          _reader.ReadSingle(),
          _reader.ReadSingle()
      );
    }

    private long ReadInt(int flags)
    {
      return (flags & 1) == 1 ? _reader.ReadInt64() : _reader.ReadInt32();
    }

    private string ReadString()
    {
      int length = _reader.ReadInt32();
      char[] chars = _reader.ReadChars(length);

      // Padding to align to 4 bytes
      int padding = (4 - (int)(_reader.BaseStream.Position % 4)) % 4;
      _reader.ReadBytes(padding);

      return new string(chars);
    }

    private Dictionary<int, object> ReadArray()
    {
      int count = _reader.ReadInt32() & 0x7FFFFFFF;
      var array = new Dictionary<int, object>(count);

      for (int i = 0; i < count; i++)
      {
        array[i] = ReadNext();
      }

      return array;
    }

    private Dictionary<string, object> ReadDictionary()
    {
      int count = _reader.ReadInt32() & 0x7FFFFFFF;
      var dictionary = new Dictionary<string, object>(count);

      for (int i = 0; i < count; i++)
      {
        object keyObj = ReadNext();
        if (keyObj is not string key)
        {
          Console.WriteLine("Error: Dictionary key is not a string.");
          break;
        }

        object value = ReadNext();
        dictionary[key] = value;
      }

      return dictionary;
    }
  }

  /// <summary>
  /// Represents an error in reading an object from the Godot data format.
  /// </summary>
  public class ReadError
  {
    public string Message { get; }

    public ReadError(string message)
    {
      Message = message;
    }

    public override string ToString() => $"ReadError: {Message}";
  }
}
