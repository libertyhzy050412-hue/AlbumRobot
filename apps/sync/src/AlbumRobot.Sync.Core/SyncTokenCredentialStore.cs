using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace AlbumRobot.Sync.Core;

/// <summary>
/// Stores the remote Worker bearer token in the current Windows user's
/// Credential Manager. The token is keyed by a hash of the Worker URL so a
/// token cannot be silently reused for a different endpoint.
/// </summary>
public sealed class SyncTokenCredentialStore
{
    private const uint GenericCredentialType = 1;
    private const uint LocalMachinePersistence = 2;
    private const int MaximumCredentialBlobBytes = 512;
    private const string TargetPrefix = "AlbumRobot.SyncToken.v1.";

    public bool TryRead(string workerBaseUrl, out string syncToken)
    {
        syncToken = string.Empty;
        if (!OperatingSystem.IsWindows()) return false;

        IntPtr credentialPointer = IntPtr.Zero;
        try
        {
            var target = BuildTarget(workerBaseUrl);
            if (!CredRead(target, GenericCredentialType, 0, out credentialPointer)) return false;

            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlob == IntPtr.Zero ||
                credential.CredentialBlobSize == 0 ||
                credential.CredentialBlobSize > MaximumCredentialBlobBytes)
            {
                return false;
            }

            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            var value = Encoding.UTF8.GetString(bytes).Trim();
            if (value.Length == 0) return false;
            syncToken = value;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            InvalidOperationException or
            MarshalDirectiveException or
            Win32Exception or
            DllNotFoundException or
            EntryPointNotFoundException)
        {
            return false;
        }
        finally
        {
            if (credentialPointer != IntPtr.Zero) CredFree(credentialPointer);
        }
    }

    public bool TryWrite(string workerBaseUrl, string syncToken)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(syncToken)) return false;

        var bytes = Encoding.UTF8.GetBytes(syncToken.Trim());
        if (bytes.Length == 0 || bytes.Length > MaximumCredentialBlobBytes) return false;

        IntPtr targetPointer = IntPtr.Zero;
        IntPtr userPointer = IntPtr.Zero;
        IntPtr blobPointer = IntPtr.Zero;
        try
        {
            targetPointer = Marshal.StringToCoTaskMemUni(BuildTarget(workerBaseUrl));
            userPointer = Marshal.StringToCoTaskMemUni("AlbumRobot Sync");
            blobPointer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, blobPointer, bytes.Length);

            var credential = new NativeCredential
            {
                Type = GenericCredentialType,
                TargetName = targetPointer,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blobPointer,
                Persist = LocalMachinePersistence,
                UserName = userPointer,
            };

            return CredWrite(ref credential, 0);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            InvalidOperationException or
            MarshalDirectiveException or
            Win32Exception or
            DllNotFoundException or
            EntryPointNotFoundException)
        {
            return false;
        }
        finally
        {
            if (blobPointer != IntPtr.Zero) Marshal.FreeHGlobal(blobPointer);
            if (userPointer != IntPtr.Zero) Marshal.FreeCoTaskMem(userPointer);
            if (targetPointer != IntPtr.Zero) Marshal.FreeCoTaskMem(targetPointer);
        }
    }

    public bool TryDelete(string workerBaseUrl)
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            return CredDelete(BuildTarget(workerBaseUrl), GenericCredentialType, 0) ||
                   Marshal.GetLastWin32Error() == 1168;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            InvalidOperationException or
            MarshalDirectiveException or
            Win32Exception or
            DllNotFoundException or
            EntryPointNotFoundException)
        {
            return false;
        }
    }

    public static string BuildTarget(string workerBaseUrl)
    {
        if (!Uri.TryCreate(workerBaseUrl?.Trim(), UriKind.Absolute, out var workerUri) ||
            (workerUri.Scheme != Uri.UriSchemeHttp && workerUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("A HTTP(S) Worker URL is required.", nameof(workerBaseUrl));
        }

        var normalizedUrl = workerUri.AbsoluteUri.TrimEnd('/');
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedUrl)));
        return TargetPrefix + hash;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string targetName, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string targetName, uint type, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr credential);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }
}
