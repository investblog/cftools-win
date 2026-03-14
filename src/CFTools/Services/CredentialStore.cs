using Windows.Security.Credentials;

namespace CFTools.Services;

/// <summary>
/// Stores Cloudflare credentials in Windows Credential Manager.
/// </summary>
public sealed class CredentialStore
{
    private const string Resource = "CFTools";

    /// <summary>
    /// Save credentials to Windows Credential Manager.
    /// </summary>
    public void Save(string email, string apiKey)
    {
        Delete();

        var vault = new PasswordVault();
        vault.Add(new PasswordCredential(Resource, email, apiKey));
    }

    /// <summary>
    /// Load credentials from Windows Credential Manager.
    /// Returns null if no credentials are stored.
    /// </summary>
    public (string Email, string ApiKey)? Load()
    {
        try
        {
            var vault = new PasswordVault();
            var credentials = vault.FindAllByResource(Resource);

            if (credentials.Count == 0)
                return null;

            var credential = credentials[0];
            credential.RetrievePassword();

            return (credential.UserName, credential.Password);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Delete all CFTools credentials from Windows Credential Manager.
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
    /// Check if credentials exist in Windows Credential Manager.
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
