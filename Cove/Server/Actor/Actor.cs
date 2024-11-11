using Steamworks;
using Cove.GodotFormat;

namespace Cove.Server.Actor
{
    public class WFActor
    {
        public long InstanceID { get; set; }
        public string Type { get; }
        public DateTimeOffset SpawnTime = DateTimeOffset.UtcNow;

        public Vector3 pos { get; set; }
        public Vector3 rot { get; set; }

        public string zone = "main_zone";
        public int zoneOwner = -1;

        public int despawnTime = -1;
        public bool despawn = true;

        public WFActor(long ID, string Type, Vector3 entPos, Vector3 entRot = null)
        {
            InstanceID = ID;
            this.Type = Type;
            pos = entPos;
            if (entRot != null)
                rot = entRot;
            else
                rot = Vector3.zero;
        }

        public virtual void onUpdate()
        {

        }
    }
}