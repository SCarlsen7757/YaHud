#if LINUX
namespace R3E.Tray.Linux;

using System.Text;
using Tmds.DBus.Protocol;

/// <summary>
/// D-Bus method handler for the org.kde.StatusNotifierItem interface.
/// Exposes tray icon properties and handles introspection at /StatusNotifierItem.
/// </summary>
internal sealed class StatusNotifierItemHandler : IMethodHandler
{
    private static readonly string[] PropertyNames =
    [
        "Category", "Id", "Title", "Status", "WindowId",
        "IconName", "IconPixmap",
        "OverlayIconName", "OverlayIconPixmap",
        "AttentionIconName", "AttentionIconPixmap",
        "ToolTip", "ItemIsMenu", "Menu"
    ];

    private static readonly ReadOnlyMemory<byte> SniInterfaceXml = Encoding.UTF8.GetBytes(
        """
        <interface name="org.kde.StatusNotifierItem">
          <property name="Category" type="s" access="read"/>
          <property name="Id" type="s" access="read"/>
          <property name="Title" type="s" access="read"/>
          <property name="Status" type="s" access="read"/>
          <property name="WindowId" type="i" access="read"/>
          <property name="IconName" type="s" access="read"/>
          <property name="IconPixmap" type="a(iiay)" access="read"/>
          <property name="OverlayIconName" type="s" access="read"/>
          <property name="OverlayIconPixmap" type="a(iiay)" access="read"/>
          <property name="AttentionIconName" type="s" access="read"/>
          <property name="AttentionIconPixmap" type="a(iiay)" access="read"/>
          <property name="ToolTip" type="(sa(iiay)ss)" access="read"/>
          <property name="ItemIsMenu" type="b" access="read"/>
          <property name="Menu" type="o" access="read"/>
          <method name="ContextMenu">
            <arg direction="in" name="x" type="i"/>
            <arg direction="in" name="y" type="i"/>
          </method>
          <method name="Activate">
            <arg direction="in" name="x" type="i"/>
            <arg direction="in" name="y" type="i"/>
          </method>
          <method name="SecondaryActivate">
            <arg direction="in" name="x" type="i"/>
            <arg direction="in" name="y" type="i"/>
          </method>
          <method name="Scroll">
            <arg direction="in" name="delta" type="i"/>
            <arg direction="in" name="orientation" type="s"/>
          </method>
          <signal name="NewTitle"/>
          <signal name="NewIcon"/>
          <signal name="NewAttentionIcon"/>
          <signal name="NewOverlayIcon"/>
          <signal name="NewToolTip"/>
          <signal name="NewStatus">
            <arg type="s"/>
          </signal>
        </interface>
        """
    );

    private readonly VariantValue iconPixmapVariant;
    private readonly VariantValue emptyPixmapArrayVariant;
    private readonly VariantValue tooltipVariant;

    public string Path => "/StatusNotifierItem";

    public StatusNotifierItemHandler(int iconWidth, int iconHeight, byte[] iconArgbData)
    {
        var pixmapArray = new Array<Struct<int, int, byte[]>>
        {
            new(iconWidth, iconHeight, iconArgbData)
        };
        iconPixmapVariant = pixmapArray;

        var emptyPixmapArray = new Array<Struct<int, int, byte[]>>();
        emptyPixmapArrayVariant = emptyPixmapArray;

        var tooltip = new Struct<string, Array<Struct<int, int, byte[]>>, string, string>(
            "", new Array<Struct<int, int, byte[]>>(), "YaHud", "");
        tooltipVariant = tooltip;
    }

    public bool RunMethodHandlerSynchronously(Message message) => true;

    public ValueTask HandleMethodAsync(MethodContext context)
    {
        if (context.IsDBusIntrospectRequest)
        {
            context.ReplyIntrospectXml([SniInterfaceXml]);
            return default;
        }

        var request = context.Request;
        var iface = request.InterfaceAsString;
        var member = request.MemberAsString;

        if (context.IsPropertiesInterfaceRequest)
        {
            HandleProperties(context, member);
        }
        else if (iface == "org.kde.StatusNotifierItem")
        {
            HandleSniMethod(context, member);
        }
        else
        {
            context.ReplyError("org.freedesktop.DBus.Error.UnknownInterface",
                $"Unknown interface: {iface}");
        }

        return default;
    }

    private void HandleProperties(MethodContext context, string? member)
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

    private void HandlePropertyGet(MethodContext context)
    {
        var reader = context.Request.GetBodyReader();
        reader.ReadString(); // interface name
        var propertyName = reader.ReadString();

        using var writer = context.CreateReplyWriter("v");
        WritePropertyAsVariant(ref writer, propertyName);
        context.Reply(writer.CreateMessage());
    }

    private void HandlePropertyGetAll(MethodContext context)
    {
        var reader = context.Request.GetBodyReader();
        reader.ReadString(); // interface name

        using var writer = context.CreateReplyWriter("a{sv}");
        var dictStart = writer.WriteDictionaryStart();

        foreach (var prop in PropertyNames)
        {
            writer.WriteDictionaryEntryStart();
            writer.WriteString(prop);
            WritePropertyAsVariant(ref writer, prop);
        }

        writer.WriteDictionaryEnd(dictStart);
        context.Reply(writer.CreateMessage());
    }

    private void WritePropertyAsVariant(ref MessageWriter writer, string propertyName)
    {
        switch (propertyName)
        {
            case "Category":
                writer.WriteVariantString("ApplicationStatus");
                break;
            case "Id":
                writer.WriteVariantString("YaHud");
                break;
            case "Title":
                writer.WriteVariantString("YaHud");
                break;
            case "Status":
                writer.WriteVariantString("Active");
                break;
            case "WindowId":
                writer.WriteVariantInt32(0);
                break;
            case "IconName":
            case "OverlayIconName":
            case "AttentionIconName":
                writer.WriteVariantString("");
                break;
            case "IconPixmap":
                writer.WriteVariant(iconPixmapVariant);
                break;
            case "OverlayIconPixmap":
            case "AttentionIconPixmap":
                writer.WriteVariant(emptyPixmapArrayVariant);
                break;
            case "ToolTip":
                writer.WriteVariant(tooltipVariant);
                break;
            case "ItemIsMenu":
                writer.WriteVariantBool(false);
                break;
            case "Menu":
                writer.WriteVariantObjectPath("/MenuBar");
                break;
            default:
                writer.WriteVariantString("");
                break;
        }
    }

    private static void HandleSniMethod(MethodContext context, string? member)
    {
        switch (member)
        {
            case "Activate":
            case "SecondaryActivate":
            case "ContextMenu":
            case "Scroll":
                if (!context.NoReplyExpected)
                {
                    using var writer = context.CreateReplyWriter(null);
                    context.Reply(writer.CreateMessage());
                }
                break;
            default:
                context.ReplyUnknownMethodError();
                break;
        }
    }
}
#endif
