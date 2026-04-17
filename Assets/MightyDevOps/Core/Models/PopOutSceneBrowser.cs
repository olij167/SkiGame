#if UNITY_EDITOR
using static Mighty.MightyCoreData;

namespace Mighty
{
    public class PopOutSceneBrowser : BasePopOutWindow
    {
        // protected VisualElement content;

        // public virtual void AddContent(VisualElement content)
        // {
        //     rootVisualElement.Clear();
        //     this.content = content;
        //     var scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
        //     scrollView.Add(content);
        //     rootVisualElement.Add(scrollView);
        // }

        private void OnEnable()
        {
            //var taskList = new TaskablesData.TaskList();
            //taskList.PopulateTasks();

            // var win = new CustomWindow(typeof(PopOutTaskCards), "Task Cards", new Vector2(taskList.view.style.width.value.value, taskList.view.style.height.value.value), new Vector2(32, 32));


            //AddContent(taskList.view);
        }

        protected override void OnPopBack()
        {
            DevLog("Pop Back Button Pressed in PopOutSceneGraph!");
            // var taskList = new TaskablesData.TaskList();
            // taskList.PopulateTasks();

            // TAO_CoreData.WindowManager.DeregisterWindow(this.titleContent.text);
            // this.titleContent.text = "Pop Back";

            // var win = new CustomWindow(typeof(PopOutTaskCards),
            //                            "Task Cards",
            //                            new Vector2(taskList.view.style.width.value.value, taskList.view.style.height.value.value),
            //                            new Vector2(32, 32),
            //                            new Vector2(330, 200),
            //                            new Vector2(330, 32000));
            // if (win.content != null)
            // {
            //     win.content.Add(taskList.view);

            //     TAO_CoreData.root.Add(win);
            // }

            // this.Close();
            // add specific PopBack actions here...
        }

        // private void OnDisable()
        // {
        //     TAO.TAO_CoreData.WindowManager.DeregisterWindow(this.titleContent.text);
        // }
    }
}
#endif