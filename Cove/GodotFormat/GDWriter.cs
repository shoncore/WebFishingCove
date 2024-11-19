using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cove.GodotFormat
{
  /// <summary>
  /// Utility class for writing Godot's serialized data format.
  /// </summary>
  public static class GodotWriter
  {
    /// <summary>
    /// Converts a dictionary into a binary Godot packet.
    /// </summary>
    /// <param name="packet">The dictionary representing the packet.</param>
    /// <returns>The binary representation of the packet.</returns>
    public static byte[] WriteGodotPacket(Dictionary<string, object> packet)
    {
      using var stream = new MemoryStream();
      using var writer = new BinaryWriter(stream);
      WriteDictionary(packet, writer);
      return stream.ToArray();
    }

    /// <summary>
    /// Writes any supported type to the binary writer.
    /// </summary>
    private static void WriteAny(object? value, BinaryWriter writer)
    {
      switch (value)
      {
        case null:
          writer.Write(0);
          break;
        case Dictionary<string, object> dict:
          WriteDictionary(dict, writer);
          break;
        case string str:
          WriteString(str, writer);
          break;
        case int i:
          WriteInt(i, writer);
          break;
        case long l:
          WriteLong(l, writer);
          break;
        case float f:
          WriteSingle(f, writer);
          break;
        case double d:
          WriteDouble(d, writer);
          break;
        case bool b:
          WriteBool(b, writer);
          break;
        case Dictionary<int, object> array:
          WriteArray(array, writer);
          break;
        case Vector3 vector3:
          WriteVector3(vector3, writer);
          break;
        default:
          throw new InvalidOperationException($"Unsupported value type: {value.GetType().Name}");
      }
    }

    private static void WriteVector3(Vector3 vector, BinaryWriter writer)
    {
      writer.Write((int)GodotTypes.Vector3Value); // Header for Vector3
      writer.Write(vector.X);
      writer.Write(vector.Y);
      writer.Write(vector.Z);
    }

    private static void WriteBool(bool value, BinaryWriter writer)
    {
      writer.Write((int)GodotTypes.BoolValue);
      writer.Write(value ? 1 : 0);
    }

    private static void WriteInt(int value, BinaryWriter writer)
    {
      writer.Write((int)GodotTypes.IntValue);
      writer.Write(value);
    }

    private static void WriteLong(long value, BinaryWriter writer)
    {
      writer.Write((int)65538); // Int64 header
      writer.Write(value);
    }

    private static void WriteSingle(float value, BinaryWriter writer)
    {
      writer.Write((int)GodotTypes.FloatValue);
      writer.Write(value);
    }

    private static void WriteDouble(double value, BinaryWriter writer)
    {
      writer.Write((int)65539); // Float64 header
      writer.Write(value);
    }

    private static void WriteString(string value, BinaryWriter writer)
    {
      writer.Write((int)GodotTypes.StringValue); // Header for string
      byte[] bytes = Encoding.UTF8.GetBytes(value);
      writer.Write(bytes.Length);
      writer.Write(bytes);

      // Align to 4 bytes
      int padding = (4 - (bytes.Length % 4)) % 4;
      writer.Write(new byte[padding]);
    }

    private static void WriteArray(Dictionary<int, object> array, BinaryWriter writer)
    {
      writer.Write((int)GodotTypes.ArrayValue);
      writer.Write(array.Count);

      foreach (var value in array.Values)
      {
        WriteAny(value, writer);
      }
    }

    private static void WriteDictionary(Dictionary<string, object> dictionary, BinaryWriter writer)
    {
      writer.Write((int)GodotTypes.DictionaryValue);
      writer.Write(dictionary.Count);

      foreach (var pair in dictionary)
      {
        WriteAny(pair.Key, writer);
        WriteAny(pair.Value, writer);
      }
    }
  }
}
