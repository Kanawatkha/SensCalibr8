using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace SensCalibr8.Calibration.Native;

internal static class NativeRawInput
{
    public const int WmInput = 0x00FF;
    private const uint RidInput = 0x10000003;
    private const uint RidiDeviceName = 0x20000007;
    private const uint RimTypeMouse = 0;
    private const uint RidevInputSink = 0x00000100;

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr TargetWindow;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct RawMouseButtons
    {
        [FieldOffset(0)] public uint Buttons;
        [FieldOffset(0)] public ushort ButtonFlags;
        [FieldOffset(2)] public ushort ButtonData;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawMouse
    {
        public ushort Flags;
        public RawMouseButtons Buttons;
        public uint RawButtons;
        public int LastX;
        public int LastY;
        public uint ExtraInformation;
    }

    internal readonly record struct MouseMessage(
        IntPtr Device,
        int DeltaX,
        int DeltaY,
        ushort Flags,
        ushort ButtonFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(
        [In] RawInputDevice[] devices,
        uint numberOfDevices,
        uint sizeOfDevice);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr rawInput,
        uint command,
        IntPtr data,
        ref uint size,
        uint headerSize);

    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetRawInputDeviceInfoSize(
        IntPtr device,
        uint command,
        IntPtr data,
        ref uint size);

    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr device,
        uint command,
        StringBuilder data,
        ref uint size);

    public static void RegisterMouse(IntPtr windowHandle)
    {
        RawInputDevice[] devices =
        {
            new RawInputDevice
            {
                UsagePage = 0x01,
                Usage = 0x02,
                Flags = RidevInputSink,
                TargetWindow = windowHandle
            }
        };

        if (!RegisterRawInputDevices(
                devices,
                (uint)devices.Length,
                (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                "Unable to register the native raw mouse input sink.");
        }
    }

    public static bool TryReadMouse(IntPtr rawInputHandle, out MouseMessage message)
    {
        message = default;
        uint size = 0;
        uint headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        uint queryResult = GetRawInputData(
            rawInputHandle,
            RidInput,
            IntPtr.Zero,
            ref size,
            headerSize);
        if (queryResult != 0 || size < headerSize)
        {
            return false;
        }

        IntPtr buffer = Marshal.AllocHGlobal(checked((int)size));
        try
        {
            uint copied = GetRawInputData(
                rawInputHandle,
                RidInput,
                buffer,
                ref size,
                headerSize);
            if (copied != size)
            {
                return false;
            }

            RawInputHeader header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            if (header.Type != RimTypeMouse || header.Device == IntPtr.Zero)
            {
                return false;
            }

            IntPtr mousePointer = IntPtr.Add(buffer, checked((int)headerSize));
            RawMouse mouse = Marshal.PtrToStructure<RawMouse>(mousePointer);
            message = new MouseMessage(
                header.Device,
                mouse.LastX,
                mouse.LastY,
                mouse.Flags,
                mouse.Buttons.ButtonFlags);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static string GetDevicePath(IntPtr device)
    {
        uint characterCount = 0;
        GetRawInputDeviceInfoSize(device, RidiDeviceName, IntPtr.Zero, ref characterCount);
        if (characterCount == 0)
        {
            return "unknown-native-device";
        }

        StringBuilder value = new(checked((int)characterCount));
        uint result = GetRawInputDeviceInfo(device, RidiDeviceName, value, ref characterCount);
        return result == uint.MaxValue || value.Length == 0
            ? "unknown-native-device"
            : value.ToString();
    }

    public static int StableDeviceId(string devicePath)
    {
        const uint OffsetBasis = 2166136261;
        const uint Prime = 16777619;
        uint hash = OffsetBasis;
        foreach (char character in devicePath.ToUpperInvariant())
        {
            hash ^= character;
            hash *= Prime;
        }

        return (int)(hash & 0x7FFFFFFF);
    }

    public static int ExpectedHeaderSize => sizeof(uint) * 2 + IntPtr.Size * 2;
}
