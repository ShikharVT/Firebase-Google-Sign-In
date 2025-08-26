using System;
using UnityEngine;
using Firebase;
using Firebase.Auth;

namespace BackendDev
{
    public class ActionHandler
    {
        // Authentication Based
        public static Action<FirebaseUser> OnLoginSuccess;
        public static Action OnLoginFailed;
        
        
        //GameCreation Based
        public static Action OnRoomCreated;
        public static Action OnRoomJoined;
    }
}

