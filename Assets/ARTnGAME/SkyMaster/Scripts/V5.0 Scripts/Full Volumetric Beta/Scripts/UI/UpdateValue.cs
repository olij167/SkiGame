using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Artngame.SKYMASTER
{
    public class UpdateValue : MonoBehaviour
    {

        public Text text;

        public void ChangeValue(Slider slider)
        {
            text.text = slider.value.ToString();
        }
    }
}