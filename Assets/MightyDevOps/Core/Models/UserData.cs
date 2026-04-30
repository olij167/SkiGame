using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using MightyTasks;

namespace MightyTasks
{
    [System.Serializable]
    public class UserData
    {
        // Unique identifier (using timestamp for simplicity)
        public string ID;
        public string userName;
        public string bio;
        public Texture2D faceImage;
        public string lastActiveTaskID;

        // Passcode: a 4-symbol string (each symbol 1–9) representing the user's local authentication code.
        public string passcode;

        // List of proficiencies for each category.
        public List<CategoryProficiency> proficiencies = new List<CategoryProficiency>();

        // Authenticate the user using a provided passcode.
        public bool Authenticate(string inputPasscode)
        {
            return passcode == inputPasscode;
        }

        // Helper function to create a new user. IDs are generated via timestamp.
        public static UserData CreateUser(string userName, string bio, Texture2D faceImage, string passcode, List<CategoryProficiency> proficiencies)
        {
            UserData newUser = new UserData();
            newUser.ID = System.DateTime.Now.Ticks.ToString();
            newUser.userName = userName;
            newUser.bio = bio;
            newUser.faceImage = faceImage;
            newUser.passcode = passcode;
            newUser.proficiencies = proficiencies;
            newUser.lastActiveTaskID = "";
            return newUser;
        }

        // Modify this user's basic information.
        public void ModifyUser(string newName, string newBio, Texture2D newFaceImage)
        {
            userName = newName;
            bio = newBio;
            faceImage = newFaceImage;
        }

        // Find all tasks from a given list that are assigned to this user.
        // public List<TaskDataItem> FindAssociatedTasks(List<TaskDataItem> allTasks)
        // {
        //     List<TaskDataItem> result = new List<TaskDataItem>();
        //     foreach (var task in allTasks)
        //     {
        //         if (task.AssignedUserIDs.Contains(ID))
        //         {
        //             result.Add(task);
        //         }
        //     }
        //     return result;
        // }
    }

    [System.Serializable]
    public class CategoryProficiency
    {
        public string category;
        public int proficiency; // Rating from 0 to 5.
    }
}
