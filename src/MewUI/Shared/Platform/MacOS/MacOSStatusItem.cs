using Aprillz.MewUI.Controls;
using System.Runtime.InteropServices;

namespace Aprillz.MewUI.Platform.MacOS;

public sealed unsafe class MacOSStatusItem : IDisposable
{
    private static readonly Dictionary<nint, MacOSStatusItem> Instances = new();
    private static readonly object SyncRoot = new();
    private static bool _initialized;
    private static nint _clsNSObject;
    private static nint _clsNSStatusBar;
    private static nint _clsNSMenu;
    private static nint _clsNSMenuItem;
    private static nint _clsNSImage;
    private static nint _selAlloc;
    private static nint _selInit;
    private static nint _selSystemStatusBar;
    private static nint _selStatusItemWithLength;
    private static nint _selRemoveStatusItem;
    private static nint _selButton;
    private static nint _selSetTitle;
    private static nint _selSetImage;
    private static nint _selSetTemplate;
    private static nint _selSetSize;
    private static nint _selSetMenu;
    private static nint _selSetSubmenu;
    private static nint _selSetTarget;
    private static nint _selSetEnabled;
    private static nint _selAddItem;
    private static nint _selSeparatorItem;
    private static nint _selInitWithTitleActionKeyEquivalent;
    private static nint _selInitWithContentsOfFile;
    private static nint _selInvokeMenuItem;
    private static nint _delegateClass;

    private readonly Dictionary<nint, Action?> _actionsByMenuItem = new();
    private readonly nint _statusBar;
    private readonly nint _delegateObject;
    private nint _statusItem;

    private string _title = string.Empty;
    private string? _imagePath;
    private Menu? _menuModel;

