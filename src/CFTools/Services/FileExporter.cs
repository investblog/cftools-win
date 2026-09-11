using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace CFTools.Services;

/// <summary>
/// Save-file dialog + write, for CSV exports. Must be called from the UI thread.
/// </summary>
public static class FileExporter
{
    /// <summary>
    /// Ask the user where to save and write the content. Returns false if cancelled.
    /// </summary>
    public static async Task<bool> SaveCsvAsync(string suggestedFileName, string content)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName,
        };
        picker.FileTypeChoices.Add("CSV file", new List<string> { ".csv" });
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.MainWindowHandle);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
            return false;

        await FileIO.WriteTextAsync(file, content, UnicodeEncoding.Utf8);
        return true;
    }

    /// <summary>
    /// Safe file-name fragment from an arbitrary label (account name etc.).
    /// </summary>
    public static string Slug(string? value, string fallback = "export")
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var chars = value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-');
        var slug = new string(chars.ToArray()).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);

        return slug.Length == 0 ? fallback : slug[..Math.Min(40, slug.Length)];
    }
}
