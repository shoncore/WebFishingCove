// This reads and writes Godot's serialized format
// https://docs.godotengine.org/en/stable/tutorials/io/binary_serialization_api.html

using System.Text;

// alot of these need to be filled in lmao
enum GodotTypes
{
    nullValue = 0,
    boolValue = 1,
    intValue = 2,
    floatValue = 3,
    stringValue = 4,
    vector2Value = 5,
    rect2Value = 0,
    vector3Value = 7,
    transform2DValue = 0,
    planeValue = 9,
    quatValue = 10,
    aabbValue = 0,
    basisValue = 0,
    transformValue = 0,
    colorValue = 0,
    nodePathValue = 0,
    ridValue = 0, // ns
    objectValue = 0, //ns
    dictionaryValue = 18,
    arrayValue = 19
}

public class Vector3
{
    public float x;
    public float y;
    public float z;

    public Vector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
}

public class Vector2
{
    public float x;
    public float y;

    public Vector2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }
}

public class Quat
{
    public float x;
    public float y;
    public float z;
    public float w;

    public Quat(float x, float y, float z, float w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }
}

public class Plane
{
    public float x;
    public float y;
    public float z;
    public float distance;

    public Plane(float x, float y, float z, float distance)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.distance = distance;
    }
}

public class ReadError
{
    public ReadError()
    {

    }
}
public class GodotPacketDeserializer {

    public byte[] data;
    private BinaryReader reader;

    public GodotPacketDeserializer(byte[] data) 
    {
        this.data = data;
        reader = new BinaryReader(new MemoryStream(data), System.Text.Encoding.UTF8);
    }

    public Dictionary<string, object> readPacket()
    {
        int type = reader.ReadInt32();

        if (type != (int) GodotTypes.dictionaryValue)
        {
            //throw new Exception("Unable to decode a non-dictionary godot packet!");
        }

        Dictionary<string, object> dic = new Dictionary<string, object>();

        try
        {
            dic = readDictionary();
        } catch (Exception e) {
            Console.WriteLine("-- Error reading packet! --"); // incase we do have a error!
            Console.WriteLine(e.ToString()); // incase we do have a error!
        }

        return dic;
    }

    private Object readNext()
    {
        int type = reader.ReadInt32();

        //Console.WriteLine($"Type: {type}");

        switch(type)
        {
            case (int) GodotTypes.nullValue:
                return null;

            case (int) GodotTypes.dictionaryValue:
                return readDictionary();

            case (int)GodotTypes.arrayValue:
                return readArray();

            case (int) GodotTypes.stringValue:
                return readString();

            case (int)GodotTypes.intValue:
                return readInt(false);

            case 65538: // same as above but with the 64bit flag set!
                return readInt(true);

            case (int)GodotTypes.vector3Value:
                return readVector3();

            case (int)GodotTypes.quatValue:
                return readQuat();

            case (int)GodotTypes.boolValue:
                return readBool();

            case (int)GodotTypes.floatValue:
                return readFloat(false);

            case 65539: // same as above but with the 64bit flag set!
                return readFloat(true);

            case (int)GodotTypes.planeValue:
                return readPlane();

            case (int)GodotTypes.vector2Value:
                return readVector2();

            default:
                Console.WriteLine($"Unable to handel object of type: {type}");
                return new ReadError();
        }
    }

    private Plane readPlane()
    {
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        float z = reader.ReadSingle();
        float dist = reader.ReadSingle();

        return new Plane(x, y, z, dist);
    }

    private double readFloat(bool is64)
    {
        if (is64)
        {
            return reader.ReadDouble();
        } else
        {
            return reader.ReadSingle();
        }
    }

    private bool readBool()
    {
        return reader.ReadInt32() != 0;
    }

    private Quat readQuat()
    {
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        float z = reader.ReadSingle();
        float w = reader.ReadSingle();
        Quat quat = new Quat(x,y,z,w);

        return quat;
    }

    private Vector2 readVector2()
    {
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        Vector2 newVec = new(x, y);

        return newVec;
    }

    private Vector3 readVector3()
    {
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        float z = reader.ReadSingle();
        Vector3 newVec = new(x,y,z);

        return newVec;
    }

    private long readInt(bool is64)
    {
        if (is64)
        {
            return reader.ReadInt64();
        } else
        {
            int v = reader.ReadInt32();
            return v;
        }
    }

    private string readString()
    {
        int stringLength = reader.ReadInt32();
        char[] stringValue = reader.ReadChars(stringLength);

        // this feild is padded to 4 bytes
        if (4 - ((int)reader.BaseStream.Position % 4) != 4)
        {
            reader.ReadBytes(4 - ((int)reader.BaseStream.Position % 4));
        }

        return new string(stringValue);
    }

