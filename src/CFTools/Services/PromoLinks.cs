namespace CFTools.Services;

/// <summary>
/// Links to the publisher's own service (301.st), tagged per placement so the
/// dashboard can tell which in-app tip brought the visit. No identifiers are sent.
/// </summary>
public static class PromoLinks
{
    private const string Base = "https://301.st/";
    private const string Source = "cftools-win";

    public const string AfterCreateCampaign = "after-create";
    public const string ZonesPendingCampaign = "zones-pending";
    public const string AuthCampaign = "auth";
    public const string AboutCampaign = "about";

    public static string Url(string campaign) =>
        $"{Base}?utm_source={Source}&utm_medium=app&utm_campaign={campaign}";

    public static Uri Uri(string campaign) => new(Url(campaign));
}
