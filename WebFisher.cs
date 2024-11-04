using Steamworks;

public class WebFisher
{
    public SteamId SteamId { get; set; }
    public string FisherID { get; set; }
    public string FisherName { get; set; }

    public WebFisher(SteamId id, string fisherName)
    {
        this.SteamId = id;
        string randomID = new string(Enumerable.Range(0, 5).Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[new Random().Next(36)]).ToArray());
        FisherID = randomID;
        FisherName = fisherName;
    }
};