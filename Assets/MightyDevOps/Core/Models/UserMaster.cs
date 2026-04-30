using System.Collections.Generic;
using UnityEngine;

namespace MightyTasks
{
    [CreateAssetMenu(fileName = "UserMaster", menuName = "MightyTasks/UserMaster", order = 1)]
    public class UserMaster : ScriptableObject
    {
        public List<UserData> Users = new List<UserData>();
    }
}
