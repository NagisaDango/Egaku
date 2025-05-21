using System.Collections;
using System.Threading;
using UnityEngine;

public class LevelTransition : MonoBehaviour
{
    public float outTime = 1f;
    public float inTime = 1f;

    public Material matTransition;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnEnable()
    {
        EventHandler.ReachDestinationEvent += LoadLevel;
    }

    private void OnDisable()
    {
        EventHandler.ReachDestinationEvent -= LoadLevel;

    }



    public void LoadLevel()
    {
        StartCoroutine(ShowLevelTransition());
    }

    IEnumerator ShowLevelTransition()
    {
        float startTime = Time.time;
        float timer = Time.time;
        while (timer > timer + outTime)
        {
            var p = (timer - startTime) / outTime;
            p = Mathf.Clamp01(p);
            var r = (1 - p) * 1.5;

            matTransition.SetFloat("_Radius", (float)r);
            yield return null;
            timer  = Time.time;
        }


    }

}
