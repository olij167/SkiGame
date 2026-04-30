using System.Collections.Generic;
using UnityEngine;

namespace MightyTasks
{
    public static class UserManager
    {
        // The central master file for all users.
        public static UserMaster Master;

        // Call this to load the master file from Resources.
        public static void LoadMaster()
        {
            if (Master == null)
            {
                Master = Resources.Load<UserMaster>("UserMaster");
                if (Master == null)
                {
                    // If not found, create a new one (in memory).
                    Master = ScriptableObject.CreateInstance<UserMaster>();
                }
            }
        }

        // Get a user by their ID.
        public static UserData GetUserByID(string id)
        {
            if (Master == null) LoadMaster();
            foreach (var user in Master.Users)
            {
                if (user.ID == id)
                    return user;
            }
            return null;
        }

        // Return the default user (first user in the master list). If none exists, create one.
        public static UserData GetDefaultUser()
        {
            if (Master == null) LoadMaster();
            if (Master.Users.Count == 0)
            {
                UserData defaultUser = UserData.CreateUser("Default", "Default user", null, "1234", new List<CategoryProficiency>());
                Master.Users.Add(defaultUser);
                return defaultUser;
            }
            return Master.Users[0];
        }

        // Helper function to add a new user.
        public static void AddUser(UserData user)
        {
            if (Master == null) LoadMaster();
            Master.Users.Add(user);
        }

        // Helper function to remove a user.
        public static void RemoveUser(UserData user)
        {
            if (Master == null) LoadMaster();
            Master.Users.Remove(user);
        }
    }
}