    private Dictionary<int, object> readArray()
    {
        Dictionary<int, object> array = new Dictionary<int, object>();

        int elementCount = reader.ReadInt32() & 0x7FFFFFFF;

        for (int i = 0; i < elementCount; i++)
        {
            object ins = readNext(); // read the next thing
            array[i] = ins;
        }

        return array;
    }

    private Dictionary<string, object> readDictionary()
    {

        Dictionary<string, object> dic = new Dictionary<string, object>();

        int elementCount = reader.ReadInt32() & 0x7FFFFFFF;

        for (int i = 0; i < elementCount; i++)
        {
            object keyValue = readNext();
            string key = "NullValue";

            if (keyValue == null || !(keyValue is String)) // if the value is not a string (bad read) break the loop.
            {
                Console.WriteLine("READ ERROR, KEY PROVIDED IS NOT A STRING!");
                break; //break from the loop to save the server!
            } else {
                key = keyValue.ToString(); // ITS A STRING I SWEAR TO GOD! PLEASEEE
            }

            object value = readNext();

            dic[key] = value;
        }

        return dic;
    }

}

public class GodotWriter
{

    public static byte[] WriteGodotPacket(Dictionary<string, object> packet)
    {
        MemoryStream stream = new MemoryStream();
        BinaryWriter bw = new BinaryWriter(stream);

        writeDictionary(packet, bw);

        return stream.ToArray();

    }

    private static void writeAny(object packet, BinaryWriter bw)
    {
        if (packet == null)
        {
            bw.Write(0);
        }
        else if(packet is Dictionary<string, object>)
        {
            writeDictionary((Dictionary<string, object>) packet, bw);
        } else if (packet is string)
        {
            writeString((string) packet, bw);
        } else if(packet is int)
        {
            writeInt((int) packet, bw);
        } else if( packet is long)
        {
            writeLong((long) packet, bw);
        } else if (packet is Single)
        {
            writeSingle((Single) packet, bw);
        } else if ( packet is Double)
        {
            writeDouble((Double) packet, bw);
        } else if (packet is bool)
        {
            writeBool((bool) packet, bw);
        } else if (packet is Dictionary<int, object>)
        {
            writeArray((Dictionary<int, object>) packet, bw);
        } else if (packet is Vector3)
        {
            writeVector3((Vector3) packet, bw);
        }
    }

    private static void writeVector3(Vector3 packet,  BinaryWriter bw)
    {
        bw.Write((int) 7); // write v3 header

        bw.Write((Single)packet.x);
        bw.Write((Single)packet.y);
        bw.Write((Single)packet.z);
    }

    private static void writeBool(bool packet,  BinaryWriter bw)
    {
        bw.Write((int) 2);
        bw.Write(packet ? 1 : 0);
    }

    private static void writeInt(int packet, BinaryWriter writer)
    {
        writer.Write((int)GodotTypes.intValue); // write the int value header!
        writer.Write((int)packet);
    }

    private static void writeLong(long packet, BinaryWriter writer)
    {
        writer.Write(65538); // write the int value header! this is the same as above but with the 64 bit header!
        writer.Write((long)packet);
    }

    private static void writeSingle(Single packet, BinaryWriter writer)
    {
        writer.Write((int) 3);
        writer.Write((Single) packet);
    }

    private static void writeDouble(Double packet, BinaryWriter writer)
    {
        writer.Write((int) 65539);// write the float value header! this is the same as above but with the 64 bit header!
        writer.Write((Double)packet);
    }

    private static void writeString(string packet, BinaryWriter bw)
    {
        bw.Write((int) 4); // remeber to write the string header!
        bw.Write((int) packet.Length);
        byte[] bytes = Encoding.UTF8.GetBytes(packet);
        // get the ammount to pad by!

        // Step 3: Write the actual bytes of the string
        bw.Write(bytes);

        // Step 4: Calculate padding needed to make the total length a multiple of 4
        int padding = (4 - (bytes.Length % 4)) % 4; // Calculate padding

        // Step 5: Write padding bytes (if needed)
        for (int i = 0; i < padding; i++)
        {
            bw.Write((byte)0); // Write padding as zero bytes
        }
    }

    private static void writeArray(Dictionary<int, object> packet, BinaryWriter writer)
    {
        // because we have a dic we need to write the correct byte info!
        writer.Write((int)18); // make sure these are 4 bits as that is what godot is exspecting!
        writer.Write((int)packet.Count);

        for (int i = 0; i < packet.Count; i++)
        {
            writeAny(packet[i], writer);
        }
    }

    private static void writeDictionary(Dictionary<string, object> packet, BinaryWriter writer)
    {
        // because we have a dic we need to write the correct byte info!
        writer.Write((int)18); // make sure these are 4 bits as that is what godot is exspecting!
        writer.Write((int) packet.Count);

        foreach (KeyValuePair<string, object> pair in packet)
        {
            writeAny(pair.Key, writer);
            writeAny(pair.Value, writer);
        }
    }
}