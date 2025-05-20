using System;
using UnityEngine;
public static class EventHandler
{
    public static event Action ReachDestinationEvent;
    public static void CallReachDestinationEvent()
    {
        ReachDestinationEvent?.Invoke();
    }


}