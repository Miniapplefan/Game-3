using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface InputController
{
    public bool getForward();
    public bool getBackward();
    public bool getLeft();
    public bool getRight();

    public Vector2 getHeadRotation();

    public bool getFire1();
    public bool getFire2();
    public bool getFire3();

    public bool getScroll();
    public bool getScrollUp();

    public bool getScrollDown();
    public bool getReload();

    public bool getSiphon();

    public bool getAimRight();
    public bool getAimLeft();
    public bool getAimMiddle();



}
