using CFTools.Models;
using Windows.Security.Credentials;

namespace CFTools.Services;

/// <summary>
/// Stores the Cloudflare credential in Windows Credential Manager. The credential kind is
/// encoded in the entry's user name (see <see cref="CredentialVaultCodec"/>).
/// </summary>
public sealed class CredentialStore
{
    private const string Resource = "CFTools";

    /// <summary>
    /// Save the credential to Windows Credential Manager (replaces any existing entry).
    /// </summary>
    public void Save(CfCredential credential)
    {
        Delete();

        var userName = CredentialVaultCodec.EncodeUserName(credential);
        if (userName is null || string.IsNullOrEmpty(credential.Secret))
            return;

        var vault = new PasswordVault();
        vault.Add(new PasswordCredential(Resource, userName, credential.Secret));
    }

    /// <summary>
    /// Load the stored credential. Returns null if nothing is stored.
    /// </summary>
    public CfCredential? Load()
    {
        try
        {
            var vault = new PasswordVault();
            var credentials = vault.FindAllByResource(Resource);

            if (credentials.Count == 0)
                return null;

            var credential = credentials[0];
            credential.RetrievePassword();

            return CredentialVaultCodec.Decode(credential.UserName, credential.Password);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Delete all stored credentials from Windows Credential Manager.
    /// </summary>
    public void Delete()
    {
        try
        {
            var vault = new PasswordVault();
            var credentials = vault.FindAllByResource(Resource);

            foreach (var credential in credentials)
            {
                vault.Remove(credential);
            }
        }
        catch (Exception)
        {
            // No credentials to delete
        }
    }

    /// <summary>
    /// Check if a credential exists in Windows Credential Manager.
    /// </summary>
    public bool Exists()
    {
        try
        {
            var vault = new PasswordVault();
            var credentials = vault.FindAllByResource(Resource);
            return credentials.Count > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
