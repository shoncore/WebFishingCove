namespace Cove.Server.Actor
{
    /// <summary>
    /// Represents a server-owned actor in the game world.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="WFActor"/> class.
    /// </remarks>
    /// <param name="ID">The unique instance ID of the actor.</param>
    /// <param name="type">The type of the actor.</param>
    /// <param name="position">The position of the actor.</param>
    /// <param name="rotation">The rotation of the actor. If null, defaults to zero rotation.</param>
    public class WFActor(long ID, string type, Vector3 position, Vector3? rotation = null)
    {
        public long InstanceId { get; set; } = ID;
        public string Type { get; set; } = type;
        public DateTimeOffset SpawnTime { get; } = DateTimeOffset.UtcNow;
        public Vector3 Position { get; set; } = position;
        public Vector3 Rotation { get; set; } = rotation ?? Vector3.Zero;
        public string Zone { get; set; } = "main_zone";
        public int ZoneOwner { get; set; } = -1;
        public int DespawnTime { get; set; } = -1;
        public bool ShouldDespawn { get; set; } = true;

        /// <summary>
        /// Called on each update cycle to perform actor-specific logic.
        /// </summary>
        public virtual void OnUpdate()
        {
            // No-op by default.
        }
    }
}
