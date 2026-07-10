#if LINUX
namespace R3E.Tray.Linux;

using System.Text;
using Tmds.DBus.Protocol;

/// <summary>
/// D-Bus method handler for the com.canonical.dbusmenu interface.
/// Provides a simple context menu with a single "Quit" item at /MenuBar.
/// </summary>
internal sealed class DbusTrayMenuHandler : IPathMethodHandler
{
    private const int RootId = 0;
    private const int QuitId = 1;

    private static readonly ReadOnlyMemory<byte> DbusmenuInterfaceXml = Encoding.UTF8.GetBytes(
        """
        <interface name="com.canonical.dbusmenu">
          <method name="GetLayout">
            <arg direction="in" type="i" name="parentId"/>
            <arg direction="in" type="i" name="recursionDepth"/>
            <arg direction="in" type="as" name="propertyNames"/>
            <arg direction="out" type="u" name="revision"/>
            <arg direction="out" type="(ia{sv}av)" name="layout"/>
          </method>
          <method name="GetGroupProperties">
            <arg direction="in" type="ai" name="ids"/>
            <arg direction="in" type="as" name="propertyNames"/>
            <arg direction="out" type="a(ia{sv})" name="properties"/>
          </method>
          <method name="AboutToShow">
            <arg direction="in" type="i" name="id"/>
            <arg direction="out" type="b" name="needUpdate"/>
          </method>
          <method name="AboutToShowGroup">
            <arg direction="in" type="ai" name="ids"/>
            <arg direction="out" type="ai" name="updatesNeeded"/>
            <arg direction="out" type="ai" name="idErrors"/>
          </method>
          <method name="Event">
            <arg direction="in" type="i" name="id"/>
            <arg direction="in" type="s" name="eventId"/>
            <arg direction="in" type="v" name="data"/>
            <arg direction="in" type="u" name="timestamp"/>
          </method>
          <method name="EventGroup">
            <arg direction="in" type="a(isvu)" name="events"/>
            <arg direction="out" type="ai" name="idErrors"/>
          </method>
          <property name="Version" type="u" access="read"/>
          <property name="TextDirection" type="s" access="read"/>
          <property name="Status" type="s" access="read"/>
          <property name="IconThemePath" type="as" access="read"/>
          <signal name="LayoutUpdated">
            <arg type="u" name="revision"/>
            <arg type="i" name="parent"/>
          </signal>
          <signal name="ItemsPropertiesUpdated">
            <arg type="a(ia{sv})" name="updatedProps"/>
            <arg type="a(ias)" name="removedProps"/>
          </signal>
        </interface>
        """
    );

    private readonly Action quitCallback;

    public string Path => "/MenuBar";

    public DbusTrayMenuHandler(Action quitCallback)
    {
        this.quitCallback = quitCallback;
    }

    public bool HandlesChildPaths => false;

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        if (context.IsDBusIntrospectRequest)
        {
            context.ReplyIntrospectXml([DbusmenuInterfaceXml]);
            return default;
        }

        var request = context.Request;
        var iface = request.InterfaceAsString;
        var member = request.MemberAsString;

        if (iface == "com.canonical.dbusmenu")
        {
            HandleDbusmenu(context, member);
        }
        else if (context.IsPropertiesInterfaceRequest)
        {
            HandleProperties(context, member);
        }
        else
        {
            context.ReplyError("org.freedesktop.DBus.Error.UnknownInterface",
                $"Unknown interface: {iface}");
        }

