using Cove.GodotFormat;

namespace Cove.Server.Actor
{
    /// <summary>
    /// Represents a server-owned actor in the game world.
    /// </summary>
    public class WFActor
    {
        /// <summary>
        /// Gets or sets the unique instance ID of the actor.
        /// </summary>
        public long InstanceID { get; set; }

        /// <summary>
        /// Gets the type of the actor.
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// Gets the time when the actor was spawned.
        /// </summary>
        public DateTimeOffset SpawnTime { get; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the position of the actor.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Gets or sets the rotation of the actor.
        /// </summary>
        public Vector3 Rotation { get; set; }

        /// <summary>
        /// Gets or sets the zone in which the actor is located.
        /// </summary>
        public string Zone { get; set; } = "main_zone";

        /// <summary>
        /// Gets or sets the owner of the zone.
        /// </summary>
        public int ZoneOwner { get; set; } = -1;

        /// <summary>
        /// Gets or sets the time after which the actor should despawn, in seconds.
        /// </summary>
        public int DespawnTime { get; set; } = -1;

        /// <summary>
        /// Gets or sets a value indicating whether the actor should despawn.
        /// </summary>
        public bool ShouldDespawn { get; set; } = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="WFActor"/> class.
        /// </summary>
        /// <param name="id">The unique instance ID of the actor.</param>
        /// <param name="type">The type of the actor.</param>
        /// <param name="position">The position of the actor.</param>
        /// <param name="rotation">The rotation of the actor. If null, defaults to zero rotation.</param>
        public WFActor(long id, string type, Vector3 position, Vector3? rotation = null)
        {
            InstanceID = id;
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Position = position;
            Rotation = rotation ?? Vector3.Zero;
        }

        /// <summary>
        /// Called on each update cycle to perform actor-specific logic.
        /// </summary>
        public virtual void OnUpdate()
        {
            // No-op by default.
        }
    }
}
