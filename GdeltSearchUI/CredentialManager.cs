using System.Runtime.InteropServices;
using System.Text;

namespace GdeltSearchUI;

internal static class CredentialManager
{
    private const string BlueskyTarget            = "GdeltSearchUI/Bluesky";
    private const string GasPriceBlueskyTarget    = "GdeltSearchUI/GasPriceBluesky";
    private const string QuakeBlueskyTarget        = "GdeltSearchUI/QuakeBluesky";
    private const string DebtBlueskyTarget         = "GdeltSearchUI/DebtBluesky";
    private const string CommodityBlueskyTarget    = "GdeltSearchUI/CommodityBluesky";
    private const string EiaTarget                 = "GdeltSearchUI/EIA";
    private const string ApiNinjasTarget           = "GdeltSearchUI/ApiNinjas";
    private const uint CRED_TYPE_GENERIC = 1;
    private const uint CRED_PERSIST_LOCAL_MACHINE = 2;

    // Struct used for writing (string fields marshal correctly outbound)
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL_WRITE
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    // Struct used for reading (all IntPtr — we marshal strings manually from native memory)
    [StructLayout(LayoutKind.Sequential)]
    private struct CREDENTIAL_READ
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref CREDENTIAL_WRITE cred, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr cred);

    // ── Bluesky ───────────────────────────────────────────────────────────────

    public static void Save(string handle, string password) =>
        SaveInternal(BlueskyTarget, handle, password);

    public static (string Handle, string Password)? Load() =>
        LoadInternal(BlueskyTarget);

    public static void Delete() => CredDelete(BlueskyTarget, CRED_TYPE_GENERIC, 0);

    // ── Gas Price Bluesky ─────────────────────────────────────────────────────

    public static void SaveGasPriceBluesky(string handle, string password) =>
        SaveInternal(GasPriceBlueskyTarget, handle, password);

    public static (string Handle, string Password)? LoadGasPriceBluesky() =>
        LoadInternal(GasPriceBlueskyTarget);

    // ── Quake Bluesky ─────────────────────────────────────────────────────────

    public static void SaveQuakeBluesky(string handle, string password) =>
        SaveInternal(QuakeBlueskyTarget, handle, password);

    public static (string Handle, string Password)? LoadQuakeBluesky() =>
        LoadInternal(QuakeBlueskyTarget);

    // ── Debt Bluesky ──────────────────────────────────────────────────────────

    public static void SaveDebtBluesky(string handle, string password) =>
        SaveInternal(DebtBlueskyTarget, handle, password);

    public static (string Handle, string Password)? LoadDebtBluesky() =>
        LoadInternal(DebtBlueskyTarget);

    // ── Commodity Bluesky ─────────────────────────────────────────────────────

    public static void SaveCommodityBluesky(string handle, string password) =>
        SaveInternal(CommodityBlueskyTarget, handle, password);

    public static (string Handle, string Password)? LoadCommodityBluesky() =>
        LoadInternal(CommodityBlueskyTarget);

    // ── API-Ninjas ─────────────────────────────────────────────────────────────

    public static void SaveApiNinjasKey(string apiKey) =>
        SaveInternal(ApiNinjasTarget, "apininjas", apiKey);

    public static string? LoadApiNinjasKey() =>
        LoadInternal(ApiNinjasTarget)?.Password;

    // ── EIA ───────────────────────────────────────────────────────────────────

    public static void SaveEiaApiKey(string apiKey) =>
        SaveInternal(EiaTarget, "eia", apiKey);

    public static string? LoadEiaApiKey() =>
        LoadInternal(EiaTarget)?.Password;

    // ── Internal helpers ──────────────────────────────────────────────────────

    private static void SaveInternal(string target, string username, string password)
    {
        var blob = Encoding.Unicode.GetBytes(password);
        var blobPtr = Marshal.AllocHGlobal(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, blobPtr, blob.Length);
            var cred = new CREDENTIAL_WRITE
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = target,
                UserName = username,
                CredentialBlob = blobPtr,
                CredentialBlobSize = (uint)blob.Length,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
            };
            if (!CredWrite(ref cred, 0))
                throw new InvalidOperationException($"CredWrite failed (error {Marshal.GetLastWin32Error()})");
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
        }
    }

    private static (string Handle, string Password)? LoadInternal(string target)
    {
        if (!CredRead(target, CRED_TYPE_GENERIC, 0, out var ptr)) return null;
        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL_READ>(ptr);
            var handle = Marshal.PtrToStringUni(cred.UserName) ?? string.Empty;
            var blob = new byte[cred.CredentialBlobSize];
            if (cred.CredentialBlobSize > 0)
                Marshal.Copy(cred.CredentialBlob, blob, 0, blob.Length);
            var password = Encoding.Unicode.GetString(blob);
            return (handle, password);
        }
        finally
        {
            CredFree(ptr);
        }
    }
}