        return default;
    }

    private void HandleDbusmenu(MethodContext context, string? member)
    {
        switch (member)
        {
            case "GetLayout":
                HandleGetLayout(context);
                break;
            case "GetGroupProperties":
                HandleGetGroupProperties(context);
                break;
            case "AboutToShow":
                HandleAboutToShow(context);
                break;
            case "AboutToShowGroup":
                HandleAboutToShowGroup(context);
                break;
            case "Event":
                HandleEvent(context);
                break;
            case "EventGroup":
                HandleEventGroup(context);
                break;
            default:
                context.ReplyUnknownMethodError();
                break;
        }
    }

    private void HandleGetLayout(MethodContext context)
    {
        var reader = context.Request.GetBodyReader();
        var parentId = reader.ReadInt32();
        reader.ReadInt32(); // recursionDepth
        reader.ReadArrayOfString(); // propertyNames (ignored, return all)

        // Response signature: u(ia{sv}av)
        // MessageWriter is a ref struct: a 'using' local cannot be passed by ref,
        // so dispose manually via try/finally.
        var writer = context.CreateReplyWriter("u(ia{sv}av)");
        try
        {
            writer.WriteUInt32(1); // revision

            if (parentId == RootId)
            {
                WriteRootLayout(ref writer);
            }
            else if (parentId == QuitId)
            {
                WriteQuitItemLayout(ref writer);
            }
            else
            {
                // Unknown item, return empty
                WriteEmptyItemLayout(ref writer, parentId);
            }

            context.Reply(writer.CreateMessage());
        }
        finally
        {
            writer.Dispose();
        }
    }

    private static void WriteRootLayout(ref MessageWriter writer)
    {
        writer.WriteStructureStart();
        writer.WriteInt32(RootId);

        // Root properties: a{sv}
        var dictStart = writer.WriteDictionaryStart();
        writer.WriteDictionaryEntryStart();
        writer.WriteString("children-display");
        writer.WriteVariantString("submenu");
        writer.WriteDictionaryEnd(dictStart);

        // Children: av (array of variants, each containing a (ia{sv}av) struct)
        var childrenStart = writer.WriteArrayStart(DBusType.Variant);
        WriteQuitChildVariant(ref writer);
        writer.WriteArrayEnd(childrenStart);
    }

    private static void WriteQuitChildVariant(ref MessageWriter writer)
    {
        // Write as variant containing struct (ia{sv}av)
        writer.WriteSignature("(ia{sv}av)");
        writer.WriteStructureStart();
        writer.WriteInt32(QuitId);

        // Quit properties
        var dictStart = writer.WriteDictionaryStart();

        writer.WriteDictionaryEntryStart();
        writer.WriteString("label");
        writer.WriteVariantString("Quit");

        writer.WriteDictionaryEntryStart();
        writer.WriteString("enabled");
        writer.WriteVariantBool(true);

        writer.WriteDictionaryEnd(dictStart);

        // No children
        var childrenStart = writer.WriteArrayStart(DBusType.Variant);
        writer.WriteArrayEnd(childrenStart);
    }

    private static void WriteQuitItemLayout(ref MessageWriter writer)
    {
        writer.WriteStructureStart();
        writer.WriteInt32(QuitId);

        var dictStart = writer.WriteDictionaryStart();
        writer.WriteDictionaryEntryStart();
        writer.WriteString("label");
        writer.WriteVariantString("Quit");
        writer.WriteDictionaryEntryStart();
        writer.WriteString("enabled");
        writer.WriteVariantBool(true);
        writer.WriteDictionaryEnd(dictStart);

        var childrenStart = writer.WriteArrayStart(DBusType.Variant);
        writer.WriteArrayEnd(childrenStart);
    }

    private static void WriteEmptyItemLayout(ref MessageWriter writer, int id)
    {
        writer.WriteStructureStart();
        writer.WriteInt32(id);

        var dictStart = writer.WriteDictionaryStart();
        writer.WriteDictionaryEnd(dictStart);

        var childrenStart = writer.WriteArrayStart(DBusType.Variant);
        writer.WriteArrayEnd(childrenStart);
    }

    private static void HandleGetGroupProperties(MethodContext context)
    {
        var reader = context.Request.GetBodyReader();
        reader.ReadArrayOfInt32(); // ids
        reader.ReadArrayOfString(); // propertyNames

        // Response: a(ia{sv}) - array of (id, properties) structs
        using var writer = context.CreateReplyWriter("a(ia{sv})");
        var arrayStart = writer.WriteArrayStart(DBusType.Struct);

        // Root item
        writer.WriteStructureStart();
        writer.WriteInt32(RootId);
        var rootDict = writer.WriteDictionaryStart();
        writer.WriteDictionaryEntryStart();
        writer.WriteString("children-display");
        writer.WriteVariantString("submenu");
        writer.WriteDictionaryEnd(rootDict);

        // Quit item
        writer.WriteStructureStart();
        writer.WriteInt32(QuitId);
        var quitDict = writer.WriteDictionaryStart();
        writer.WriteDictionaryEntryStart();
        writer.WriteString("label");
        writer.WriteVariantString("Quit");
        writer.WriteDictionaryEntryStart();
        writer.WriteString("enabled");
        writer.WriteVariantBool(true);
        writer.WriteDictionaryEnd(quitDict);

        writer.WriteArrayEnd(arrayStart);
        context.Reply(writer.CreateMessage());
    }

    private static void HandleAboutToShow(MethodContext context)
    {
        // Response: b (needUpdate)
        using var writer = context.CreateReplyWriter("b");
        writer.WriteBool(false);
        context.Reply(writer.CreateMessage());
    }

    private static void HandleAboutToShowGroup(MethodContext context)
    {
        // Response: ai ai (updatesNeeded, idErrors)
        using var writer = context.CreateReplyWriter("aiai");
        writer.WriteArray(System.Array.Empty<int>());
        writer.WriteArray(System.Array.Empty<int>());
        context.Reply(writer.CreateMessage());
    }

    private void HandleEvent(MethodContext context)
    {
        var reader = context.Request.GetBodyReader();
        var id = reader.ReadInt32();
        var eventId = reader.ReadString();

        if (id == QuitId && eventId == "clicked")
        {
            quitCallback();
        }

        if (!context.NoReplyExpected)
        {
            using var writer = context.CreateReplyWriter(null);
            context.Reply(writer.CreateMessage());
        }
    }

    private static void HandleEventGroup(MethodContext context)
    {
        // Response: ai (idErrors - empty, no errors)
        using var writer = context.CreateReplyWriter("ai");
        writer.WriteArray(System.Array.Empty<int>());
        context.Reply(writer.CreateMessage());
    }

    private static void HandleProperties(MethodContext context, string? member)
    {
        switch (member)
        {
            case "Get":
                HandlePropertyGet(context);
                break;
            case "GetAll":
                HandlePropertyGetAll(context);
                break;
            default:
                context.ReplyUnknownMethodError();
                break;
        }
    }

    private static void HandlePropertyGet(MethodContext context)
    {
        var reader = context.Request.GetBodyReader();
        reader.ReadString(); // interface
        var propertyName = reader.ReadString();

        var writer = context.CreateReplyWriter("v");
        try
        {
            WriteMenuPropertyAsVariant(ref writer, propertyName);
            context.Reply(writer.CreateMessage());
        }
        finally
        {
            writer.Dispose();
        }
    }

    private static void HandlePropertyGetAll(MethodContext context)
    {
        var reader = context.Request.GetBodyReader();
        reader.ReadString(); // interface

        var writer = context.CreateReplyWriter("a{sv}");
        try
        {
            var dictStart = writer.WriteDictionaryStart();

            foreach (var prop in (ReadOnlySpan<string>)["Version", "TextDirection", "Status", "IconThemePath"])
            {
                writer.WriteDictionaryEntryStart();
                writer.WriteString(prop);
                WriteMenuPropertyAsVariant(ref writer, prop);
            }

            writer.WriteDictionaryEnd(dictStart);
            context.Reply(writer.CreateMessage());
        }
        finally
        {
            writer.Dispose();
        }
    }

    private static void WriteMenuPropertyAsVariant(ref MessageWriter writer, string propertyName)
    {
        switch (propertyName)
        {
            case "Version":
                writer.WriteVariantUInt32(3);
                break;
            case "TextDirection":
                writer.WriteVariantString("ltr");
                break;
            case "Status":
                writer.WriteVariantString("normal");
                break;
            case "IconThemePath":
                var emptyPaths = new Array<string>();
                writer.WriteVariant((VariantValue)emptyPaths);
                break;
            default:
                writer.WriteVariantString("");
                break;
        }
    }
}
#endif
