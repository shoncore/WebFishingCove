enum GodotTypes
{
    nullValue = 0,
    boolValue = 1,
    intValue = 2,
    floatValue = 3,
    stringValue = 4,
    vector2Value = 0,
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
    arrayValue = 0
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
            throw new Exception("Unable to decode a non-dictionary godot packet!");
        }

        Dictionary<string, object> dic = readDictionary();

        //Console.WriteLine("Returning read packet!");
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
                return null;

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

            default:
                Console.WriteLine($"Unable to handel object of type: {type}");
                return null;
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

    private Dictionary<string, object> readDictionary()
    {

        Dictionary<string, object> dic = new Dictionary<string, object>();

        // read how many elements there are in the dic
        int elementCount = reader.ReadInt32() & 0x7FFFFFFF;

        //Console.WriteLine($"Dictionary has {elementCount} elements!");

        for (int i = 0; i < elementCount; i++)
        {

            string key = (string) readNext();
            object value = readNext();

            if (key == null)
            {
                continue;
            }

            dic[key] = value;
        }

        return dic;
    }

}