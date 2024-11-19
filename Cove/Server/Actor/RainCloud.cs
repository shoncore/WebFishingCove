namespace Cove.Server.Actor
{
  public class RainCloud : WFActor
  {

    public Vector3 ToCenter;
    public float WanderDirection;

    public bool IsStatic = false;

    public RainCloud(int ID, Vector3 entPos) : base(ID, "raincloud", Vector3.Zero)
    {
      Position = entPos;

      ToCenter = (Position - new Vector3(30, 40, -50)).Normalize();
      WanderDirection = new Vector2(ToCenter.X, ToCenter.Z).Angle();
      ShouldDespawn = true;
      DespawnTime = 550;
    }

    public override void OnUpdate()
    {
      if (IsStatic) return; // for rain that dont move

      Vector2 dir = new Vector2(-1, 0).Rotate(WanderDirection) * (0.17f / 6f);
      Position += new Vector3(dir.X, 0, dir.Y);
    }
  }
}
