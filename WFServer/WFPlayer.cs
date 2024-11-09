using Steamworks;

namespace WFServer
{

    public class WFPlayer
    {
        public SteamId SteamId { get; set; }
        public string FisherID { get; set; }
        public string FisherName { get; set; }

        public long PlayerInstanceID { get; set; }
        public Vector3 PlayerPosition { get; set; }

        public WFPlayer(SteamId id, string fisherName)
        {
            this.SteamId = id;
            string randomID = new string(Enumerable.Range(0, 3).Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[new Random().Next(36)]).ToArray());
            FisherID = randomID;
            FisherName = fisherName;

            PlayerInstanceID = 0;
            PlayerPosition = new Vector3(0, 0, 0);
        }
    };

    public class WFActor
    {
        public int InstanceID { get; }
        public string Type { get; }
        public System.DateTimeOffset SpawnTime = DateTimeOffset.UtcNow;

        public Vector3 pos { get; set; }
        public Vector3 rot { get; set; }

        public WFActor(int ID, string Type, Vector3 entPos, Vector3 entRot = null)
        {
            this.InstanceID = ID;
            this.Type = Type;
            this.pos = entPos;
            if (entRot != null)
            {
                this.rot = entRot;
            }
            else
            {
                this.rot = Vector3.zero;
            }
        }

        public virtual void onUpdate()
        {

        }
    }

    public class RainCloud : WFActor
    {

        public Vector3 toCenter;
        public float wanderDirection;

        public RainCloud(int ID, Vector3 entPos) : base(ID, "raincloud", Vector3.zero)
        {
            this.pos = entPos;
            //this.InstanceID = ID;

            toCenter = (pos - new Vector3(30, 40, -50)).Normalized();
            wanderDirection = new Vector2(toCenter.x, toCenter.z).Angle();
        }

        public override void onUpdate()
        {
            Vector2 dir = new Vector2(-1, 0).Rotate(wanderDirection) * (0.17f / 4.5f);
            pos += new Vector3(dir.x, 0, dir.y);
        }
    }

}