using CrashKonijn.Goap.Classes.References;
using CrashKonijn.Goap.Interfaces;

public class CommonData : IActionData
{
  [GetComponentInChildren]
  public BodyState bodyState { get; set; }
  public ITarget Target { get; set; }
  public float Timer { get; set; }
}