using System;
using System.Formats.Nrbf;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace LiveSplit.Drawing;

/// <summary>
/// Reads and writes the BinaryFormatter (NRBF) payloads that WinForms LiveSplit stores
/// in splits and layout files for <c>System.Drawing.Font</c> and <c>System.Drawing.Bitmap</c>.
/// Reading goes through the safe <see cref="NrbfDecoder"/>; writing emits the exact record
/// layout BinaryFormatter produces so the files stay readable by the Windows version.
/// </summary>
public static class LegacyBinarySerializer
{
    private const string SystemDrawingAssembly = "System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";

    public static Font ReadFont(byte[] data)
    {
        ClassRecord record = Decode(data);
        if (record == null || record.TypeName.FullName != "System.Drawing.Font")
        {
            return null;
        }

        string name = record.GetString("Name");
        float size = record.GetSingle("Size");
        var style = (FontStyle)ReadEnum(record, "Style");
        var unit = (GraphicsUnit)ReadEnum(record, "Unit", (int)GraphicsUnit.Point);

        return new Font(name, size, style, unit);
    }

    public static Image ReadImage(byte[] data)
    {
        ClassRecord record = Decode(data);
        if (record?.GetRawValue("Data") is SZArrayRecord<byte> bytes)
        {
            return new Image(bytes.GetArray());
        }

        return null;
    }

    public static byte[] WriteFont(Font font)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.UTF8);

        WriteHeader(w);
        WriteLibrary(w);

        w.Write((byte)RecordType.ClassWithMembersAndTypes);
        w.Write(1);
        w.Write("System.Drawing.Font");
        w.Write(4);
        w.Write("Name");
        w.Write("Size");
        w.Write("Style");
        w.Write("Unit");
        w.Write((byte)BinaryType.String);
        w.Write((byte)BinaryType.Primitive);
        w.Write((byte)BinaryType.Class);
        w.Write((byte)BinaryType.Class);
        w.Write((byte)PrimitiveKind.Single);
        w.Write("System.Drawing.FontStyle");
        w.Write(LibraryId);
        w.Write("System.Drawing.GraphicsUnit");
        w.Write(LibraryId);
        w.Write(LibraryId);

        w.Write((byte)RecordType.BinaryObjectString);
        w.Write(3);
        w.Write(font.Name);

        w.Write(font.Size);

        WriteEnumValue(w, -4, "System.Drawing.FontStyle", (int)font.Style);
        WriteEnumValue(w, -5, "System.Drawing.GraphicsUnit", (int)font.Unit);

        w.Write((byte)RecordType.MessageEnd);
        w.Flush();
        return ms.ToArray();
    }

    public static byte[] WriteImage(Image image)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.UTF8);

        WriteHeader(w);
        WriteLibrary(w);

        w.Write((byte)RecordType.ClassWithMembersAndTypes);
        w.Write(1);
        w.Write("System.Drawing.Bitmap");
        w.Write(1);
        w.Write("Data");
        w.Write((byte)BinaryType.PrimitiveArray);
        w.Write((byte)PrimitiveKind.Byte);
        w.Write(LibraryId);

        w.Write((byte)RecordType.MemberReference);
        w.Write(3);

        w.Write((byte)RecordType.ArraySinglePrimitive);
        w.Write(3);
        w.Write(image.Data.Length);
        w.Write((byte)PrimitiveKind.Byte);
        w.Write(image.Data);

        w.Write((byte)RecordType.MessageEnd);
        w.Flush();
        return ms.ToArray();
    }

    private const int LibraryId = 2;

    private static ClassRecord Decode(byte[] data)
    {
        try
        {
            using var ms = new MemoryStream(data, false);
            return NrbfDecoder.Decode(ms) as ClassRecord;
        }
        catch (Exception ex) when (ex is SerializationException or NotSupportedException or EndOfStreamException or FormatException)
        {
            Options.Log.Error(ex);
            return null;
        }
    }

    private static int ReadEnum(ClassRecord record, string memberName, int defaultValue = 0)
    {
        if (!record.HasMember(memberName))
        {
            return defaultValue;
        }

        return record.GetRawValue(memberName) switch
        {
            ClassRecord boxed => boxed.GetInt32("value__"),
            PrimitiveTypeRecord primitive => Convert.ToInt32(primitive.Value),
            int value => value,
            _ => defaultValue
        };
    }

    private static void WriteHeader(BinaryWriter w)
    {
        w.Write((byte)RecordType.SerializedStreamHeader);
        w.Write(1);
        w.Write(-1);
        w.Write(1);
        w.Write(0);
    }

    private static void WriteLibrary(BinaryWriter w)
    {
        w.Write((byte)RecordType.BinaryLibrary);
        w.Write(LibraryId);
        w.Write(SystemDrawingAssembly);
    }

    private static void WriteEnumValue(BinaryWriter w, int objectId, string typeName, int value)
    {
        w.Write((byte)RecordType.ClassWithMembersAndTypes);
        w.Write(objectId);
        w.Write(typeName);
        w.Write(1);
        w.Write("value__");
        w.Write((byte)BinaryType.Primitive);
        w.Write((byte)PrimitiveKind.Int32);
        w.Write(LibraryId);
        w.Write(value);
    }

    private enum RecordType : byte
    {
        SerializedStreamHeader = 0,
        ClassWithMembersAndTypes = 5,
        BinaryObjectString = 6,
        MemberReference = 9,
        MessageEnd = 11,
        BinaryLibrary = 12,
        ArraySinglePrimitive = 15
    }

    private enum BinaryType : byte
    {
        Primitive = 0,
        String = 1,
        Class = 4,
        PrimitiveArray = 7
    }

    private enum PrimitiveKind : byte
    {
        Byte = 2,
        Int32 = 8,
        Single = 11
    }
}
