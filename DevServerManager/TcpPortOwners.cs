using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DevServerManager;

public readonly record struct TcpPortOwner(int Port, int ProcessId);

// The Windows owner-PID listener tables include both IPv4 and IPv6 endpoints.
public static class TcpPortOwners
{
    private const int ErrorInsufficientBuffer = 122;
    private const int TcpTableOwnerPidListener = 3;
    private const int AddressFamilyInterNetwork = 2;
    private const int AddressFamilyInterNetworkV6 = 23;
    private const int TcpStateListen = 2;

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool order, int addressFamily, int tableClass, uint reserved);

    public static IReadOnlyList<TcpPortOwner> FindListeners(IReadOnlySet<int> ports)
    {
        if (ports.Count == 0) return [];
        var found = new List<TcpPortOwner>();
        ReadTable(AddressFamilyInterNetwork, 24, 0, 8, 20, ports, found);
        ReadTable(AddressFamilyInterNetworkV6, 56, 48, 20, 52, ports, found);
        return found.Distinct().ToArray();
    }

    private static void ReadTable(int family, int rowSize, int stateOffset, int portOffset, int pidOffset, IReadOnlySet<int> ports, List<TcpPortOwner> found)
    {
        var size = 0;
        var result = GetExtendedTcpTable(IntPtr.Zero, ref size, false, family, TcpTableOwnerPidListener, 0);
        if (result != ErrorInsufficientBuffer && result != 0) throw new Win32Exception((int)result);
        if (size < sizeof(int)) return;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var allocated = size;
            var buffer = Marshal.AllocHGlobal(allocated);
            try
            {
                result = GetExtendedTcpTable(buffer, ref size, false, family, TcpTableOwnerPidListener, 0);
                if (result == ErrorInsufficientBuffer) continue;
                if (result != 0) throw new Win32Exception((int)result);
                var count = Marshal.ReadInt32(buffer);
                if (count < 0 || count > (allocated - sizeof(int)) / rowSize) throw new InvalidOperationException("Invalid TCP listener table size.");
                for (var i = 0; i < count; i++)
                {
                    var row = IntPtr.Add(buffer, sizeof(int) + i * rowSize);
                    if (Marshal.ReadInt32(row, stateOffset) != TcpStateListen) continue;
                    var port = Marshal.ReadByte(row, portOffset) * 256 + Marshal.ReadByte(row, portOffset + 1);
                    if (ports.Contains(port)) found.Add(new TcpPortOwner(port, Marshal.ReadInt32(row, pidOffset)));
                }
                return;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        throw new Win32Exception(ErrorInsufficientBuffer);
    }
}
