using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SensorsModel : SystemModel
{
    public GameObject head;

    public GameObject headL;

    public BodyController bodyController;

    public float xRotation;
    public float yRotation;

    public float rotationSmoothDamp = 0.1f;

    public ICommand lookCommand;

    public SensorsModel(int currentLvl, BodyController bc, GameObject h, GameObject hL) : base(currentLvl)
    {
        bodyController = bc;
        head = h;
        headL = hL;
    }

    protected override void InitCommands()
    {
        lookCommand = new RotateHeadCommand(this);
    }

    public override void SetNameAndMaxLevel()
    {
        name = BodyInfo.systemID.Sensors;
        maxLevel = 4;
    }

    public void setHeadRotation(Vector2 rotation)
    {
        xRotation = rotation.x;
        yRotation = rotation.y;
        lookCommand.Execute();
    }
}
