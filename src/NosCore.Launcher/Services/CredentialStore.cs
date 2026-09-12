using System.Runtime.InteropServices;
using System.Text;

namespace NosCore.Launcher.Services;

/// <summary>
/// Stores the account password in Windows Credential Manager under a generic
/// credential. The Electron launcher this replaces kept passwords as cleartext
/// JSON in the user profile; Credential Manager keeps them encrypted, scoped to
/// the Windows account, and — unlike a DPAPI blob in our own file — visible and
/// revocable by the user in a place they already know to look.
/// </summary>
public static class CredentialStore
{
    private const string TargetPrefix = "NosCore.Launcher";

    public static void Save(string username, string password)
    {
        var blob = Encoding.Unicode.GetBytes(password);
        var handle = GCHandle.Alloc(blob, GCHandleType.Pinned);
        try
        {
            var credential = new Credential
            {
                Type = CredentialType.Generic,
                TargetName = TargetName(username),
                UserName = username,
                CredentialBlob = handle.AddrOfPinnedObject(),
                CredentialBlobSize = (uint)blob.Length,
                Persist = CredentialPersist.LocalMachine,
            };
            if (!CredWriteW(ref credential, 0))
            {
                throw new InvalidOperationException(
                    $"CredWrite failed ({Marshal.GetLastWin32Error()}).");
            }
        }
        finally
        {
            handle.Free();
        }
    }

    public static string? Load(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        if (!CredReadW(TargetName(username), CredentialType.Generic, 0, out var handle)) return null;

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(handle);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0) return null;
            // Size is in bytes; the blob was written as UTF-16.
            return Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
        }
        finally
        {
            CredFree(handle);
        }
    }

    public static void Delete(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return;
        CredDeleteW(TargetName(username), CredentialType.Generic, 0);
    }

    private static string TargetName(string username) => $"{TargetPrefix}:{username}";

    private enum CredentialType : uint
    {
        Generic = 1,
    }

    private enum CredentialPersist : uint
    {
        LocalMachine = 2,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public CredentialType Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public CredentialPersist Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWriteW([In] ref Credential credential, uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredReadW(string targetName, CredentialType type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDeleteW(string targetName, CredentialType type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