    public MacOSStatusItem()
    {
        EnsureInitialized();

        _statusBar = ObjC.MsgSend_nint(_clsNSStatusBar, _selSystemStatusBar);
        _delegateObject = CreateDelegateObject(this);
        _statusItem = ObjC.MsgSend_nint_double(_statusBar, _selStatusItemWithLength, -1);
        ApplyVisuals();
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value ?? string.Empty;
            ApplyVisuals();
        }
    }

    public string? ImagePath
    {
        get => _imagePath;
        set
        {
            _imagePath = string.IsNullOrWhiteSpace(value) ? null : value;
            ApplyVisuals();
        }
    }

    public Menu? Menu
    {
        get => _menuModel;
        set
        {
            _menuModel = value;
            RebuildMenu();
        }
    }

    public void Dispose()
    {
        lock (SyncRoot)
        {
            if (_delegateObject != 0)
            {
                Instances.Remove(_delegateObject);
            }
        }

        if (_statusBar != 0 && _statusItem != 0)
        {
            ObjC.MsgSend_void_nint_nint(_statusBar, _selRemoveStatusItem, _statusItem);
            _statusItem = 0;
        }
    }

    [UnmanagedCallersOnly]
    private static void InvokeMenuItem(nint self, nint sel, nint sender)
    {
        MacOSStatusItem? instance;
        lock (SyncRoot)
        {
            Instances.TryGetValue(self, out instance);
        }

        if (instance == null || sender == 0)
        {
            return;
        }

        if (instance._actionsByMenuItem.TryGetValue(sender, out var action))
        {
            action?.Invoke();
        }
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        MacOSInterop.EnsureApplicationInitialized();

        _clsNSObject = ObjC.GetClass("NSObject");
        _clsNSStatusBar = ObjC.GetClass("NSStatusBar");
        _clsNSMenu = ObjC.GetClass("NSMenu");
        _clsNSMenuItem = ObjC.GetClass("NSMenuItem");
        _clsNSImage = ObjC.GetClass("NSImage");

        _selAlloc = ObjC.Sel("alloc");
        _selInit = ObjC.Sel("init");
        _selSystemStatusBar = ObjC.Sel("systemStatusBar");
        _selStatusItemWithLength = ObjC.Sel("statusItemWithLength:");
        _selRemoveStatusItem = ObjC.Sel("removeStatusItem:");
        _selButton = ObjC.Sel("button");
        _selSetTitle = ObjC.Sel("setTitle:");
        _selSetImage = ObjC.Sel("setImage:");
        _selSetTemplate = ObjC.Sel("setTemplate:");
        _selSetSize = ObjC.Sel("setSize:");
        _selSetMenu = ObjC.Sel("setMenu:");
        _selSetSubmenu = ObjC.Sel("setSubmenu:");
        _selSetTarget = ObjC.Sel("setTarget:");
        _selSetEnabled = ObjC.Sel("setEnabled:");
        _selAddItem = ObjC.Sel("addItem:");
        _selSeparatorItem = ObjC.Sel("separatorItem");
        _selInitWithTitleActionKeyEquivalent = ObjC.Sel("initWithTitle:action:keyEquivalent:");
        _selInitWithContentsOfFile = ObjC.Sel("initWithContentsOfFile:");
        _selInvokeMenuItem = ObjC.Sel("invokeMenuItem:");

        if (_delegateClass == 0 && _clsNSObject != 0)
        {
            const string className = "MewUIStatusItemDelegate";
            var cls = ObjC.GetClass(className);
            if (cls == 0)
            {
                cls = ObjC.AllocateClassPair(_clsNSObject, className);
                if (cls != 0)
                {
                    var imp = (nint)(delegate* unmanaged<nint, nint, nint, void>)&InvokeMenuItem;
                    _ = ObjC.AddMethod(cls, _selInvokeMenuItem, imp, "v@:@");
                    ObjC.RegisterClassPair(cls);
                }
            }

            _delegateClass = cls;
        }

        _initialized = true;
    }

    private static nint CreateDelegateObject(MacOSStatusItem owner)
    {
        if (_delegateClass == 0)
        {
            return 0;
        }

        var obj = ObjC.MsgSend_nint(_delegateClass, _selAlloc);
        obj = obj != 0 ? ObjC.MsgSend_nint(obj, _selInit) : 0;
        if (obj != 0)
        {
            lock (SyncRoot)
            {
                Instances[obj] = owner;
            }
        }

        return obj;
    }

    private void ApplyVisuals()
    {
        if (_statusItem == 0)
        {
            return;
        }

        var button = ObjC.MsgSend_nint(_statusItem, _selButton);
        if (button == 0)
        {
            return;
        }

        ObjC.MsgSend_void_nint_nint(button, _selSetTitle, ObjC.CreateNSString(_title));

        if (_clsNSImage != 0 && !string.IsNullOrWhiteSpace(_imagePath))
        {
            var image = ObjC.MsgSend_nint(_clsNSImage, _selAlloc);
            image = image != 0 ? ObjC.MsgSend_nint_nint(image, _selInitWithContentsOfFile, ObjC.CreateNSString(_imagePath)) : 0;
            if (image != 0)
            {
                ObjC.MsgSend_void_nint_bool(image, _selSetTemplate, true);
                ObjC.MsgSend_void_nint_size(image, _selSetSize, new NSSize(20, 20));
                ObjC.MsgSend_void_nint_nint(button, _selSetImage, image);
            }
        }
        else
        {
            ObjC.MsgSend_void_nint_nint(button, _selSetImage, 0);
        }
    }

    private void RebuildMenu()
    {
        _actionsByMenuItem.Clear();

        nint nativeMenu = 0;
        if (_menuModel != null)
        {
            nativeMenu = CreateMenu(_menuModel);
        }

        if (_statusItem != 0)
        {
            ObjC.MsgSend_void_nint_nint(_statusItem, _selSetMenu, nativeMenu);
        }
    }

    private nint CreateMenu(Menu menu)
    {
        var nativeMenu = ObjC.MsgSend_nint(_clsNSMenu, _selAlloc);
        nativeMenu = nativeMenu != 0 ? ObjC.MsgSend_nint(nativeMenu, _selInit) : 0;
        if (nativeMenu == 0)
        {
            return 0;
        }

        foreach (var entry in menu.Items)
        {
            nint nativeItem;
            if (entry is MenuSeparator)
            {
                nativeItem = ObjC.MsgSend_nint(_clsNSMenuItem, _selSeparatorItem);
            }
            else if (entry is MenuItem item)
            {
                nativeItem = CreateMenuItem(item);
            }
            else
            {
                continue;
            }

            if (nativeItem != 0)
            {
                ObjC.MsgSend_void_nint_nint(nativeMenu, _selAddItem, nativeItem);
            }
        }

        return nativeMenu;
    }

    private nint CreateMenuItem(MenuItem item)
    {
        var nativeItem = ObjC.MsgSend_nint(_clsNSMenuItem, _selAlloc);
        nativeItem = ObjC.MsgSend_nint_nint_nint_nint(
            nativeItem,
            _selInitWithTitleActionKeyEquivalent,
            ObjC.CreateNSString(item.Text ?? string.Empty),
            item.Click != null ? _selInvokeMenuItem : 0,
            ObjC.CreateNSString(string.Empty));
        if (nativeItem == 0)
        {
            return 0;
        }

        ObjC.MsgSend_void_nint_nint(nativeItem, _selSetTarget, _delegateObject);
        ObjC.MsgSend_void_nint_bool(nativeItem, _selSetEnabled, item.IsEnabled);
        _actionsByMenuItem[nativeItem] = item.Click;

        if (item.SubMenu != null)
        {
            var submenu = CreateMenu(item.SubMenu);
            ObjC.MsgSend_void_nint_nint(nativeItem, _selSetSubmenu, submenu);
        }

        return nativeItem;
    }
}
