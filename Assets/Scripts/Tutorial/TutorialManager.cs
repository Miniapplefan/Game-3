using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialManager : MonoBehaviour
{
    public SiphonTarget siphonTarget;
    public GameObject warper;

    // Start is called before the first frame update
    void Start()
    {
        warper.SetActive(false);

    }

    // Update is called once per frame
    void Update()
    {
        if (siphonTarget.dollarsLeft < 1)
        {
            warper.SetActive(true);
        }
    }
}
