using Steamworks;

public class WebFisher
{
    public SteamId SteamId { get; set; }
    public string FisherID { get; set; }
    public string FisherName { get; set; }

    public long PlayerInstanceID { get; set; }
    public Vector3 PlayerPosition { get; set; }

    public WebFisher(SteamId id, string fisherName)
    {
        this.SteamId = id;
        string randomID = new string(Enumerable.Range(0, 5).Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[new Random().Next(36)]).ToArray());
        FisherID = randomID;
        FisherName = fisherName;

        PlayerInstanceID = 0;
        PlayerPosition = new Vector3(0,0,0);
    }
};

public class WFInstance
{
    public int InstanceID { get; set; }
    public string Type { get; set; }
    public System.DateTimeOffset SpawnTime = DateTimeOffset.UtcNow;

    public WFInstance(int ID, string Type)
    {
        this.InstanceID = ID;
        this.Type = Type;
    }
}